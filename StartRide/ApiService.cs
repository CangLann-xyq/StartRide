using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{

    public class ApiService
    {

        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                Proxy = null,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        }

        private const string SERVER = "https://cloudfurhub.cn";
        private const string BASE = SERVER + "/api/startride";

        /// <summary>账号类接口（Steam 授权 / 激活 / 云存档 / UGC / 素材）用的根地址。</summary>
        /// <remarks>
        /// 默认是官方域名。但部分网络到该域名的 443 会被中途重置（实测 code=000，0.1 秒即断），
        /// 而同一台机器的 80 端口明文链路是通的 —— 大厅公告板早就按候选链走了，账号接口却
        /// 一直钉死在域名上：授权一失败就回退本地证据直接建号，QQ 群激活门禁因此永远不触发。
        /// 这里统一改成「大厅探明哪个根地址可用，账号接口就跟哪个」。
        /// </remarks>
        private static string ApiBase => boardBase ?? BASE;

        private static DateTime apiProbeAt;

        /// <summary>确保已探明可用根地址；最多每 30 秒探一次，失败不抛。</summary>
        private async Task EnsureApiBaseAsync()
        {
            if (boardBase != null) return;
            if ((DateTime.UtcNow - apiProbeAt).TotalSeconds < 30) return;
            apiProbeAt = DateTime.UtcNow;
            try { await GetRoomsAsync(); } catch { }
        }

        // ===================== 房间公告板根地址 =====================
        // 公告板（房间列表 / 进出房 / 心跳 / 房间聊天）按候选顺序探测：
        //   ① 中继那台机器的明文接口 http://<RelayHost>:<RelayWebSocketPort>/api/startride
        //      —— 官方中继的同端口 nginx 已经把这套接口反代到同一个公告板，
        //         而官方域名走 https，部分网络下 443 会被中途打断（TLS 被重置），
        //         这条明文链路在那种网络里依然通；自建中继则由它自己的内置板接。
        //   ② 官方 https://cloudfurhub.cn/api/startride —— 自建中继没开板接口时兜底。
        // 谁先答话就用谁，结果缓存 60 秒；账号 / 云存档 / UGC / Steam 中转仍只走官方。
        //
        // ⚠️ 旧实现只在「RelayHost 不是默认值」时才用 ①，而默认值本身就是 IP，
        //    于是默认配置恒走 ②：域名 TLS 一被干扰，大厅就整个空掉（房间登记失败、
        //    列表读空 → 双方各自建房 → 进游戏互相看不见人）。这里不再按「是不是默认值」分叉。
        private static string? boardBase;
        private static DateTime boardBaseAt;

        /// <summary>最近一次探测到的公告板根地址（诊断用）。</summary>
        public static string? BoardBaseUsed => boardBase;

        /// <summary>最近一次读房间列表失败的原因（诊断用，成功时清空）。</summary>
        public static string? LastRoomsError { get; private set; }

        private static List<string> BoardCandidates()
        {
            var list = new List<string>();
            try
            {
                var app = AppSettings.Current;
                string host = (app.RelayHost ?? "").Trim();
                int port = app.RelayWebSocketPort;
                if (host.Length > 0 && port > 0)
                {
                    list.Add("http://" + host + ":" + port + "/api/startride");
                }
            }
            catch { }
            list.Add(BASE);
            if (boardBase != null && (DateTime.UtcNow - boardBaseAt).TotalSeconds < 60 && list.Contains(boardBase))
            {
                list.Remove(boardBase);
                list.Insert(0, boardBase);
            }
            return list;
        }

        /// <summary>取出可用的公告板根：已验证过就直接用，没验证过就先读一次列表探明。</summary>
        private async Task<string> BoardBaseAsync()
        {
            if (boardBase != null) return boardBase;
            await GetRoomsAsync();          // 探一次，成功会写入 boardBase
            return boardBase ?? BASE;
        }

        public string? Token { get; set; }

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private HttpRequestMessage Authorized(HttpMethod method, string url)
        {
            var req = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(Token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            return req;
        }

        public async Task<AuthResponse?> RegisterAsync(string username, string password)
        {
            try
            {
                await EnsureApiBaseAsync();
                var resp = await _http.PostAsJsonAsync($"{ApiBase}/auth/register", new { username, password }, _jsonOpts);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<AuthResponse>>(_jsonOpts);
                if (r?.Code == 200 && r.Data != null) { Token = r.Data.Token; return r.Data; }
                return new AuthResponse { Error = r?.Msg ?? "注册失败" };
            }
            catch (Exception ex) { return new AuthResponse { Error = ex.Message }; }
        }

        public async Task<AuthResponse?> LoginAsync(string username, string password)
        {
            try
            {
                await EnsureApiBaseAsync();
                var resp = await _http.PostAsJsonAsync($"{ApiBase}/auth/login", new { username, password }, _jsonOpts);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<AuthResponse>>(_jsonOpts);
                if (r?.Code == 200 && r.Data != null) { Token = r.Data.Token; return r.Data; }
                return new AuthResponse { Error = r?.Msg ?? "登录失败" };
            }
            catch (Exception ex) { return new AuthResponse { Error = ex.Message }; }
        }

        public async Task<AuthResponse?> SteamLoginAsync(string steamId, string username, string avatar = "")
        {
            try
            {
                await EnsureApiBaseAsync();
                var resp = await _http.PostAsJsonAsync($"{ApiBase}/auth/steam", new { steamId, username, avatar }, _jsonOpts);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<AuthResponse>>(_jsonOpts);
                if (r?.Code == 200 && r.Data != null) { Token = r.Data.Token; return r.Data; }
                return new AuthResponse { Error = r?.Msg ?? "Steam 登录失败" };
            }
            catch (Exception ex) { return new AuthResponse { Error = ex.Message }; }
        }

        public async Task LogoutAsync()
        {
            try { if (Token != null) await _http.SendAsync(Authorized(HttpMethod.Post, $"{ApiBase}/auth/logout")); }
            catch { }
            Token = null;
        }

        public static string SteamOpenIdLoginUrl => $"{ApiBase}/auth/steam/login";

        public async Task<string?> GetSyncAsync(string key)
        {
            if (string.IsNullOrEmpty(Token)) return null;
            try
            {
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{ApiBase}/sync/{Uri.EscapeDataString(key)}"));
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<SyncEntry>>(_jsonOpts);
                return r?.Data?.Value;
            }
            catch { return null; }
        }

        public async Task<bool> PutSyncAsync(string key, string value)
        {
            if (string.IsNullOrEmpty(Token)) return false;
            try
            {
                var body = new StringContent(JsonSerializer.Serialize(new { value }), Encoding.UTF8, "application/json");
                var req = Authorized(HttpMethod.Put, $"{ApiBase}/sync/{Uri.EscapeDataString(key)}");
                req.Content = body;
                var resp = await _http.SendAsync(req);
                var res = await resp.Content.ReadFromJsonAsync<ApiResult<object>>(_jsonOpts);
                return res?.Code == 200;
            }
            catch { return false; }
        }

        public async Task<bool> MergeSyncAsync(Dictionary<string, string> items)
        {
            if (string.IsNullOrEmpty(Token) || items == null || items.Count == 0) return false;
            try
            {
                var req = Authorized(HttpMethod.Post, $"{ApiBase}/sync/merge");
                req.Content = new StringContent(JsonSerializer.Serialize(new { items }), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                var res = await resp.Content.ReadFromJsonAsync<ApiResult<object>>(_jsonOpts);
                return res?.Code == 200;
            }
            catch { return false; }
        }

        public async Task<Dictionary<string, SyncEntry>?> GetAllSyncAsync()
        {
            if (string.IsNullOrEmpty(Token)) return null;
            try
            {
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{ApiBase}/sync"));
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<Dictionary<string, SyncEntry>>>(_jsonOpts);
                return r?.Data;
            }
            catch { return null; }
        }

        public async Task<bool> PostUgcAsync(string kind, string targetId, int rating, string content)
        {
            if (string.IsNullOrEmpty(Token)) return false;
            try
            {
                var req = Authorized(HttpMethod.Post, $"{ApiBase}/ugc");
                req.Content = new StringContent(JsonSerializer.Serialize(new { kind, targetId, rating, content }), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<object>>(_jsonOpts);
                return r?.Code == 200;
            }
            catch { return false; }
        }

        public async Task<UgcResult?> GetUgcAsync(string kind, string targetId)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiResult<UgcResult>>(
                    $"{ApiBase}/ugc?kind={Uri.EscapeDataString(kind)}&targetId={Uri.EscapeDataString(targetId)}", _jsonOpts);
                return resp?.Data;
            }
            catch { return null; }
        }

        public async Task<List<ConfigTemplateInfo>> GetConfigTemplatesAsync()
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiResult<List<ConfigTemplateInfo>>>(
                    $"{ApiBase}/config/templates", _jsonOpts);
                return resp?.Data ?? new List<ConfigTemplateInfo>();
            }
            catch { return new List<ConfigTemplateInfo>(); }
        }

        public async Task<ConfigTemplateContent?> GetConfigTemplateAsync(string key)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiResult<ConfigTemplateContent>>(
                    $"{ApiBase}/config/template/{Uri.EscapeDataString(key)}", _jsonOpts);
                return resp?.Data;
            }
            catch { return null; }
        }

        /// <summary>
        /// 读公开房间列表。返回 null 表示**真的读不到**（所有候选地址都失败），
        /// 调用方据此区分「没有房间」和「读不到」——旧实现把异常吞成空列表，
        /// 大厅于是显示「暂无公开房间」，玩家根本不知道是自己网络的问题。
        /// </summary>
        public async Task<List<Room>?> GetRoomsAsync()
        {
            LastRoomsError = null;
            string? firstError = null;
            foreach (string url in BoardCandidates())
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                    var resp = await _http.GetFromJsonAsync<ApiResult<List<Room>>>(
                        url + "/rooms", _jsonOpts, cts.Token);
                    if (resp?.Data != null)
                    {
                        boardBase = url;
                        boardBaseAt = DateTime.UtcNow;
                        return resp.Data;
                    }
                    firstError ??= url + " 返回空响应";
                }
                catch (Exception ex)
                {
                    firstError ??= url + " → " + ex.Message;
                }
            }
            LastRoomsError = firstError ?? "没有可用的公告板地址";
            return null;
        }

        public async Task<Room?> CreateRoomAsync(Room room)
        {
            string url = await BoardBaseAsync();
            try
            {
                var resp = await _http.PostAsJsonAsync(url + "/rooms", room, _jsonOpts);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<Room>>(_jsonOpts);
                if (r?.Code != 200) throw new InvalidOperationException($"{url} 返回 code={r?.Code} {r?.Msg}");
                return r?.Data;
            }
            catch (Exception ex) { throw new Exception($"创建房间失败({url}): {ex.Message}"); }
        }

        public async Task CloseRoomAsync(string roomId, string by)
        {
            try { await _http.DeleteAsync((await BoardBaseAsync()) + "/rooms/" + roomId); } catch { }
        }

        public async Task RoomHeartbeatAsync(string roomId)
        {
            try { await _http.PostAsync((await BoardBaseAsync()) + "/rooms/" + roomId + "/heartbeat", null); } catch { }
        }

        public async Task JoinRoomAsync(string roomId, string player)
        {
            try
            {
                await _http.PostAsJsonAsync((await BoardBaseAsync()) + "/rooms/" + roomId + "/join",
                    new { player }, _jsonOpts);
            }
            catch { }
        }

        public async Task LeaveRoomAsync(string roomId, string player)
        {
            try
            {
                await _http.PostAsJsonAsync((await BoardBaseAsync()) + "/rooms/" + roomId + "/leave",
                    new { player }, _jsonOpts);
            }
            catch { }
        }

        public async Task SendChatAsync(string roomId, string player, string text)
        {
            try
            {
                await _http.PostAsJsonAsync((await BoardBaseAsync()) + "/rooms/" + roomId + "/chat",
                    new { player, text }, _jsonOpts);
            }
            catch { }
        }

        public async Task<ChatResponse?> GetChatAsync(string roomId, int sinceId)
        {
            try
            {
                return await _http.GetFromJsonAsync<ChatResponse>(
                    (await BoardBaseAsync()) + "/rooms/" + roomId + "/chat?since=" + sinceId, _jsonOpts);
            }
            catch { return null; }
        }

        // ---- Steam 时长 / 成就 ----
        // 客户端不直连 api.steampowered.com（国内不通），一律经服务器中转，
        // Steam Web API Key 只存在服务器上，不下发到客户端。

        /// <summary>
        /// 刷新并取回 Steam 侧记录的权威时长。
        /// forceFresh：跳过服务端 5 分钟缓存。退出游戏后立刻回读必须用它，
        /// 否则可能命中启动时写下的缓存，刚玩的那几分钟看不到。
        /// </summary>
        // ===================== 激活状态 =====================
        // 注册（含 Steam 授权自动建号）后账号默认是「未激活」：启动器把一次性激活码
        // 显示给用户，用户加 QQ 群把码发到群里，群机器人核验通过后置 activated。
        // 这里只读状态 —— 能不能激活只有服务端说了算，客户端不参与判定。
        /// <summary>带判据地查激活状态：区分未激活 / 账号失效 / 网络失败。</summary>
        public async Task<ActivationCheck> CheckActivationAsync()
        {
            if (string.IsNullOrEmpty(Token))
            {
                return new ActivationCheck { State = ActivationState.AccountInvalid, Error = "本地没有登录凭据" };
            }
            await EnsureApiBaseAsync();
            try
            {
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{ApiBase}/activation"));
                int code = (int)resp.StatusCode;
                if (code == 401 || code == 403 || code == 404)
                {
                    return new ActivationCheck { State = ActivationState.AccountInvalid, Error = "HTTP " + code };
                }
                if (!resp.IsSuccessStatusCode)
                {
                    return new ActivationCheck { State = ActivationState.NetworkError, Error = "HTTP " + code };
                }
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<ActivationStatus>>(_jsonOpts);
                if (r?.Code == 200 && r.Data != null)
                {
                    return new ActivationCheck
                    {
                        State = r.Data.Activated ? ActivationState.Ok : ActivationState.NotActivated,
                        Status = r.Data
                    };
                }
                return new ActivationCheck
                {
                    State = ActivationState.AccountInvalid,
                    Error = r?.Msg ?? ("code=" + r?.Code)
                };
            }
            catch (Exception ex)
            {
                return new ActivationCheck { State = ActivationState.NetworkError, Error = ex.Message };
            }
        }

        public async Task<ActivationStatus?> GetActivationAsync()
        {
            if (string.IsNullOrEmpty(Token)) return null;
            await EnsureApiBaseAsync();
            try
            {
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{ApiBase}/activation"));
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<ActivationStatus>>(_jsonOpts);
                return r?.Code == 200 ? r.Data : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// 注销 StartRide 账号（不可撤销）。
        /// 服务端删账号行（含 QQ 绑定、激活状态、激活码）与云端同步/UGC/AI 用量；
        /// Steam 账号、游戏本体、本机存档一概不动 —— 注销后可以用同一 Steam 账号重新登录，
        /// 只是要拿新的激活码重新加群激活。
        /// confirmUsername 原样回传当前昵称，服务端据此二次确认，避免误删别人的号。
        /// </summary>
        public async Task<AccountDeleteResult> DeleteAccountAsync(string confirmUsername)
        {
            if (string.IsNullOrEmpty(Token))
            {
                return new AccountDeleteResult { Ok = false, Message = "当前没有登录 StartRide 账号" };
            }
            await EnsureApiBaseAsync();
            try
            {
                var req = Authorized(HttpMethod.Post, $"{ApiBase}/account/delete");
                req.Content = JsonContent.Create(new { confirm = confirmUsername ?? "" }, options: _jsonOpts);
                var resp = await _http.SendAsync(req);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<AccountDeletePayload>>(_jsonOpts);
                if (r?.Code == 200)
                {
                    return new AccountDeleteResult { Ok = true, Message = r.Msg ?? "账号已注销" };
                }
                return new AccountDeleteResult
                {
                    Ok = false,
                    Message = r?.Msg ?? ("注销失败（HTTP " + (int)resp.StatusCode + "）")
                };
            }
            catch (Exception ex)
            {
                return new AccountDeleteResult { Ok = false, Message = "注销失败：" + ex.Message };
            }
        }

        public async Task<SteamProfileInfo?> GetSteamProfileAsync(bool forceFresh = false)
        {
            if (string.IsNullOrEmpty(Token)) return null;
            try
            {
                string url = $"{ApiBase}/steam/profile" + (forceFresh ? "?fresh=1" : "");
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, url));
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<SteamProfileInfo>>(_jsonOpts);
                return r?.Code == 200 ? r.Data : null;
            }
            catch { return null; }
        }

        /// <summary>取回成就（名称/描述已按 lang 本地化，并带全球解锁率）。</summary>
        public async Task<SteamAchievementResult?> GetSteamAchievementsAsync(string lang)
        {
            if (string.IsNullOrEmpty(Token)) return null;
            try
            {
                string q = Uri.EscapeDataString(string.IsNullOrWhiteSpace(lang) ? "zh-Hans" : lang);
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{ApiBase}/steam/achievements?lang={q}"));
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<SteamAchievementResult>>(_jsonOpts);
                return r?.Code == 200 ? r.Data : null;
            }
            catch { return null; }
        }
    }

    /// <summary>Steam 侧时长快照（服务器查 Web API 得到）。</summary>
    public class SteamProfileInfo
    {
        public bool Bound { get; set; }
        public string? SteamId { get; set; }
        public bool? OwnsBeamng { get; set; }
        public long PlaytimeMinutes { get; set; }
        public long Playtime2WeeksMinutes { get; set; }
        public long LastPlayedUnix { get; set; }
        public int AchievementsUnlocked { get; set; }
        public int AchievementsTotal { get; set; }
        public string? SyncedAt { get; set; }
        /// <summary>false = 本次没从 Steam 取到，返回的是库里上次同步的值。</summary>
        public bool? Fresh { get; set; }
    }

    /// <summary>Steam 成就查询结果。</summary>
    public class SteamAchievementResult
    {
        public bool Bound { get; set; }
        public bool Available { get; set; }
        public string? Reason { get; set; }
        public int Total { get; set; }
        public int Unlocked { get; set; }
        public List<SteamAchievementItem>? List { get; set; }
        public SteamPlayerStats? Stats { get; set; }
    }

    public class SteamAchievementItem
    {
        public string Api { get; set; } = "";
        public string Name { get; set; } = "";
        public string Desc { get; set; } = "";
        public bool Achieved { get; set; }
        public long UnlockedAt { get; set; }
        public double? Percent { get; set; }
    }

    /// <summary>Steam 记的玩家数值（GetUserStatsForGame）。</summary>
    public class SteamPlayerStats
    {
        public long MetersDriven { get; set; }
        public long AirTimeMinutes { get; set; }
        public long RollOver { get; set; }
        public long VehiclesSpawned { get; set; }
    }

    public class ApiResult<T>
    {
        public int Code { get; set; }
        public string? Msg { get; set; }
        public T? Data { get; set; }
    }

    public class Room
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>玩法模式 id（见 LobbyGameModeCatalog）。旧字段，保留兼容。</summary>
        public string Mode { get; set; } = "freeroam";
        /// <summary>本房玩法模式 id。是房间的权威宣告，加入者据此显示玩法。</summary>
        public string GameMode { get; set; } = "free_drive";
        /// <summary>玩法规则版本号；两端不一致时说明对端是旧版规则。</summary>
        public int GameModeRevision { get; set; } = 1;
        public string Map { get; set; } = "west_coast_usa";
        public string Host { get; set; } = "";
        public string? HostIp { get; set; }
        public int HostPort { get; set; } = 7777;
        public int Capacity { get; set; } = 8;
        public int Players { get; set; } = 1;
        public List<string>? PlayerList { get; set; }
        public bool Live { get; set; }
        public string? SteamId { get; set; }
        public string? Password { get; set; }
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public string Player { get; set; } = "";
        public string Text { get; set; } = "";
        public long Timestamp { get; set; }
        public string? Time { get; set; }
    }

    public class ChatResponse
    {
        public int Code { get; set; }
        public List<ChatMessage>? Data { get; set; }
        public int Latest { get; set; }
    }

    public class AuthResponse
    {
        public string? Username { get; set; }
        public string? SteamId { get; set; }
        public string? Avatar { get; set; }
        public bool? OwnsBeamng { get; set; }
        public long BeamngPlaytimeMin { get; set; }
        /// <summary>是否已通过 QQ 群验证；老账号在服务端迁移时统一置真。</summary>
        public bool Activated { get; set; }
        /// <summary>一次性激活码（SR-XXXXXX），用户要把它发到群里。</summary>
        public string? ActivationCode { get; set; }
        /// <summary>已绑定的群成员 QQ。</summary>
        public string? Qq { get; set; }
        public string? Token { get; set; }
        public string? Error { get; set; }
    }

    public class ActivationStatus
    {
        public bool Activated { get; set; }
        public string? Code { get; set; }
        public string? Qq { get; set; }
        public string? ActivatedAt { get; set; }
        public string? GroupUrl { get; set; }
        public string? GroupId { get; set; }
    }

    /// <summary>激活校验判据。</summary>
    /// <remarks>
    /// 旧实现只用「拿到状态 / 拿不到状态」两态，拿不到就当没事放行 —— 后台清过库之后
    /// token 早就失效，客户端却把 401 也当成「网络抖动」放过去，等于谁都不用重新认证。
    /// 这里把四种情况分开，调用方才能决定是放行、提示激活、还是赶回添加账户。
    /// </remarks>
    public enum ActivationState
    {
        /// <summary>已激活。</summary>
        Ok,
        /// <summary>账号在，但没在群里发过码。</summary>
        NotActivated,
        /// <summary>服务端没有这个账号，或 token 已失效（例如后台清过库）。</summary>
        AccountInvalid,
        /// <summary>网络或服务不可达，无法判定。</summary>
        NetworkError
    }

    public sealed class ActivationCheck
    {
        public ActivationState State { get; init; }
        public ActivationStatus? Status { get; init; }
        public string? Error { get; init; }

        /// <summary>是否必须重新走一遍认证（未激活或账号已失效）。</summary>
        public bool IsBlocked => State == ActivationState.NotActivated || State == ActivationState.AccountInvalid;
    }

    public sealed class AccountDeletePayload
    {
        public string? Username { get; set; }
    }

    /// <summary>注销结果。失败也要有能直接给用户看的话（服务端 msg 原样透出）。</summary>
    public sealed class AccountDeleteResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
    }

    public class SyncEntry
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
        public string? UpdatedAt { get; set; }
    }

    public class UgcResult
    {
        public List<UgcItem>? List { get; set; }
        public int Count { get; set; }
        public double AvgRating { get; set; }
    }

    public class UgcItem
    {
        public string? Username { get; set; }
        public int Rating { get; set; }
        public string? Content { get; set; }
        public string? CreatedAt { get; set; }
    }

    public class ConfigTemplateInfo
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public int Size { get; set; }
        public string Sha256 { get; set; } = "";
        public string? GameVersion { get; set; }
        public string? UpdatedAt { get; set; }
    }

    public class ConfigTemplateContent
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int Size { get; set; }
        public string? UpdatedAt { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>
    /// StartRide 云后端 API 客户端
    /// 生产走 HTTPS 域名 https://windseek.cloud/api/startride（nginx 反代到 :3002）
    /// —— 登录 token / 用户数据全程加密，不再走明文 IP。
    /// </summary>
    public class ApiService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
        private const string SERVER = "https://windseek.cloud";
        private const string BASE = SERVER + "/api/startride";

        /// <summary>当前登录 token（Steam 授权或用户名密码登录后写入）</summary>
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

        // ===== 认证 =====

        public async Task<AuthResponse?> RegisterAsync(string username, string password)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{BASE}/auth/register", new { username, password }, _jsonOpts);
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
                var resp = await _http.PostAsJsonAsync($"{BASE}/auth/login", new { username, password }, _jsonOpts);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<AuthResponse>>(_jsonOpts);
                if (r?.Code == 200 && r.Data != null) { Token = r.Data.Token; return r.Data; }
                return new AuthResponse { Error = r?.Msg ?? "登录失败" };
            }
            catch (Exception ex) { return new AuthResponse { Error = ex.Message }; }
        }

        /// <summary>SteamID 直接登录（本地 vdf 兜底，不走 OpenID）</summary>
        public async Task<AuthResponse?> SteamLoginAsync(string steamId, string username, string avatar = "")
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{BASE}/auth/steam", new { steamId, username, avatar }, _jsonOpts);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<AuthResponse>>(_jsonOpts);
                if (r?.Code == 200 && r.Data != null) { Token = r.Data.Token; return r.Data; }
                return new AuthResponse { Error = r?.Msg ?? "Steam 登录失败" };
            }
            catch (Exception ex) { return new AuthResponse { Error = ex.Message }; }
        }

        /// <summary>登出</summary>
        public async Task LogoutAsync()
        {
            try { if (Token != null) await _http.SendAsync(Authorized(HttpMethod.Post, $"{BASE}/auth/logout")); }
            catch { }
            Token = null;
        }

        /// <summary>Steam OpenID 授权跳转地址（真 OAuth）</summary>
        public static string SteamOpenIdLoginUrl => $"{BASE}/auth/steam/login";

        // ===== 云同步 =====

        /// <summary>拉取某个同步项（返回 JSON 字符串，无则 null）</summary>
        public async Task<string?> GetSyncAsync(string key)
        {
            if (string.IsNullOrEmpty(Token)) return null;
            try
            {
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{BASE}/sync/{Uri.EscapeDataString(key)}"));
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<SyncEntry>>(_jsonOpts);
                return r?.Data?.Value;
            }
            catch { return null; }
        }

        /// <summary>写入某个同步项（实时增量）</summary>
        public async Task<bool> PutSyncAsync(string key, string value)
        {
            if (string.IsNullOrEmpty(Token)) return false;
            try
            {
                var body = new StringContent(JsonSerializer.Serialize(new { value }), Encoding.UTF8, "application/json");
                var req = Authorized(HttpMethod.Put, $"{BASE}/sync/{Uri.EscapeDataString(key)}");
                req.Content = body;
                var resp = await _http.SendAsync(req);
                var res = await resp.Content.ReadFromJsonAsync<ApiResult<object>>(_jsonOpts);
                return res?.Code == 200;
            }
            catch { return false; }
        }

        /// <summary>批量合并同步项（把本地状态一次性 push 到云）</summary>
        public async Task<bool> MergeSyncAsync(Dictionary<string, string> items)
        {
            if (string.IsNullOrEmpty(Token) || items == null || items.Count == 0) return false;
            try
            {
                var req = Authorized(HttpMethod.Post, $"{BASE}/sync/merge");
                req.Content = new StringContent(JsonSerializer.Serialize(new { items }), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                var res = await resp.Content.ReadFromJsonAsync<ApiResult<object>>(_jsonOpts);
                return res?.Code == 200;
            }
            catch { return false; }
        }

        /// <summary>拉取全部云同步项</summary>
        public async Task<Dictionary<string, SyncEntry>?> GetAllSyncAsync()
        {
            if (string.IsNullOrEmpty(Token)) return null;
            try
            {
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{BASE}/sync"));
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<Dictionary<string, SyncEntry>>>(_jsonOpts);
                return r?.Data;
            }
            catch { return null; }
        }

        // ===== UGC =====

        /// <summary>发布评分/评论/分享</summary>
        public async Task<bool> PostUgcAsync(string kind, string targetId, int rating, string content)
        {
            if (string.IsNullOrEmpty(Token)) return false;
            try
            {
                var req = Authorized(HttpMethod.Post, $"{BASE}/ugc");
                req.Content = new StringContent(JsonSerializer.Serialize(new { kind, targetId, rating, content }), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<object>>(_jsonOpts);
                return r?.Code == 200;
            }
            catch { return false; }
        }

        /// <summary>拉取某目标的 UGC（评分/评论）</summary>
        public async Task<UgcResult?> GetUgcAsync(string kind, string targetId)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiResult<UgcResult>>(
                    $"{BASE}/ugc?kind={Uri.EscapeDataString(kind)}&targetId={Uri.EscapeDataString(targetId)}", _jsonOpts);
                return resp?.Data;
            }
            catch { return null; }
        }

        // ===== 游戏必需配置文件模板 =====

        /// <summary>云端保存的配置文件模板清单（不含正文）。</summary>
        public async Task<List<ConfigTemplateInfo>> GetConfigTemplatesAsync()
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiResult<List<ConfigTemplateInfo>>>(
                    $"{BASE}/config/templates", _jsonOpts);
                return resp?.Data ?? new List<ConfigTemplateInfo>();
            }
            catch { return new List<ConfigTemplateInfo>(); }
        }

        /// <summary>取某个模板正文（配置损坏时用它补全）。</summary>
        public async Task<ConfigTemplateContent?> GetConfigTemplateAsync(string key)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiResult<ConfigTemplateContent>>(
                    $"{BASE}/config/template/{Uri.EscapeDataString(key)}", _jsonOpts);
                return resp?.Data;
            }
            catch { return null; }
        }

        // ===== 房间 =====

        public async Task<List<Room>> GetRoomsAsync()
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiResult<List<Room>>>($"{BASE}/rooms", _jsonOpts);
                return resp?.Data ?? new List<Room>();
            }
            catch { return new List<Room>(); }
        }

        public async Task<Room?> CreateRoomAsync(Room room)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{BASE}/rooms", room, _jsonOpts);
                var r = await resp.Content.ReadFromJsonAsync<ApiResult<Room>>(_jsonOpts);
                return r?.Data;
            }
            catch (Exception ex) { throw new Exception($"创建房间失败: {ex.Message}"); }
        }

        public async Task CloseRoomAsync(string roomId, string by)
        {
            try { await _http.DeleteAsync($"{BASE}/rooms/{roomId}"); } catch { }
        }

        public async Task RoomHeartbeatAsync(string roomId)
        {
            try { await _http.PostAsync($"{BASE}/rooms/{roomId}/heartbeat", null); } catch { }
        }

        public async Task JoinRoomAsync(string roomId, string player)
        {
            try { await _http.PostAsJsonAsync($"{BASE}/rooms/{roomId}/join", new { player }, _jsonOpts); } catch { }
        }

        public async Task LeaveRoomAsync(string roomId, string player)
        {
            try { await _http.PostAsJsonAsync($"{BASE}/rooms/{roomId}/leave", new { player }, _jsonOpts); } catch { }
        }

        // ===== 聊天 =====

        public async Task SendChatAsync(string roomId, string player, string text)
        {
            try { await _http.PostAsJsonAsync($"{BASE}/rooms/{roomId}/chat", new { player, text }, _jsonOpts); } catch { }
        }

        public async Task<ChatResponse?> GetChatAsync(string roomId, int sinceId)
        {
            try
            {
                return await _http.GetFromJsonAsync<ChatResponse>($"{BASE}/rooms/{roomId}/chat?since={sinceId}", _jsonOpts);
            }
            catch { return null; }
        }
    }

    // ===== 数据模型 =====

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
        public string Mode { get; set; } = "freeroam";
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
        /// <summary>三态：true=确认拥有 BeamNG / false=确认没有 / null=无法判断（未配置 Key）</summary>
        public bool? OwnsBeamng { get; set; }
        public long BeamngPlaytimeMin { get; set; }
        public string? Token { get; set; }
        public string? Error { get; set; }
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

    /// <summary>云端配置模板条目（清单用，不含正文）</summary>
    public class ConfigTemplateInfo
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public int Size { get; set; }
        public string Sha256 { get; set; } = "";
        public string? GameVersion { get; set; }
        public string? UpdatedAt { get; set; }
    }

    /// <summary>云端配置模板正文</summary>
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

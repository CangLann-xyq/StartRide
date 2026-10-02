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

        private const string SERVER = "https://windseek.cloud";
        private const string BASE = SERVER + "/api/startride";

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

        public async Task LogoutAsync()
        {
            try { if (Token != null) await _http.SendAsync(Authorized(HttpMethod.Post, $"{BASE}/auth/logout")); }
            catch { }
            Token = null;
        }

        public static string SteamOpenIdLoginUrl => $"{BASE}/auth/steam/login";

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

        // ---- Steam 时长 / 成就 ----
        // 客户端不直连 api.steampowered.com（国内不通），一律经服务器中转，
        // Steam Web API Key 只存在服务器上，不下发到客户端。

        /// <summary>
        /// 刷新并取回 Steam 侧记录的权威时长。
        /// forceFresh：跳过服务端 5 分钟缓存。退出游戏后立刻回读必须用它，
        /// 否则可能命中启动时写下的缓存，刚玩的那几分钟看不到。
        /// </summary>
        public async Task<SteamProfileInfo?> GetSteamProfileAsync(bool forceFresh = false)
        {
            if (string.IsNullOrEmpty(Token)) return null;
            try
            {
                string url = $"{BASE}/steam/profile" + (forceFresh ? "?fresh=1" : "");
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
                var resp = await _http.SendAsync(Authorized(HttpMethod.Get, $"{BASE}/steam/achievements?lang={q}"));
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

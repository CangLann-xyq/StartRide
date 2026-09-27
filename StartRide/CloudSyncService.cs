using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>
    /// 云同步服务：把启动器本地状态（配置/账户/下载记录/回放车辆列表）实时增量同步到
    /// 云后端（https://windseek.cloud/api/startride/sync，nginx 反代 :3002），登录后启动时拉取云端最新状态合并。
    ///
    /// 所有 push 都防抖 + 异步 + 失败静默（云不可用时不影响本地功能，下次操作再补推）。
    /// </summary>
    public sealed class CloudSyncService : IDisposable
    {
        private const string AUTH_FILE = "cloud-auth.json"; // %AppData%\StartRide\ 下

        private readonly ApiService _api;
        private readonly AppSettings _settings;
        private readonly object _gate = new();
        private CancellationTokenSource? _debounceCts;
        private bool _disposed;

        /// <summary>当前登录态（null 表示未登录云端）。</summary>
        public AuthResponse? Auth { get; private set; }

        /// <summary>是否有云端登录态。</summary>
        public bool IsLoggedIn => Auth != null && !string.IsNullOrEmpty(Auth.Token);

        public CloudSyncService(ApiService api, AppSettings settings)
        {
            _api = api;
            _settings = settings;
            LoadAuth();
            AppSettings.Saved += OnSettingsSaved;
        }

        private void OnSettingsSaved(AppSettings s)
        {
            if (_disposed) return;
            PushSettings();
        }

        private static string AuthPath => Path.Combine(AppSettings.ConfigDirectory, AUTH_FILE);

        // ================= 登录态 =================

        private void LoadAuth()
        {
            try
            {
                if (File.Exists(AuthPath))
                {
                    var json = File.ReadAllText(AuthPath);
                    var auth = JsonSerializer.Deserialize<AuthResponse>(json);
                    if (auth != null && !string.IsNullOrEmpty(auth.Token))
                    {
                        Auth = auth;
                        _api.Token = auth.Token;
                    }
                }
            }
            catch
            {
                // 登录态损坏视为未登录
            }
        }

        /// <summary>保存登录态（登录成功时调用）。</summary>
        public void SaveAuth(AuthResponse auth)
        {
            Auth = auth;
            _api.Token = auth.Token;
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDirectory);
                File.WriteAllText(AuthPath, JsonSerializer.Serialize(auth));
            }
            catch
            {
                // 写盘失败不影响本次会话
            }
        }

        /// <summary>清除登录态（登出）。</summary>
        public void ClearAuth()
        {
            Auth = null;
            _api.Token = null;
            try { if (File.Exists(AuthPath)) File.Delete(AuthPath); } catch { }
        }

        // ================= push（实时增量）=================

        /// <summary>把整个设置对象 push 到云端（防抖合并）。</summary>
        public void PushSettings()
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(_settings);
                return _api.PutSyncAsync("settings", json);
            });
        }

        /// <summary>push 账户列表（账户名/steamId/头像，不含敏感 token）。</summary>
        public void PushAccounts(IEnumerable<object> accounts)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(accounts);
                return _api.PutSyncAsync("accounts", json);
            });
        }

        /// <summary>push 下载记录（已下载模组列表）。</summary>
        public void PushDownloads(IEnumerable<object> downloads)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(downloads);
                return _api.PutSyncAsync("downloads", json);
            });
        }

        /// <summary>push 回放列表。</summary>
        public void PushReplays(IEnumerable<object> replays)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(replays);
                return _api.PutSyncAsync("replays", json);
            });
        }

        /// <summary>push 车辆列表。</summary>
        public void PushVehicles(IEnumerable<object> vehicles)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(vehicles);
                return _api.PutSyncAsync("vehicles", json);
            });
        }

        // ================= pull（登录后合并）=================

        /// <summary>登录成功后拉取云端全部同步项。</summary>
        public async Task<Dictionary<string, string>?> PullAllAsync()
        {
            if (!IsLoggedIn) return null;
            var entries = await _api.GetAllSyncAsync();
            var result = new Dictionary<string, string>();
            if (entries != null)
            {
                foreach (var kv in entries)
                    if (kv.Value?.Value != null)
                        result[kv.Key] = kv.Value.Value;
            }
            return result;
        }

        /// <summary>拉取云端的设置并合并到本地（云端优先，覆盖本地）。</summary>
        public async Task<bool> PullSettingsAsync()
        {
            var v = await _api.GetSyncAsync("settings");
            if (v == null) return false;
            try
            {
                var remote = JsonSerializer.Deserialize<AppSettings>(v);
                if (remote == null) return false;
                // 用云端值覆盖本地（保留本机路径等敏感字段）
                _settings.AccentKey = remote.AccentKey;
                _settings.Language = remote.Language;
                _settings.AutoUpdate = remote.AutoUpdate;
                _settings.DownloadThreads = remote.DownloadThreads;
                _settings.DownloadSpeedLimitKbps = remote.DownloadSpeedLimitKbps;
                _settings.MinimizeToTray = remote.MinimizeToTray;
                _settings.MaxMemoryMB = remote.MaxMemoryMB;
                _settings.ExtraLaunchArgs = remote.ExtraLaunchArgs;
                _settings.AutoCheckVehicleMods = remote.AutoCheckVehicleMods;
                _settings.ForceHighPerformanceGpu = remote.ForceHighPerformanceGpu;
                _settings.Save();
                return true;
            }
            catch { return false; }
        }

        // ================= 防抖 =================

        private void Debounce(Func<Task<bool>> work)
        {
            lock (_gate)
            {
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                var ct = _debounceCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1200, ct).ConfigureAwait(false);
                        await work().ConfigureAwait(false);
                    }
                    catch
                    {
                        // 云不可用静默，下次操作再补推
                    }
                }, ct);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            AppSettings.Saved -= OnSettingsSaved;
            lock (_gate)
            {
                _debounceCts?.Cancel();
                _debounceCts = null;
            }
        }
    }
}

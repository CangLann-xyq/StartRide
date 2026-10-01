using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{

    public sealed class CloudSyncService : IDisposable
    {
        private const string AUTH_FILE = "cloud-auth.json";

        private readonly ApiService _api;
        private readonly AppSettings _settings;
        private readonly object _gate = new();
        private CancellationTokenSource? _debounceCts;
        private bool _disposed;

        public AuthResponse? Auth { get; private set; }

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
            }
        }

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
            }
        }

        public void ClearAuth()
        {
            Auth = null;
            _api.Token = null;
            try { if (File.Exists(AuthPath)) File.Delete(AuthPath); } catch { }
        }

        public void PushSettings()
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(_settings);
                return _api.PutSyncAsync("settings", json);
            });
        }

        public void PushAccounts(IEnumerable<object> accounts)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(accounts);
                return _api.PutSyncAsync("accounts", json);
            });
        }

        public void PushDownloads(IEnumerable<object> downloads)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(downloads);
                return _api.PutSyncAsync("downloads", json);
            });
        }

        public void PushReplays(IEnumerable<object> replays)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(replays);
                return _api.PutSyncAsync("replays", json);
            });
        }

        public void PushVehicles(IEnumerable<object> vehicles)
        {
            if (!IsLoggedIn) return;
            Debounce(() =>
            {
                var json = JsonSerializer.Serialize(vehicles);
                return _api.PutSyncAsync("vehicles", json);
            });
        }

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

        public async Task<bool> PullSettingsAsync()
        {
            var v = await _api.GetSyncAsync("settings");
            if (v == null) return false;
            try
            {
                var remote = JsonSerializer.Deserialize<AppSettings>(v);
                if (remote == null) return false;
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

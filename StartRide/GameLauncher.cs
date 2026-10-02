using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StartRide.Core
{
    public sealed class GameLauncher
    {
        private readonly AppSettings _settings;
        private Process? _process;
        private JobMemoryLimiter.AppliedLimit? _memoryLimit;

        public GameLauncher(AppSettings settings) => _settings = settings;

        /// <summary>BeamNG 在 Steam 上的 appid。游戏目录里没有 steam_appid.txt，只能由启动器告诉它。</summary>
        public const string BeamNgSteamAppId = "284160";

        public event Action<string>? Log;
        public event Action<bool>? RunningChanged;

        /// <summary>
        /// 任意实例的日志都会走这里。为什么必须静态：主页/实例/回放/托盘会各自 new 一个
        /// GameLauncher（要指向不同实例目录），实例级 Log 只服务各自流程，而日志落盘是全局行为。
        /// 2026-10-02 实测：只挂 AppState.Launcher 时，从主页启动的一局在日志里一条都看不到。
        /// </summary>
        public static event Action<string>? AnyLog;

        /// <summary>
        /// 任意实例启动的游戏退出都会触发。
        /// 用途：Steam 时长回读（AppState）—— 它是全局行为，挂在实例上就会漏掉
        /// 主页/实例/回放/托盘这四条入口（2026-10-02 实测踩到）。
        /// </summary>
        public static event Action? AnyGameExited;

        /// <summary>
        /// 本进程内「已启动且仍在跑」的游戏目录。
        /// 同一目录重复启动会让 BeamNG 崩（2026-10-02 实测 0xC0000409）。
        /// </summary>
        private static readonly HashSet<string> RunningDirectories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object RunningGate = new object();

        private void EmitLog(string message)
        {
            Log?.Invoke(message);
            AnyLog?.Invoke(message);
        }

        public bool IsRunning => _process is { HasExited: false };
        public int? ProcessId => IsRunning ? _process?.Id : null;

        public int AppliedMemoryLimitMb { get; private set; }

        public string ExecutablePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_settings.GameDirectory)) return "";
                string exe = Path.Combine(_settings.GameDirectory, "BeamNG.drive.exe");
                return File.Exists(exe) ? exe : "";
            }
        }

        public bool IsInstalled => ExecutablePath.Length > 0;

        public string GameVersion
        {
            get
            {
                try
                {
                    string vt = Path.Combine(_settings.GameDirectory, "version.txt");
                    if (File.Exists(vt))
                    {
                        string v = File.ReadAllText(vt).Trim();
                        if (v.Length > 0) return v;
                    }
                }
                catch { }

                try
                {
                    string root = _settings.ResolveUserDataRoot();
                    foreach (var log in new[]
                             {
                                 Path.Combine(root, "beamng-launcher.log"),
                                 Path.Combine(root, "current", "beamng-launcher.log"),
                             })
                    {
                        if (!File.Exists(log)) continue;
                        string head = File.ReadLines(log).FirstOrDefault() ?? "";
                        var m = System.Text.RegularExpressions.Regex.Match(head, @"\bv\s*(\d+\.\d+\.\d+\.\d+)\b");
                        if (m.Success) return m.Groups[1].Value;
                    }
                }
                catch { }

                try
                {
                    string dir = Path.Combine(_settings.GameDirectory, "content", "vehicles");
                    if (Directory.Exists(dir)) return "已安装";
                }
                catch { }
                return "";
            }
        }

        public static List<string> BuildLaunchArguments(AppSettings settings)
        {
            var args = new List<string>();

            string userPath = AppSettings.UserDataDirectory;
            if (Directory.Exists(userPath))
            {
                args.Add("-userpath");
                args.Add(userPath);
            }

            if (settings.ForceHighPerformanceGpu)
            {
                args.Add("-highperformancegpu");
            }

            if (settings.LaunchFullScreen)
            {
                args.Add("-fullscreen");
            }

            if (settings.SkipLaunchMenu)
            {
                args.Add("-noninteractive");
            }

            string gfx = NormalizeGraphicsBackend(settings.GraphicsBackend);
            if (gfx.Length > 0)
            {
                args.Add("-gfx");
                args.Add(gfx);
            }

            if (settings.PhysicsFps > 0)
            {
                args.Add("-physicsfps");
                args.Add(settings.PhysicsFps.ToString());
            }

            AddUserArguments(args, settings.ExtraLaunchArgs);
            AddUserArguments(args, settings.GameArguments);

            return args;
        }

        public static string NormalizeGraphicsBackend(string? value)
        {
            return (value ?? "").Trim().ToLowerInvariant() switch
            {
                "dx11" or "d3d11" or "directx11" => "dx11",
                "d3d12" or "dx12" or "directx12" => "d3d12",
                "vk" or "vulkan" => "vk",
                _ => "",
            };
        }

        internal static void AddUserArguments(List<string> args, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var token in SplitArguments(raw))
            {
                if (token.Length > 0) args.Add(token);
            }
        }

        internal static List<string> SplitArguments(string raw)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            foreach (char c in raw)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) result.Add(current.ToString());
            return result;
        }

        public string? Launch(bool withMod)
        {
            if (withMod)
            {
                var installer = new ModInstaller(_settings);
                string? err = installer.Install();
                if (err != null) return "安装联机模组失败：" + err;
            }

            string exe = ExecutablePath;
            if (exe.Length == 0) return "找不到 BeamNG.drive.exe，请先在「全局设置 → 通用」里指定游戏目录。";

            // 同一目录已经在跑就不再启动。
            // 2026-10-02 实测：连点「启动游戏」会各自 new 一个 GameLauncher（IsRunning 各算各的），
            // 于是同一个 userpath 上起了两份 BeamNG —— 后起那份 8 秒后崩在 0xC0000409。
            string gameDir = _settings.GameDirectory ?? string.Empty;
            lock (RunningGate)
            {
                if (RunningDirectories.Contains(gameDir))
                {
                    EmitLog("同一目录的游戏已在运行，本次启动被忽略（重复启动会让 BeamNG 崩溃）");
                    return "游戏已经在运行了，请先结束当前游戏。";
                }
                RunningDirectories.Add(gameDir);
            }

            RunPreLaunchCommand();
            EnsureSteamRunning();

            // 明确留痕：游戏一律由启动器直接运行 exe，不经 Steam 启动（Steam 只负责后台计时）
            EmitLog("启动方式：直接运行 exe，不经 Steam 启动流程；已注入 SteamAppId="
                + BeamNgSteamAppId + "，Steam 照常记账");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = _settings.GameDirectory,
                    UseShellExecute = false,
                };

                foreach (var a in BuildLaunchArguments(_settings))
                {
                    psi.ArgumentList.Add(a);
                }

                // Steam 记账的命门。Steam 启动游戏时会注入 SteamAppId / SteamGameId，
                // BeamNG 的 steam_api64.dll 靠它才能 SteamAPI_Init 成功；而游戏目录里没有
                // steam_appid.txt，手工直起必然失败（游戏日志只写 "Steam not in use"），
                // Steam 于是完全不知道它在跑。2026-10-02 实测对照：
                //   不注入 → 游戏日志 Steam not in use；Steam 三处账本
                //            （logs/gameprocess_log.txt、appmanifest_284160.acf、
                //             userdata/<id>/config/localconfig.vdf）全无记录，时长不涨。
                //   注入后 → 游戏日志 "Steam App ID: 284160 / Initialization done /
                //            Using online service provider: Steam"，
                //            Steam 立刻跟踪进程并记账（playtime 5425→5426，last_played 当场刷新）。
                psi.Environment["SteamAppId"] = BeamNgSteamAppId;
                psi.Environment["SteamGameId"] = BeamNgSteamAppId;

                _process = Process.Start(psi);
                if (_process == null)
                {
                    lock (RunningGate)
                    {
                        RunningDirectories.Remove(gameDir);
                    }
                }
                else
                {
                    ApplyMemoryLimit();
                    _process.EnableRaisingEvents = true;
                    _process.Exited += (_, _) =>
                    {
                        EmitLog("游戏已退出");
                        lock (RunningGate)
                        {
                            RunningDirectories.Remove(gameDir);
                        }
                        ReleaseMemoryLimit();
                        RunPostExitCommand();
                        RunningChanged?.Invoke(false);
                        AnyGameExited?.Invoke();
                    };
                }

                EmitLog("已启动 BeamNG.drive");
                RunningChanged?.Invoke(true);
                return null;
            }
            catch (Exception ex)
            {
                lock (RunningGate)
                {
                    RunningDirectories.Remove(gameDir);
                }
                EmitLog("启动游戏失败：" + ex.Message);
                return ex.Message;
            }
        }

        /// <summary>
        /// 启动游戏前确保 Steam 在运行。
        /// 为什么值得做：Steam 只记录「它自己在运行时」启动的 BeamNG 时长。
        /// 从启动器直接起 exe 时若 Steam 没开，这一局就不会出现在 Steam 上，
        /// 「启动器时长」与「Steam 时长」也就永远对不上。
        /// </summary>
        private void EnsureSteamRunning()
        {
            try
            {
                if (!_settings.EnsureSteamBeforeLaunch)
                {
                    EmitLog("已关闭「启动前确保 Steam 在运行」，本次游玩不保证计入 Steam 时长");
                    return;
                }
                if (SteamLoginClient.IsSteamRunning())
                {
                    EmitLog("Steam 已在运行，本次游玩会计入 Steam 时长");
                    return;
                }
                if (SteamLoginClient.EnsureSteamRunning(30000, m => EmitLog(m)))
                {
                    EmitLog("Steam 已就绪，本次游玩会计入 Steam 时长");
                }
                else
                {
                    EmitLog("Steam 未就绪：本次游玩不会计入 Steam 时长（游戏仍可离线启动）");
                }
            }
            catch (Exception ex)
            {
                EmitLog("确认 Steam 状态时异常：" + ex.Message);
            }
        }

        private void RunPreLaunchCommand()
        {
            string cmd = _settings.PreLaunchCommand;
            if (string.IsNullOrWhiteSpace(cmd)) return;

            try
            {
                if (_settings.WaitForPreLaunchCommand)
                {
                    var r = CommandRunner.Run(cmd);
                    EmitLog("启动前命令" + r.Summary + (r.Error.Length > 0 ? "：" + r.Error : ""));
                }
                else
                {
                    _ = CommandRunner.RunAsync(cmd, wait: false);
                    EmitLog("已拉起启动前命令（不等待）");
                }
            }
            catch (Exception ex)
            {
                EmitLog("启动前命令异常：" + ex.Message);
            }
        }

        private void RunPostExitCommand()
        {
            string cmd = _settings.PostExitCommand;
            if (string.IsNullOrWhiteSpace(cmd)) return;

            try
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var r = CommandRunner.Run(cmd);
                        EmitLog("退出后命令" + r.Summary);
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                EmitLog("退出后命令异常：" + ex.Message);
            }
        }

        private void ApplyMemoryLimit()
        {
            try
            {
                ReleaseMemoryLimit();
                if (_process == null) return;
                if (!_settings.LimitGameMemory)
                {
                    EmitLog("已关闭游戏内存限制，按系统默认分配");
                    return;
                }

                int mb = _settings.MaxMemoryMB;
                if (mb <= 0) return;

                _memoryLimit = JobMemoryLimiter.Apply(_process, mb);
                AppliedMemoryLimitMb = _memoryLimit != null ? mb : 0;
                EmitLog(_memoryLimit != null
                    ? $"已对游戏进程施加 {mb} MB 内存上限（Windows 作业对象）"
                    : $"内存上限施加失败，本次不限制内存（{mb} MB）");
            }
            catch (Exception ex)
            {
                AppliedMemoryLimitMb = 0;
                EmitLog("内存上限施加异常：" + ex.Message);
            }
        }

        private void ReleaseMemoryLimit()
        {
            try
            {
                _memoryLimit?.Dispose();
            }
            catch { }
            _memoryLimit = null;
        }

        public void Focus()
        {
            try
            {
                if (_process is { HasExited: false })
                {
                    _process.Refresh();
                    if (_process.MainWindowHandle != IntPtr.Zero)
                        NativeMethods.SetForegroundWindow(_process.MainWindowHandle);
                }
            }
            catch { }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
    }
}

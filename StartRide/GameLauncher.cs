using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>启动 BeamNG.drive 并跟踪进程状态。</summary>
    public sealed class GameLauncher
    {
        private readonly AppSettings _settings;
        private Process? _process;
        private JobMemoryLimiter.AppliedLimit? _memoryLimit;

        public GameLauncher(AppSettings settings) => _settings = settings;

        public event Action<string>? Log;
        public event Action<bool>? RunningChanged;

        public bool IsRunning => _process is { HasExited: false };
        public int? ProcessId => IsRunning ? _process?.Id : null;

        /// <summary>本次运行实际施加成功的内存上限（MB）；0 表示没限制。</summary>
        public int AppliedMemoryLimitMb { get; private set; }

        /// <summary>游戏可执行文件路径；找不到返回空串。</summary>
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

        /// <summary>游戏版本号（依次从 version.txt / 启动日志 / 用户设置里读，读不到返回"已安装"）。</summary>
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

                // 游戏自己的启动日志第一行就写着版本，例如：Log started - v 0.39.4.0 - x86 - build 20972
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

        /// <summary>
        /// 组装真正传给 BeamNG.drive.exe 的参数。
        /// 抽成静态方法是为了能单独验证（探针直接调它断言参数内容），
        /// 也避免以后改启动流程时把某个开关漏掉。
        /// </summary>
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
                // 让游戏走独显，避免笔记本上跑核显掉帧
                args.Add("-highperformancegpu");
            }

            if (settings.LaunchFullScreen)
            {
                args.Add("-fullscreen");
            }

            if (settings.SkipLaunchMenu)
            {
                // 跳过游戏启动菜单，直接进驾驶界面
                args.Add("-noninteractive");
            }

            // 渲染后端：只认游戏真实支持的取值，别把用户乱填的东西塞进去
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

        /// <summary>渲染后端取值归一化：只放行游戏文档里支持的三个值。</summary>
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

        /// <summary>
        /// 把用户填的一行参数拆开追加。支持带引号的路径（"C:\a b\x.dll"）。
        /// </summary>
        internal static void AddUserArguments(List<string> args, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var token in SplitArguments(raw))
            {
                if (token.Length > 0) args.Add(token);
            }
        }

        /// <summary>按空格拆参数，但保留引号内的空格。</summary>
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

        /// <summary>
        /// 启动游戏。返回 null 表示成功。
        /// 会带上 -userpath 指向真实用户目录，保证 mods 与启动器安装位置一致。
        /// </summary>
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

            // 启动前命令：以前只在设置页里能填、没人执行；现在真的跑
            RunPreLaunchCommand();

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

                _process = Process.Start(psi);
                if (_process != null)
                {
                    // 内存上限必须在进程还活着时立刻施加
                    ApplyMemoryLimit();
                    _process.EnableRaisingEvents = true;
                    _process.Exited += (_, _) =>
                    {
                        Log?.Invoke("游戏已退出");
                        ReleaseMemoryLimit();
                        RunPostExitCommand();
                        RunningChanged?.Invoke(false);
                    };
                }

                Log?.Invoke("已启动 BeamNG.drive");
                RunningChanged?.Invoke(true);
                return null;
            }
            catch (Exception ex)
            {
                Log?.Invoke("启动游戏失败：" + ex.Message);
                return ex.Message;
            }
        }

        /// <summary>启动前命令：按设置决定是等它跑完还是并行拉起。</summary>
        private void RunPreLaunchCommand()
        {
            string cmd = _settings.PreLaunchCommand;
            if (string.IsNullOrWhiteSpace(cmd)) return;

            try
            {
                if (_settings.WaitForPreLaunchCommand)
                {
                    var r = CommandRunner.Run(cmd);
                    Log?.Invoke("启动前命令" + r.Summary + (r.Error.Length > 0 ? "：" + r.Error : ""));
                }
                else
                {
                    _ = CommandRunner.RunAsync(cmd, wait: false);
                    Log?.Invoke("已拉起启动前命令（不等待）");
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("启动前命令异常：" + ex.Message);
            }
        }

        /// <summary>游戏退出后命令：收尾动作，异常不影响界面。</summary>
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
                        Log?.Invoke("退出后命令" + r.Summary);
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                Log?.Invoke("退出后命令异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 把「内存分配」真正作用到游戏进程上（Windows 作业对象硬上限）。
        /// 服务端没有内存参数可用，只在设置里记数字等于没生效，所以走系统层限制。
        /// </summary>
        private void ApplyMemoryLimit()
        {
            try
            {
                ReleaseMemoryLimit();
                if (_process == null) return;
                if (!_settings.LimitGameMemory)
                {
                    Log?.Invoke("已关闭游戏内存限制，按系统默认分配");
                    return;
                }

                int mb = _settings.MaxMemoryMB;
                if (mb <= 0) return;

                _memoryLimit = JobMemoryLimiter.Apply(_process, mb);
                AppliedMemoryLimitMb = _memoryLimit != null ? mb : 0;
                Log?.Invoke(_memoryLimit != null
                    ? $"已对游戏进程施加 {mb} MB 内存上限（Windows 作业对象）"
                    : $"内存上限施加失败，本次不限制内存（{mb} MB）");
            }
            catch (Exception ex)
            {
                AppliedMemoryLimitMb = 0;
                Log?.Invoke("内存上限施加异常：" + ex.Message);
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

        /// <summary>把游戏窗口拉到前台。</summary>
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

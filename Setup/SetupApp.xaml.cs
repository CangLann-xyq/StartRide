using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace StartRide.Setup;

/// <summary>
/// 安装包入口。一个可执行文件承担两种角色：
/// <list type="bullet">
/// <item>默认：安装向导 <see cref="InstallerWindow"/>；</item>
/// <item><c>--uninstall</c> 或文件名里带 "uninstall"：卸载器 <see cref="UninstallWindow"/>。</item>
/// </list>
/// 卸载器单独有一个 <c>UninstallerOnly</c> 编译产物（不含 payload，体积很小），
/// 装到安装目录里的就是它；但完整安装包也支持 <c>--uninstall</c>，
/// 万一卸载器被误删还能用原始安装包卸载。
///
/// 加 <c>--quiet</c> 则完全静默（无窗口），退出码 0/1 —— 见 <see cref="CommandLine"/>。
/// </summary>
public partial class SetupApp : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CommandLine cl = CommandLine.Parse(e.Args);
        Action<string>? log = cl.CreateLogger();

        if (cl.SilentInstall) { RunSilentInstall(cl, log); return; }
        if (cl.SilentUninstall) { RunSilentUninstall(cl, log); return; }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "安装程序遇到未处理的错误：\n\n" + args.Exception.Message,
                ProductInfo.ProductName + " 安装程序", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        if (WantsUninstall(e.Args))
        {
            new UninstallWindow().Show();
            return;
        }

#if UNINSTALLER_ONLY
        // 卸载器产物没有安装向导的 XAML，直接进卸载。
        new UninstallWindow().Show();
#else
        new InstallerWindow().Show();
#endif
    }

    private void RunSilentInstall(CommandLine cl, Action<string>? log)
    {
        int code = 0;
        try
        {
            var options = new InstallOptions
            {
                TargetDir = string.IsNullOrWhiteSpace(cl.Dir) ? ProductInfo.DefaultInstallDir : cl.Dir,
                DesktopShortcut = !cl.NoDesktopShortcut,
                StartMenuShortcut = !cl.NoStartMenuShortcut,
                LaunchAfterwards = false,
            };

            log?.Invoke($"开始静默安装 版本={ProductInfo.Version} 目录={options.TargetDir}");
            // ⚠️ 必须整段丢到线程池上跑。静默模式没有消息循环，主线程在这里等结果；
            // 而 InstallEngine 里是 async 方法，若在当前线程直接 await，续体会被
            // 投回 DispatcherSynchronizationContext —— 主线程正阻塞着等它 → 死锁。
            Task.Run(() =>
            {
                // ⚠️ progress 必须**在这个线程池线程里**构造，不能提到主线程上构造。
                // Progress<T> 会捕获创建它的 SynchronizationContext：在主线程（Dispatcher
                // 上下文）构造的话，每次 Report 都被 Post 回 UI 队列，而静默模式下主线程
                // 正阻塞在 GetResult() —— 那些回调永远轮不到执行，--log 里就只剩首尾两行，
                // 中间的进度全丢。放这儿构造，上下文为 null，回调直接跑在线程池上。
                var progress = new Progress<InstallProgress>(
                    p => log?.Invoke($"  {p.Percent:0}%  {p.Stage}  {p.Message}"));

                // 快捷方式成功/失败单独报一行 —— 用户反馈"桌面没图标"时，看日志就能定位。
                return InstallEngine.RunAsync(options, progress, CancellationToken.None,
                    m => log?.Invoke("  " + m));
            }).GetAwaiter().GetResult();
            log?.Invoke("安装成功");
            Console.Out.WriteLine("StartRide 安装成功: " + options.TargetDir);
        }
        catch (Exception ex)
        {
            code = 1;
            log?.Invoke("安装失败: " + ex);
            Console.Error.WriteLine("安装失败: " + ex.Message);
        }
        Shutdown(code);
    }

    private void RunSilentUninstall(CommandLine cl, Action<string>? log)
    {
        int code = 0;
        try
        {
            string? dir = string.IsNullOrWhiteSpace(cl.Dir) ? UninstallEngine.ResolveInstallDir() : cl.Dir;
            if (string.IsNullOrEmpty(dir))
            {
                log?.Invoke("没有找到已安装的 StartRide");
                Console.Out.WriteLine("没有找到已安装的 StartRide");
            }
            else
            {
                log?.Invoke($"开始静默卸载 目录={dir} 删除数据={cl.PurgeData}");
                UninstallEngine.Run(dir, cl.PurgeData, m => log?.Invoke("  " + m));
                log?.Invoke("卸载成功");
                Console.Out.WriteLine("StartRide 卸载成功: " + dir);
            }
        }
        catch (Exception ex)
        {
            code = 1;
            log?.Invoke("卸载失败: " + ex);
            Console.Error.WriteLine("卸载失败: " + ex.Message);
        }
        Shutdown(code);
    }

    private static bool WantsUninstall(string[] args)
        => args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)
                      || a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace StartRide.Setup;

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

                ClearBeforeInstall = !cl.KeepOldFiles,
            };

            log?.Invoke($"开始静默安装 版本={ProductInfo.Version} 目录={options.TargetDir}");

            Task.Run(() =>
            {

                var progress = new Progress<InstallProgress>(
                    p => log?.Invoke($"  {p.Percent:0}%  {p.Stage}  {p.Message}"));

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

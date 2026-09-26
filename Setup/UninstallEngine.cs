using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace StartRide.Setup;

/// <summary>
/// 卸载流程。
/// 用户级安装，卸干净只需要三件事：快捷方式、注册表卸载项、安装目录。
/// 用户数据（<c>%APPDATA%\StartRide</c>，装着账户、设置、Mods 缓存）默认**保留**，
/// 只有用户明确勾选才删 —— 卸载器最不该干的事就是顺手把人家账号删了。
/// </summary>
internal static class UninstallEngine
{
    /// <summary>要卸哪个目录：优先注册表登记的，其次看自己是不是就住在这个目录里。</summary>
    public static string? ResolveInstallDir()
    {
        string? registered = ProductInfo.FindInstalledDir();
        if (!string.IsNullOrEmpty(registered)) return registered;

        string? self = SelfPath();
        if (self != null)
        {
            string? dir = Path.GetDirectoryName(self);
            if (!string.IsNullOrEmpty(dir) && File.Exists(ProductInfo.ExePathIn(dir))) return dir;
        }
        return null;
    }

    public static string? SelfPath()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    public static void Run(string installDir, bool removeUserData, Action<string> report)
    {
        report("正在删除快捷方式…");
        ShortcutFactory.TryDelete(ShortcutFactory.DesktopLink);
        ShortcutFactory.TryDelete(ShortcutFactory.StartMenuLink);
        TryDeleteStartMenuFolder();

        report("正在注销卸载信息…");
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(
                ProductInfo.UninstallRegistryKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // 注册表项删不掉不该阻断卸载
        }

        if (removeUserData)
        {
            report("正在删除配置与账户数据…");
            try
            {
                if (Directory.Exists(ProductInfo.UserDataDir))
                {
                    InstallEngine.SafeDeleteDirectory(ProductInfo.UserDataDir);
                }
            }
            catch
            {
                // 数据可能被占用，能删多少算多少
            }
        }

        report("正在删除程序文件…");
        string? self = SelfPath();
        bool selfInside = self != null && IsInside(self, installDir);

        if (selfInside)
        {
            // 运行中的 exe 自己是删不掉的，所以先把别的清掉，
            // 再交给一个分离出去的 cmd：等本进程退出后删掉卸载器本身、顺带收掉空目录。
            DeleteAllExcept(installDir, self!);
            ScheduleSelfDelete(self!, installDir);
        }
        else
        {
            InstallEngine.SafeDeleteDirectory(installDir);
        }
    }

    private static bool IsInside(string path, string dir)
    {
        string a = Path.GetFullPath(path);
        string b = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return a.StartsWith(b, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteAllExcept(string dir, string keepFile)
    {
        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(file, keepFile, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch
            {
                // 被占用的就留下，cmd 那步会再试一次
            }
        }

        // 自底向上删空目录
        var dirs = new System.Collections.Generic.List<string>(
            Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories));
        dirs.Sort((x, y) => y.Length.CompareTo(x.Length));
        foreach (string d in dirs)
        {
            try { Directory.Delete(d, false); } catch { }
        }
    }

    /// <summary>
    /// 让一个分离的 cmd 在本进程退出后收尾。
    /// 用 <c>ping</c> 而不是 <c>timeout</c>：timeout 依赖控制台，脱离控制台会直接报错退出。
    /// <c>/s /q</c> 一起上，是为了把刚才被占用没删掉的文件也一并收走。
    /// </summary>
    private static void ScheduleSelfDelete(string selfPath, string installDir)
    {
        string script = "ping -n 3 127.0.0.1 >nul"
                      + " & del /f /q \"" + selfPath + "\""
                      + " & rmdir /s /q \"" + installDir + "\"";

        var psi = new ProcessStartInfo("cmd.exe", "/c " + script)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            Process.Start(psi);
        }
        catch
        {
            MessageBox.Show(
                "程序文件已删除，但卸载程序自身没能自动清理。\n可以手动删除目录：\n" + installDir,
                "StartRide 卸载", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static void TryDeleteStartMenuFolder()
    {
        try
        {
            if (Directory.Exists(ProductInfo.StartMenuDir)
                && Directory.GetFileSystemEntries(ProductInfo.StartMenuDir).Length == 0)
            {
                Directory.Delete(ProductInfo.StartMenuDir, false);
            }
        }
        catch
        {
            // 空文件夹留着也无所谓
        }
    }
}

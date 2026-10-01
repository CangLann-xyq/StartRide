using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace StartRide.Setup;

internal static class UninstallEngine
{
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

        bool hadDesktop = true;
        bool hadStartMenu = true;
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ProductInfo.UninstallRegistryKey);
            hadDesktop = ReadFlag(key, "ShortcutDesktop");
            hadStartMenu = ReadFlag(key, "ShortcutStartMenu");
        }
        catch
        {
        }

        report("正在删除快捷方式…");
        if (hadDesktop) ShortcutFactory.TryDelete(ShortcutFactory.DesktopLink);
        if (hadStartMenu)
        {
            ShortcutFactory.TryDelete(ShortcutFactory.StartMenuLink);
            TryDeleteStartMenuFolder();
        }

        report("正在注销卸载信息…");
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(
                ProductInfo.UninstallRegistryKey, throwOnMissingSubKey: false);
        }
        catch
        {
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
            }
        }

        report("正在删除程序文件…");
        string? self = SelfPath();
        bool selfInside = self != null && IsInside(self, installDir);

        if (selfInside)
        {

            DeleteAllExcept(installDir, self!);
            ScheduleSelfDelete(self!, installDir);
        }
        else
        {
            InstallEngine.SafeDeleteDirectory(installDir);
        }
    }

    private static bool ReadFlag(RegistryKey? key, string name)
    {
        try
        {
            object? v = key?.GetValue(name);
            if (v == null) return true;
            return Convert.ToInt32(v) != 0;
        }
        catch
        {
            return true;
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
            }
        }

        var dirs = new System.Collections.Generic.List<string>(
            Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories));
        dirs.Sort((x, y) => y.Length.CompareTo(x.Length));
        foreach (string d in dirs)
        {
            try { Directory.Delete(d, false); } catch { }
        }
    }

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
        }
    }
}

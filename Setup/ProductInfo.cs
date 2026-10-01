using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace StartRide.Setup;

internal static class ProductInfo
{
    public const string ProductName = "StartRide";
    public const string AppExeName = "StartRide.exe";
    public const string UninstallerName = "StartRide-Uninstall.exe";
    public const string Developer = "肖又祺";
    public const string Publisher = Developer;
    public const string Tagline = "BeamNG.drive 多人联机启动器";

    public const string UninstallRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\StartRide";

    public const string StartMenuFolder = "StartRide";

    public const string ShortcutName = "StartRide 启动器";

    public static string Version
    {
        get
        {
            Version? v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
        }
    }

    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "StartRide");

    public static string UserDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartRide");

    public static string StartMenuDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), StartMenuFolder);

    public static string DesktopDir
    {
        get
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) return dir;

            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (!string.IsNullOrWhiteSpace(common) && Directory.Exists(common)) return common;

            return dir;
        }
    }

    public static string ExePathIn(string installDir) => Path.Combine(installDir, AppExeName);

    public static string UninstallerPathIn(string installDir) =>
        Path.Combine(installDir, UninstallerName);

    public static string? FindInstalledDir()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(UninstallRegistryKey);
            if (key?.GetValue("InstallLocation") is string dir && dir.Length > 0
                && File.Exists(ExePathIn(dir)))
            {
                return dir;
            }
        }
        catch
        {
        }
        return null;
    }

    public static string? InstalledVersion()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(UninstallRegistryKey);
            return key?.GetValue("DisplayVersion") as string;
        }
        catch
        {
            return null;
        }
    }
}

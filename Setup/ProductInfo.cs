using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace StartRide.Setup;

/// <summary>安装包里所有"写死的名字/路径"的唯一出处。改品牌只改这一个文件。</summary>
internal static class ProductInfo
{
    public const string ProductName = "StartRide";
    public const string AppExeName = "StartRide.exe";
    public const string UninstallerName = "StartRide-Uninstall.exe";
    public const string Publisher = "StartRide";
    public const string Tagline = "BeamNG.drive 多人联机启动器";

    /// <summary>卸载列表（控制面板 → 应用和功能）里的注册表项。用户级安装，不需要管理员权限。</summary>
    public const string UninstallRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\StartRide";

    /// <summary>开始菜单里的文件夹名与快捷方式名。</summary>
    public const string StartMenuFolder = "StartRide";

    /// <summary>
    /// 快捷方式的名字。带「启动器」后缀是有原因的：本机桌面早就有一个
    /// <c>StartRide 启动器.lnk</c>（早期手写的），这里沿用同名直接覆盖，
    /// 免得装完桌面上并排躺着两个指向同一个 exe 的图标。
    /// </summary>
    public const string ShortcutName = "StartRide 启动器";

    public static string Version
    {
        get
        {
            Version? v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
        }
    }

    /// <summary>默认装到用户目录下，全程不需要管理员权限 —— 也就不会有 UAC 弹窗打断安装。</summary>
    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "StartRide");

    /// <summary>启动器的配置/账户/存档目录（数据目录归一后的位置）。卸载时是否清理由用户勾选。</summary>
    public static string UserDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartRide");

    public static string StartMenuDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), StartMenuFolder);

    public static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string ExePathIn(string installDir) => Path.Combine(installDir, AppExeName);

    /// <summary>卸载器单独放一层子目录，免得启动器主目录里混进一组不相干的 exe/runtimeconfig。</summary>
    public static string UninstallerDirIn(string installDir) => Path.Combine(installDir, "Uninstall");

    public static string UninstallerPathIn(string installDir) =>
        Path.Combine(UninstallerDirIn(installDir), UninstallerName);

    /// <summary>上一次装到哪了（读卸载表）。没有装过或目录已被删就返回 null。</summary>
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
            // 注册表读不到就当没装过，安装流程本身不依赖它
        }
        return null;
    }

    /// <summary>机器上还留着旧版本的痕迹吗（用于"升级"还是"安装"的文案）。</summary>
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

using System;
using System.IO;

namespace StartRide.Setup;

/// <summary>
/// 安装包的命令行参数。
///
/// 为什么一个"双击即用"的安装包还要有命令行：
/// 1. 静默安装/卸载是回归验证的唯一可行手段 —— GUI 点不完，脚本才跑得动全套；
/// 2. 批量部署、以及"卸载器被误删后用安装包收尾"都需要它。
/// </summary>
internal sealed class CommandLine
{
    /// <summary>静默安装：不显示任何窗口，装完退出。退出码 0 = 成功。</summary>
    public bool SilentInstall { get; private set; }

    /// <summary>静默卸载。</summary>
    public bool SilentUninstall { get; private set; }

    /// <summary>卸载时是否一并删除 %APPDATA%\StartRide。</summary>
    public bool PurgeData { get; private set; }

    public string? Dir { get; private set; }
    public string? LogPath { get; private set; }

    public bool NoDesktopShortcut { get; private set; }
    public bool NoStartMenuShortcut { get; private set; }

    public static CommandLine Parse(string[] args)
    {
        var cl = new CommandLine();
        bool uninstallRole = false;
        bool quiet = false;

        foreach (string raw in args)
        {
            string a = raw.Trim();
            if (a.Length == 0) continue;

            if (IsFlag(a, "--uninstall")) uninstallRole = true;
            else if (IsFlag(a, "--quiet") || IsFlag(a, "--silent")) quiet = true;
            else if (IsFlag(a, "--purge-data")) cl.PurgeData = true;
            else if (IsFlag(a, "--no-desktop")) cl.NoDesktopShortcut = true;
            else if (IsFlag(a, "--no-startmenu")) cl.NoStartMenuShortcut = true;
            else if (TryValue(a, "--dir", out string dir)) cl.Dir = dir;
            else if (TryValue(a, "--log", out string log)) cl.LogPath = log;
        }

        // 卸载器的文件名本身就代表"卸载"（它被单独发布时不会带参数）。
        if (!uninstallRole && LooksLikeUninstaller()) uninstallRole = true;

        if (uninstallRole) cl.SilentUninstall = quiet;
        else cl.SilentInstall = quiet;
        return cl;
    }

    private static bool IsFlag(string arg, string name) =>
        arg.Equals(name, StringComparison.OrdinalIgnoreCase)
        || arg.Equals(name.Replace("--", "/"), StringComparison.OrdinalIgnoreCase);

    private static bool TryValue(string arg, string name, out string value)
    {
        value = "";
        foreach (string prefix in new[] { name + "=", name + ":" })
        {
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(prefix.Length).Trim().Trim('"');
                return true;
            }
        }
        return false;
    }

    private static bool LooksLikeUninstaller()
    {
        try
        {
            string name = Path.GetFileNameWithoutExtension(
                System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
            return name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>静默模式下的进度输出。没传 --log 就什么都不写。</summary>
    public Action<string>? CreateLogger()
    {
        if (string.IsNullOrEmpty(LogPath)) return null;
        string path = LogPath!;
        return message =>
        {
            try
            {
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss ") + message + Environment.NewLine);
            }
            catch
            {
                // 日志写不了不影响安装
            }
        };
    }
}

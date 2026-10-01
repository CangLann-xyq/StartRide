using System;
using System.IO;

namespace StartRide.Setup;

internal sealed class CommandLine
{
    public bool SilentInstall { get; private set; }

    public bool SilentUninstall { get; private set; }

    public bool PurgeData { get; private set; }

    public string? Dir { get; private set; }
    public string? LogPath { get; private set; }

    public bool NoDesktopShortcut { get; private set; }
    public bool NoStartMenuShortcut { get; private set; }

    public bool KeepOldFiles { get; private set; }

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
            else if (IsFlag(a, "--keep-old")) cl.KeepOldFiles = true;
            else if (TryValue(a, "--dir", out string dir)) cl.Dir = dir;
            else if (TryValue(a, "--log", out string log)) cl.LogPath = log;
        }

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
            }
        };
    }
}

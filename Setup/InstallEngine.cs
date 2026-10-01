using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace StartRide.Setup;

internal enum InstallStage
{
    Preparing,
    Unpacking,
    Cleaning,
    Copying,
    Shortcuts,
    Registering,
    Finished,
}

internal readonly struct InstallProgress
{
    public InstallProgress(InstallStage stage, double percent, string message)
    {
        Stage = stage;
        Percent = percent;
        Message = message;
    }

    public InstallStage Stage { get; }
    public double Percent { get; }
    public string Message { get; }
}

internal sealed class InstallOptions
{
    public string TargetDir { get; set; } = ProductInfo.DefaultInstallDir;
    public bool DesktopShortcut { get; set; } = true;
    public bool StartMenuShortcut { get; set; } = true;
    public bool LaunchAfterwards { get; set; } = true;

    public bool ClearBeforeInstall { get; set; }
}

internal static class InstallEngine
{

    public static async Task RunAsync(InstallOptions options, IProgress<InstallProgress> progress,
                                      CancellationToken ct, Action<string>? note = null)
    {
        string target = options.TargetDir;
        string staging = Path.Combine(Path.GetTempPath(), "StartRide-setup-" + Environment.ProcessId);

        try
        {
            progress.Report(new InstallProgress(InstallStage.Preparing, 1, "正在准备安装…"));

            EnsureNotRunning();
            if (Directory.Exists(staging)) SafeDeleteDirectory(staging);
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(target);

            progress.Report(new InstallProgress(InstallStage.Unpacking, 4, "正在解包应用文件…"));
            long totalBytes = await Task.Run(() => ExtractPackage(staging, progress, ct), ct);

            if (options.ClearBeforeInstall && LooksLikeInstallDirectory(target))
            {
                progress.Report(new InstallProgress(InstallStage.Cleaning, 67, "正在清理旧版本文件…"));
                int removed = await Task.Run(() => ClearForOverwrite(target), ct);
                note?.Invoke($"已清理旧版本程序文件 {removed} 项（已保留 Mods 等数据目录）");
            }

            List<string> files = await Task.Run(() => EnumerateFiles(staging), ct);
            progress.Report(new InstallProgress(InstallStage.Copying, 70, "正在写入安装目录…"));

            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                string rel = Path.GetRelativePath(staging, files[i]);
                string dst = Path.Combine(target, rel);
                string? dir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.Copy(files[i], dst, overwrite: true);

                if ((i & 7) == 0 || i == files.Count - 1)
                {
                    double pct = 70.0 + 20.0 * (i + 1) / Math.Max(files.Count, 1);
                    progress.Report(new InstallProgress(InstallStage.Copying, pct, rel));
                }
            }

            progress.Report(new InstallProgress(InstallStage.Copying, 92, "正在放置卸载程序…"));
            string? uninstaller = await Task.Run(() => Payload.TryExtractUninstaller(target), ct);

            progress.Report(new InstallProgress(InstallStage.Shortcuts, 94, "正在创建快捷方式…"));
            await Task.Run(() =>
            {
                string exe = ProductInfo.ExePathIn(target);

                if (options.DesktopShortcut)
                {
                    string link = ShortcutFactory.DesktopLink;
                    bool made = ShortcutFactory.TryCreate(link, exe, target,
                        ProductInfo.ProductName + " — " + ProductInfo.Tagline, exe);
                    note?.Invoke(made ? "桌面快捷方式：" + link
                                      : "⚠ 桌面快捷方式没能创建：" + link +
                                        "（可手动把 " + exe + " 的快捷方式拖到桌面）");
                }

                if (options.StartMenuShortcut)
                {
                    string link = ShortcutFactory.StartMenuLink;
                    Directory.CreateDirectory(ProductInfo.StartMenuDir);
                    bool made = ShortcutFactory.TryCreate(link, exe, target,
                        ProductInfo.ProductName + " — " + ProductInfo.Tagline, exe);
                    note?.Invoke(made ? "开始菜单快捷方式：" + link
                                      : "⚠ 开始菜单快捷方式没能创建：" + link);
                }
            }, ct);

            progress.Report(new InstallProgress(InstallStage.Registering, 97, "正在登记卸载信息…"));
            await Task.Run(() => WriteUninstallRegistry(target, uninstaller, options), ct);

            progress.Report(new InstallProgress(InstallStage.Finished, 100, "安装完成"));
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging)) SafeDeleteDirectory(staging);
            }
            catch
            {
            }
        }
    }

    private static long ExtractPackage(string staging, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        using Stream raw = Payload.OpenAppPackage();
        using var zip = new ZipArchive(raw, ZipArchiveMode.Read);

        int total = zip.Entries.Count;
        long bytes = 0;
        int done = 0;
        int rootLength = DetectRootPrefix(zip);

        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            done++;

            string name = entry.FullName.Replace('\\', '/');
            if (rootLength > 0 && name.Length > rootLength) name = name.Substring(rootLength);
            name = name.TrimStart('/');
            if (name.Length == 0 || name.EndsWith("/", StringComparison.Ordinal)) continue;

            string dst = Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar));
            string? dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            entry.ExtractToFile(dst, overwrite: true);
            bytes += entry.Length;

            if ((done & 15) == 0)
            {
                progress.Report(new InstallProgress(InstallStage.Unpacking,
                    4.0 + 60.0 * done / Math.Max(total, 1), name));
            }
        }

        progress.Report(new InstallProgress(InstallStage.Unpacking, 66, "解包完成"));
        return bytes;
    }

    private static int DetectRootPrefix(ZipArchive zip)
    {
        string? first = null;
        foreach (ZipArchiveEntry e in zip.Entries)
        {
            string n = e.FullName.Replace('\\', '/');
            if (n.Length == 0) continue;
            first = n;
            break;
        }
        if (first == null) return 0;

        int slash = first.IndexOf('/');
        if (slash <= 0) return 0;
        string root = first.Substring(0, slash) + "/";

        foreach (ZipArchiveEntry e in zip.Entries)
        {
            string n = e.FullName.Replace('\\', '/');
            if (n.Length == 0) continue;
            if (!n.StartsWith(root, StringComparison.Ordinal)) return 0;
        }
        return root.Length;
    }

    private static List<string> EnumerateFiles(string dir)
    {
        var list = new List<string>();
        foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            list.Add(f);
        }
        return list;
    }

    private static void EnsureNotRunning()
    {
        foreach (Process p in Process.GetProcessesByName(
                     Path.GetFileNameWithoutExtension(ProductInfo.AppExeName)))
        {
            try
            {
                if (!p.HasExited)
                {
                    throw new InvalidOperationException(
                        ProductInfo.ProductName + " 正在运行。请先关闭它再安装（已有版本的程序文件被占用会写不进去）。");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    private static void WriteUninstallRegistry(string target, string? uninstallerPath,
                                               InstallOptions options)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(ProductInfo.UninstallRegistryKey)
            ?? throw new InvalidOperationException("无法写入卸载信息（注册表被拒绝访问）。");

        string exe = ProductInfo.ExePathIn(target);
        string uninstallCmd = !string.IsNullOrEmpty(uninstallerPath) && File.Exists(uninstallerPath)
            ? "\"" + uninstallerPath + "\""
            : "\"" + exe + "\" --uninstall";

        key.SetValue("DisplayName", ProductInfo.ProductName);
        key.SetValue("DisplayVersion", ProductInfo.Version);
        key.SetValue("Publisher", ProductInfo.Publisher);
        key.SetValue("DisplayIcon", exe);
        key.SetValue("InstallLocation", target);
        key.SetValue("UninstallString", uninstallCmd);
        key.SetValue("QuietUninstallString", uninstallCmd + " --quiet");
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

        key.SetValue("ShortcutDesktop", options.DesktopShortcut ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("ShortcutStartMenu", options.StartMenuShortcut ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", Math.Max(1, EstimateSizeKb(target)), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static int EstimateSizeKb(string dir)
    {
        long total = 0;
        try
        {
            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(f).Length; } catch { }
            }
        }
        catch
        {
            return 8192;
        }
        return (int)Math.Min(Math.Max(total / 1024, 1), int.MaxValue);
    }

    private static readonly string[] PreservedDirectoryNames =
    {
        "Mods", "BHL", ".minecraft", "cache", "images", "tools"
    };

    internal static bool LooksLikeInstallDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;
            if (IsUnsafeTarget(dir)) return false;
            if (File.Exists(ProductInfo.ExePathIn(dir))) return true;

            string full = Normalize(dir);
            if (full == Normalize(ProductInfo.DefaultInstallDir)) return true;
            string? registered = ProductInfo.FindInstalledDir();
            if (!string.IsNullOrEmpty(registered) && full == Normalize(registered!)) return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUnsafeTarget(string dir)
    {
        string full = Normalize(dir);
        if (full.Length == 0) return true;

        string root = Normalize(Path.GetPathRoot(full) ?? "");
        if (root.Length > 0 && full == root) return true;

        string[] guarded =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
        };
        foreach (string g in guarded)
        {
            if (g.Length > 0 && full == Normalize(g)) return true;
        }
        return false;
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path)
               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    internal static int ClearForOverwrite(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        int removed = 0;

        foreach (string file in Directory.EnumerateFiles(dir))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                removed++;
            }
            catch
            {
            }
        }

        foreach (string sub in Directory.EnumerateDirectories(dir))
        {
            string name = Path.GetFileName(sub);
            if (PreservedDirectoryNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            try
            {
                SafeDeleteDirectory(sub);
                removed++;
            }
            catch
            {
            }
        }
        return removed;
    }

    internal static void SafeDeleteDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            catch
            {
            }
        }
        Directory.Delete(dir, recursive: true);
    }
}

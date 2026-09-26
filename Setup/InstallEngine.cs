using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace StartRide.Setup;

internal enum InstallStage
{
    Preparing,
    Unpacking,
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
}

/// <summary>
/// 安装流程。
///
/// 为什么先解到临时目录再往安装目录搬，而不是直接解压到安装目录：
/// 直接解压的话，一旦中途失败（磁盘满、杀软拦、用户点取消），安装目录里
/// 就是"一半新一半旧"的状态，比"完全没装"更难收拾。临时目录 + 最后统一搬运，
/// 让"取消"这个动作真正干净。
/// </summary>
internal static class InstallEngine
{
    /// <param name="note">
    /// 给"人看"的补充信息（快捷方式落在哪、失败了没有），不走 <see cref="IProgress{T}"/>。
    /// 为什么不复用 progress：静默模式下 progress 的回调是 Post 到 UI 线程的，
    /// 而那时主线程正阻塞等着安装结束 —— 投过去的消息没人执行，--log 里就一片空白。
    /// </param>
    public static async Task RunAsync(InstallOptions options, IProgress<InstallProgress> progress,
                                      CancellationToken ct, Action<string>? note = null)
    {
        string target = options.TargetDir;
        string staging = Path.Combine(Path.GetTempPath(), "StartRide-setup-" + Environment.ProcessId);

        try
        {
            progress.Report(new InstallProgress(InstallStage.Preparing, 1, "正在准备安装…"));

            // 注意：这里刻意不做 await Task.Yield() 之类的"让一下"。
            // 静默模式下调用方是阻塞等待的，任何把续体投回 UI 线程的 await
            // 都会变成死锁；真正的异步交给调用方用 Task.Run 包一层。
            EnsureNotRunning();
            if (Directory.Exists(staging)) SafeDeleteDirectory(staging);
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(target);

            // ── 解包 ──────────────────────────────────────────────────────
            progress.Report(new InstallProgress(InstallStage.Unpacking, 4, "正在解包应用文件…"));
            long totalBytes = await Task.Run(() => ExtractPackage(staging, progress, ct), ct);

            // ── 搬到安装目录 ──────────────────────────────────────────────
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

            // ── 卸载器 ────────────────────────────────────────────────────
            progress.Report(new InstallProgress(InstallStage.Copying, 92, "正在放置卸载程序…"));
            string? uninstaller = await Task.Run(() => Payload.TryExtractUninstaller(target), ct);

            // ── 快捷方式 ──────────────────────────────────────────────────
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

            // ── 注册卸载信息 ──────────────────────────────────────────────
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
                // 临时目录清不掉不影响安装结果
            }
        }
    }

    /// <summary>把 payload zip 解开。zip 顶层有一层 <c>StartRide-&lt;版本&gt;-win-x64\</c>，要剥掉。</summary>
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

    /// <summary>找出 zip 里那层顶层目录的长度（含结尾的 /）。没有单层顶层就返回 0。</summary>
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

        // 确认所有条目都在这一层下面，否则不剥（避免误伤）
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
                        ProductInfo.ProductName + " 正在运行。请先关闭它再安装（升级时旧文件被占用会写不进去）。");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // 查询别的会话里的进程可能抛权限异常，忽略
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
        // 有独立卸载器就用它；没有（异常情况）退回用启动器本体 --uninstall
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
        // ⚠️ 这两项是给卸载器看的"我建过哪几个快捷方式"。
        // 不记的话卸载只能两条都删 —— 用户装的时候勾掉了桌面快捷方式，
        // 卸载却照样把桌面上那个同名的 .lnk 删掉，而那个可能是他自己做的。
        key.SetValue("ShortcutDesktop", options.DesktopShortcut ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("ShortcutStartMenu", options.StartMenuShortcut ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", Math.Max(1, EstimateSizeKb(target)), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    /// <summary>给"应用和功能"里的体积数字。算不出来就给个兜底值，不能让这一项是 0。</summary>
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

    /// <summary>
    /// 递归删目录。
    /// 先清只读属性再删：安装目录是从 zip 解出来的，里面可能带只读位，
    /// 那种文件 <c>Directory.Delete(recursive: true)</c> 会直接抛。
    /// </summary>
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
                // 属性改不掉就让它去删，删不掉会抛出来
            }
        }
        Directory.Delete(dir, recursive: true);
    }
}

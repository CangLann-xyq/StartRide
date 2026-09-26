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

    /// <summary>
    /// 覆盖安装时是否先清空目标目录里的程序文件（<see cref="PreservedDirectoryNames"/> 除外）。
    ///
    /// 为什么需要它：原来的逻辑是逐个 <c>File.Copy(overwrite: true)</c>，只覆盖"同名"文件 ——
    /// 旧版本多出来、新版本已经删掉的那些文件（旧 DLL、旧资源、旧的联机 Lua）会**永远留在
    /// 安装目录里**，而且还会被游戏/启动器扫到。所以升级前必须把程序文件清干净。
    ///
    /// 由界面上的"检测到已安装 → 是否覆盖"确认框置位；静默模式默认打开（见 --keep-old）。
    /// </summary>
    public bool ClearBeforeInstall { get; set; }
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

            // ── 清理旧版本 ────────────────────────────────────────────────
            // ⚠️ 时机：必须放在"解包成功之后、往安装目录写之前"。
            //    解包失败时安装目录还完好无损，用户重试或放弃都不会面对一个"半空"的目录；
            //    而一旦开始写，就必须先清干净 —— 否则旧版本多出来的文件会留下来（见 ClearBeforeInstall）。
            if (options.ClearBeforeInstall && LooksLikeInstallDirectory(target))
            {
                progress.Report(new InstallProgress(InstallStage.Cleaning, 67, "正在清理旧版本文件…"));
                int removed = await Task.Run(() => ClearForOverwrite(target), ct);
                note?.Invoke($"已清理旧版本程序文件 {removed} 项（已保留 Mods 等数据目录）");
            }

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
    /// 安装目录里**不属于程序本体**的顶层目录 —— 覆盖安装清理时原样保留。
    ///
    /// <c>Mods</c> 是运行期真实会有用户数据的：玩家往"模组文件夹"里丢的东西就放在
    /// <c>&lt;EXE&gt;\Mods</c>（见 ModInstaller / GameFileHealthService）。
    /// 其余几个（<c>BHL</c> / <c>.minecraft</c> / <c>cache</c> / <c>images</c> / <c>tools</c>）
    /// 是反编译框架时代的遗留目录 —— 不是我们建的，也就没资格替用户删。
    ///
    /// 不在这张表里的（含 <c>Uninstall\</c>）都是安装包放进去的，删掉后会被重新写一遍。
    /// </summary>
    private static readonly string[] PreservedDirectoryNames =
    {
        "Mods", "BHL", ".minecraft", "cache", "images", "tools"
    };

    /// <summary>
    /// 目标目录看起来是不是"我们已经装过的 StartRide 目录"。
    ///
    /// ⚠️ 只有判断为真才允许清空式覆盖。用户完全可能手点"浏览…"选到自己的文档目录、
    /// 游戏根目录甚至盘根 —— 那种情况下只能"往里写文件"，绝不能清空。
    /// </summary>
    internal static bool LooksLikeInstallDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;
            if (IsUnsafeTarget(dir)) return false;
            // 里面就躺着 StartRide.exe —— 这是最强的证据
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

    /// <summary>
    /// 危险目标：盘根、用户主目录、桌面、文档、Program Files、Windows…
    /// 清空这些是灾难，一律判否（只比较"完全相等"，不动它们的子目录）。
    /// </summary>
    private static bool IsUnsafeTarget(string dir)
    {
        string full = Normalize(dir);
        if (full.Length == 0) return true;

        string root = Normalize(Path.GetPathRoot(full) ?? "");
        if (root.Length > 0 && full == root) return true;            // C:\ 这种

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

    /// <summary>
    /// 清空安装目录里的程序文件，返回删掉的文件/目录项数。
    /// 只遍历**顶层**：命中 <see cref="PreservedDirectoryNames"/> 的整个子树都不碰。
    /// </summary>
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
                // 被占用的先留着：后面 File.Copy 会再撞一次，报出来的错误比这里静默吞掉有用
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
                // 同上
            }
        }
        return removed;
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

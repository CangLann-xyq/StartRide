using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StartRide.Core;

namespace StartRide.Services
{
    /// <summary>
    /// StartRide 的在线更新（下载 → 替换 → 重启）。
    ///
    /// 为什么需要自己写一个
    /// --------------------
    /// 框架层（Launcher.Infrastructure.dll，无源码）里那套只支持「下载一个 .exe 覆盖当前 .exe」：
    ///   · LauncherUpdateInfo.CanAutoInstall 只对 AssetKind=WindowsX64Executable 返回 true，
    ///     清单写 zip 时界面上的「更新」按钮点了会直接报「未找到可自动安装的更新包」并返回，
    ///     StartUpdateAsync 根本不会被调用（已用探针实测）
    ///   · 它的 UserAgent 常量还写着一个与本项目无关的旧名字
    /// 而 StartRide 的分发形态是 zip 安装包（exe 只有 200 KB，界面全在 StartRide.dll 里，
    /// 只替换 exe 毫无意义），所以必须支持「整包替换」。
    ///
    /// 流程
    /// ----
    ///   1. 从清单里的候选源（自有域名优先）下载 zip 到
    ///      %LOCALAPPDATA%\StartRide\update\&lt;版本&gt;\package.zip
    ///   2. 校验 sizeBytes / sha256（清单给了就一定要对）
    ///   3. 解包到同目录 stage\，并剥掉包内那层顶层文件夹
    ///   4. 生成 apply.ps1 并启动它，参数里带上当前进程 pid / 安装目录 / stage 目录
    ///   5. 界面侧随后调用 Shutdown()；脚本等本进程退出后覆盖安装目录并重启新版本
    ///
    /// 为什么用 PowerShell 脚本而不是 .cmd
    /// ----------------------------------
    ///   安装路径可能含中文，.cmd 要处理代码页（chcp + 脚本自身编码）很容易翻车；
    ///   PowerShell 由 .NET 启动、参数是原生 UTF-16，且路径全程用 -LiteralPath 处理，天然安全。
    ///   脚本内容本身是纯 ASCII（路径全部走参数），不存在编码问题。
    ///
    /// 保留用户数据
    /// ------------
    ///   只「覆盖」包里带的文件，不镜像整个目录 —— 用户的 Mods/、日志、历史数据目录不会被删。
    ///   仅额外删除已知的历史遗留文件（改名前的旧程序集）。
    /// </summary>
    public sealed class StartRideSelfUpdateService : ILauncherSelfUpdateService
    {
        /// <summary>下载用 UA。旧实现里这里是那个已经弃用的品牌名。</summary>
        private static string UserAgent => "StartRide-Launcher/" + BuildInfo.Version;

        /// <summary>一整套启动器产物会用到的扩展名（同一「主机名」下成套出现）。</summary>
        private static readonly string[] LauncherBundleSuffixes =
        {
            ".exe",
            ".dll",
            ".deps.json",
            ".runtimeconfig.json",
            ".pdb",
        };

        /// <summary>认定「一整套启动器产物」必须同时存在的后缀。</summary>
        private static readonly string[] LauncherBundleRequiredSuffixes =
        {
            ".exe",
            ".dll",
        };

        /// <summary>认定「一整套启动器产物」至少要有其一的后缀（.NET 发布物的特征）。</summary>
        private static readonly string[] LauncherBundleDescriptorSuffixes =
        {
            ".deps.json",
            ".runtimeconfig.json",
        };

        /// <summary>
        /// 安装目录里「新包不再提供、但确实是一整套启动器产物」的残留文件名。
        ///
        /// 换过程序集名的那版产物会在安装目录留下**另一套**完整的启动器文件（同一主机名 +
        /// .exe / .dll / .deps.json / .runtimeconfig.json / .pdb），与现在的 StartRide.* 并存。
        /// 这里按「成套装」识别，而不是按名字硬编码：
        ///   · 主机名取自 &lt;stem&gt;.exe，且该 .exe 不在新包里（= 新包主机名是另一个）
        ///   · 同时还存在 &lt;stem&gt;.dll 与 &lt;stem&gt;.deps.json 或 .runtimeconfig.json
        /// 好处：源码里不写死任何已弃用的名字，也不会误删根目录下的普通依赖 dll
        /// （它们没有同名 .exe），程序集以后再改名照样能清干净。
        /// </summary>
        private static IReadOnlyList<string> FindObsoleteArtifacts(string installDirectory, string stageDirectory)
        {
            var shipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var staleStems = new List<string>();
            try
            {
                foreach (string file in Directory.EnumerateFiles(stageDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    shipped.Add(Path.GetFileName(file));
                }
                foreach (string file in Directory.EnumerateFiles(installDirectory, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    if (!shipped.Contains(name))
                    {
                        staleStems.Add(Path.GetFileNameWithoutExtension(name));
                    }
                }
            }
            catch
            {
                // 目录读不了就不删任何东西：宁可留残留，也不误删用户已有的文件
                return Array.Empty<string>();
            }

            var obsolete = new List<string>();
            foreach (string stem in staleStems)
            {
                if (!HasAllSuffixes(installDirectory, stem, LauncherBundleRequiredSuffixes)
                    || !HasAnySuffix(installDirectory, stem, LauncherBundleDescriptorSuffixes))
                {
                    continue;
                }
                foreach (string suffix in LauncherBundleSuffixes)
                {
                    string name = stem + suffix;
                    if (!shipped.Contains(name) && File.Exists(Path.Combine(installDirectory, name)))
                    {
                        obsolete.Add(name);
                    }
                }
            }
            return obsolete;
        }

        private static bool HasAllSuffixes(string directory, string stem, IReadOnlyList<string> suffixes)
        {
            foreach (string suffix in suffixes)
            {
                if (!File.Exists(Path.Combine(directory, stem + suffix)))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasAnySuffix(string directory, string stem, IReadOnlyList<string> suffixes)
        {
            foreach (string suffix in suffixes)
            {
                if (File.Exists(Path.Combine(directory, stem + suffix)))
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly HttpClient Http = CreateClient();

        private readonly ILogger<StartRideSelfUpdateService> _logger;

        /// <summary>被替换的安装目录（默认 = 当前程序所在目录）。</summary>
        private readonly string? _installDirectoryOverride;

        /// <summary>替换脚本要等待退出的进程（默认 = 当前进程）。</summary>
        private readonly int? _processIdOverride;

        public StartRideSelfUpdateService(ILogger<StartRideSelfUpdateService>? logger = null)
        {
            _logger = logger ?? NullLogger<StartRideSelfUpdateService>.Instance;
        }

        /// <summary>
        /// 端到端验证用：把「安装目录」和「被等待的进程」换成指定值，
        /// 这样可以在临时目录里完整走一遍下载→替换→重启，而不动真正的安装目录。
        /// （框架层的 LauncherSelfUpdateService 也留了同样性质的构造函数。）
        /// </summary>
        internal StartRideSelfUpdateService(string installDirectory, int processId,
            ILogger<StartRideSelfUpdateService>? logger = null)
            : this(logger)
        {
            _installDirectoryOverride = installDirectory;
            _processIdOverride = processId;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            try
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            }
            catch
            {
                // UA 只是礼貌，加不上也不影响下载
            }
            return client;
        }

        // ------------------------------------------------------------------ 判定

        /// <summary>
        /// zip 安装包能不能自动安装。
        ///
        /// 界面在调用 <see cref="StartUpdateAsync"/> 之前会先问框架层的
        /// <c>LauncherUpdateInfo.CanAutoInstall</c>（它只认 WindowsX64Executable），
        /// 所以那边为 false 时还要再问一次这里，否则 zip 包永远走不到安装。
        /// </summary>
        public static bool CanAutoInstall(LauncherUpdateInfo? update)
        {
            if (update == null)
            {
                return false;
            }

            if (update.AssetKind == LauncherUpdateAssetKind.WindowsX64Executable)
            {
                return true;   // 框架层自己就能处理
            }

            if (update.AssetKind != LauncherUpdateAssetKind.ZipPackage)
            {
                return false;
            }

            return ResolvePackageUrls(update).Count > 0;
        }

        /// <summary>
        /// 挑出可用的下载地址：清单里的 downloadUrls + downloadUrl，按「自有域名优先」排序。
        ///
        /// 自有域名（windseek.cloud）在国内直连快，GitHub 那两条基本不可达，
        /// 所以不能让 GitHub 排在前面拖慢失败重试。
        /// </summary>
        internal static List<LauncherUpdateDownloadUrl> ResolvePackageUrls(LauncherUpdateInfo update)
        {
            var results = new List<LauncherUpdateDownloadUrl>();

            try
            {
                if (update.EffectiveDownloadUrls != null)
                {
                    foreach (LauncherUpdateDownloadUrl item in update.EffectiveDownloadUrls)
                    {
                        if (item != null && !string.IsNullOrWhiteSpace(item.Url))
                        {
                            results.Add(item);
                        }
                    }
                }
            }
            catch
            {
                // EffectiveDownloadUrls 是框架层的计算属性，异常时退化成只用 downloadUrl
            }

            if (results.Count == 0 && !string.IsNullOrWhiteSpace(update.DownloadUrl))
            {
                results.Add(new LauncherUpdateDownloadUrl("清单直链", update.DownloadUrl, 0));
            }

            // 去重 + 排序（自有域名在前，其余按清单给的优先级）
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            results = results.Where(x => seen.Add(x.Url)).OrderBy(Rank).ToList();
            return results;
        }

        private static int Rank(LauncherUpdateDownloadUrl item)
        {
            int ownHost = IsOwnHost(item.Url) ? 0 : 1;
            return ownHost * 1000 + item.Priority;
        }

        private static bool IsOwnHost(string url)
        {
            try
            {
                Uri uri = new Uri(url, UriKind.Absolute);
                string host = uri.Host.ToLowerInvariant();
                return host.EndsWith("windseek.cloud", StringComparison.Ordinal)
                    || host.EndsWith("startride.top", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ 执行

        public async Task<LauncherSelfUpdateStartResult> StartUpdateAsync(
            LauncherUpdateInfo update, CancellationToken cancellationToken)
        {
            if (!CanAutoInstall(update))
            {
                return LauncherSelfUpdateStartResult.Failed("更新清单里没有可用的安装包地址。");
            }

            string? executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return LauncherSelfUpdateStartResult.Failed("无法定位当前启动器程序文件。");
            }

            string installDirectory = _installDirectoryOverride
                ?? Path.GetDirectoryName(executablePath)!;
            if (!Directory.Exists(installDirectory))
            {
                return LauncherSelfUpdateStartResult.Failed("安装目录不存在：" + installDirectory);
            }
            int waitingProcessId = _processIdOverride ?? Environment.ProcessId;
            string version = SanitizeSegment(
                !string.IsNullOrWhiteSpace(update.DisplayVersion) ? update.DisplayVersion : update.Version);
            string rootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StartRide", "update", version);

            try
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
                Directory.CreateDirectory(rootDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to prepare the update directory. Path={Path}", rootDirectory);
            }

            string packagePath = Path.Combine(rootDirectory, "package.zip");
            string stageDirectory = Path.Combine(rootDirectory, "stage");

            // ---- 1) 下载 ----------------------------------------------------
            var urls = ResolvePackageUrls(update);
            var failures = new List<string>();
            bool downloaded = false;

            foreach (LauncherUpdateDownloadUrl candidate in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _logger.LogInformation(
                        "StartRide update download started. Version={Version} Source={Source} Url={Url}",
                        version, candidate.Name, candidate.Url);
                    await DownloadAsync(candidate.Url, packagePath, cancellationToken).ConfigureAwait(false);
                    downloaded = true;
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures.Add(candidate.Name + " -> " + ex.GetType().Name + ": " + ex.Message);
                    _logger.LogWarning(ex, "StartRide update download failed. Url={Url}", candidate.Url);
                }
            }

            if (!downloaded)
            {
                return LauncherSelfUpdateStartResult.Failed("下载更新包失败：" + string.Join(" | ", failures));
            }

            // ---- 2) 校验 ----------------------------------------------------
            long actualSize = new FileInfo(packagePath).Length;
            if (update.SizeBytes > 0 && actualSize != update.SizeBytes)
            {
                return LauncherSelfUpdateStartResult.Failed(
                    "更新包大小不一致（期望 " + update.SizeBytes + " 字节，实际 " + actualSize + " 字节）。");
            }

            string expectedSha = (update.Sha256 ?? string.Empty).Trim();
            if (IsHex64(expectedSha))
            {
                string actualSha;
                using (FileStream stream = File.OpenRead(packagePath))
                {
                    actualSha = Convert.ToHexString(SHA256.HashData(stream));
                }
                if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                {
                    return LauncherSelfUpdateStartResult.Failed("更新包校验失败（SHA-256 不一致）。");
                }
                _logger.LogInformation("StartRide update package verified. Sha256={Sha256}", actualSha);
            }

            // ---- 3) 解包（剥掉顶层文件夹）-----------------------------------
            try
            {
                ExtractPackage(packagePath, stageDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartRide update package extraction failed. Path={Path}", packagePath);
                return LauncherSelfUpdateStartResult.Failed("解压更新包失败：" + ex.Message);
            }

            string? newExecutable = FindLauncherExecutable(stageDirectory);
            if (newExecutable == null)
            {
                return LauncherSelfUpdateStartResult.Failed("更新包里没有找到启动器程序。");
            }

            // ---- 4) 生成并启动替换脚本 --------------------------------------
            string scriptPath = Path.Combine(rootDirectory, "apply.ps1");
            string logPath = Path.Combine(rootDirectory, "apply.log");
            try
            {
                // 带 BOM：PowerShell 5.1 只有看到 BOM 才稳定按 UTF-8 解析（见 ApplyScript 注释）
                File.WriteAllText(scriptPath, ApplyScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                StartApplyScript(
                    scriptPath,
                    processId: waitingProcessId,
                    installDirectory: installDirectory,
                    stageDirectory: stageDirectory,
                    executableName: Path.GetFileName(newExecutable),
                    logPath: logPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start the StartRide update apply script.");
                return LauncherSelfUpdateStartResult.Failed("启动更新程序失败：" + ex.Message);
            }

            _logger.LogInformation(
                "StartRide update staged. Version={Version} Install={Install} Stage={Stage} Script={Script} Log={Log}",
                version, installDirectory, stageDirectory, scriptPath, logPath);

            return LauncherSelfUpdateStartResult.Success(packagePath);
        }

        private static async Task DownloadAsync(string url, string destinationPath, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await Http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(
                destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await source.CopyToAsync(target, 81920, cancellationToken).ConfigureAwait(false);
            await target.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>解包到目标目录；包内若只有一层文件夹（我们打包就是这样），把它剥掉。</summary>
        internal static void ExtractPackage(string packagePath, string stageDirectory)
        {
            string extractDirectory = stageDirectory + ".raw";
            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, recursive: true);
            }
            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(packagePath, extractDirectory, overwriteFiles: true);

            string source = extractDirectory;
            string[] topDirectories = Directory.GetDirectories(extractDirectory);
            string[] topFiles = Directory.GetFiles(extractDirectory);
            if (topDirectories.Length == 1 && topFiles.Length == 0)
            {
                source = topDirectories[0];
            }

            if (Directory.Exists(stageDirectory))
            {
                Directory.Delete(stageDirectory, recursive: true);
            }
            Directory.CreateDirectory(stageDirectory);

            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string destination = Path.Combine(stageDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }

            try
            {
                Directory.Delete(extractDirectory, recursive: true);
            }
            catch
            {
                // 临时目录删不掉不影响替换，下次更新会清
            }
        }

        /// <summary>在解包结果里找启动器程序（优先 StartRide.exe）。</summary>
        internal static string? FindLauncherExecutable(string stageDirectory)
        {
            string preferred = Path.Combine(stageDirectory, "StartRide.exe");
            if (File.Exists(preferred))
            {
                return preferred;
            }

            return Directory.EnumerateFiles(stageDirectory, "*.exe", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static void StartApplyScript(
            string scriptPath, int processId, string installDirectory,
            string stageDirectory, string executableName, string logPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-WindowStyle");
            startInfo.ArgumentList.Add("Hidden");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-WaitProcessId");
            startInfo.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-InstallDirectory");
            startInfo.ArgumentList.Add(installDirectory);
            startInfo.ArgumentList.Add("-StageDirectory");
            startInfo.ArgumentList.Add(stageDirectory);
            startInfo.ArgumentList.Add("-ExecutableName");
            startInfo.ArgumentList.Add(executableName);
            startInfo.ArgumentList.Add("-ObsoleteNames");
            startInfo.ArgumentList.Add(string.Join(";", FindObsoleteArtifacts(installDirectory, stageDirectory)));
            startInfo.ArgumentList.Add("-LogPath");
            startInfo.ArgumentList.Add(logPath);

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("powershell.exe 未能启动。");
            }
        }

        private static bool IsHex64(string value)
        {
            if (value.Length != 64)
            {
                return false;
            }
            foreach (char c in value)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return false;
                }
            }
            return true;
        }

        private static string SanitizeSegment(string value)
        {
            var builder = new StringBuilder();
            foreach (char c in value)
            {
                builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }
            string result = builder.ToString().Trim();
            return result.Length == 0 ? "latest" : result;
        }

        /// <summary>
        /// 替换脚本。**内容必须保持纯 ASCII**，路径/文件名全部通过参数传入。
        ///
        /// 两个必须遵守的约束（都踩过）：
        ///   1) 不能写中文注释 —— PowerShell 5.1 对**无 BOM** 的文件按系统 ANSI 代码页
        ///      读取，UTF-8 的中文注释会被解码成乱码并破坏语法（表现为 ParserError
        ///      且报错行号与真实位置对不上）。
        ///   2) 写入时带 UTF-8 BOM，进一步保证解析一致。
        ///
        /// 职责：等旧进程退出 → 覆盖式复制 stage 到安装目录（只覆盖、不镜像，保留用户数据）
        /// → 删掉历史遗留文件 → 启动新版本。
        /// </summary>
        private const string ApplyScript = @"param(
    [Parameter(Mandatory = $true)][int]$WaitProcessId,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][string]$StageDirectory,
    [Parameter(Mandatory = $true)][string]$ExecutableName,
    [string]$ObsoleteNames = '',
    [string]$LogPath = ''
)

$ErrorActionPreference = 'Continue'

function Write-UpdateLog([string]$Message) {
    $line = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') + '  ' + $Message
    if ($LogPath -ne '') {
        try { Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8 } catch { }
    }
}

function Copy-FileWithRetry([string]$Source, [string]$Destination) {
    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Destination -Force -ErrorAction Stop
            return $true
        } catch {
            if ($attempt -eq 20) {
                Write-UpdateLog ('COPY FAILED  ' + $Destination + '  ' + $_.Exception.Message)
                return $false
            }
            Start-Sleep -Milliseconds 500
        }
    }
    return $false
}

Write-UpdateLog ('update apply started. waitPid=' + $WaitProcessId + ' install=' + $InstallDirectory)

$deadline = (Get-Date).AddMinutes(5)
# PID 0 is the System Idle Process: Get-Process finds it and it never exits,
# so 0/negative must be excluded explicitly or we wait for the timeout.
while ($WaitProcessId -gt 0) {
    if ($null -eq (Get-Process -Id $WaitProcessId -ErrorAction SilentlyContinue)) {
        break
    }
    if ((Get-Date) -gt $deadline) {
        Write-UpdateLog 'timed out waiting for the old launcher to exit'
        break
    }
    Start-Sleep -Milliseconds 400
}

Start-Sleep -Milliseconds 1200
Write-UpdateLog 'old process exited, copying files'

$copied = 0
$failed = 0
$stage = $StageDirectory.TrimEnd('\')
foreach ($file in (Get-ChildItem -LiteralPath $stage -Recurse -File -Force)) {
    $relative = $file.FullName.Substring($stage.Length).TrimStart('\')
    $destination = Join-Path $InstallDirectory $relative
    $parent = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    if (Copy-FileWithRetry $file.FullName $destination) { $copied++ } else { $failed++ }
}

Write-UpdateLog ('copied=' + $copied + ' failed=' + $failed)

if ($ObsoleteNames -ne '') {
    foreach ($name in $ObsoleteNames.Split(';')) {
        if ($name -eq '') { continue }
        # Double safety: deletion runs after the copy, so never remove a name the new package ships.
        if (Test-Path -LiteralPath (Join-Path $stage $name)) { continue }
        $path = Join-Path $InstallDirectory $name
        if (Test-Path -LiteralPath $path) {
            try {
                Remove-Item -LiteralPath $path -Force -ErrorAction Stop
                Write-UpdateLog ('removed obsolete file ' + $name)
            } catch {
                Write-UpdateLog ('failed to remove obsolete file ' + $name)
            }
        }
    }
}

# Since 2.9.0 the app resolves its data directories itself, so the old directory junctions
# (BHL / .minecraft pointing at %APPDATA%\StartRide\app) are leftovers and must go.
# Directory::Delete with recursive=$false removes the reparse point only, never the target,
# and fails harmlessly if the name is a real non-empty directory.
$legacyJunctionNames = @('BHL', '.minecraft')
foreach ($name in $legacyJunctionNames) {
    if (Test-Path -LiteralPath (Join-Path $stage $name)) { continue }
    $junctionPath = Join-Path $InstallDirectory $name
    if (-not (Test-Path -LiteralPath $junctionPath)) { continue }
    # Only touch it when it really is a link. A plain directory of the same name may hold
    # user data, so it is left alone (and said so) rather than force-deleted.
    $legacyItem = Get-Item -LiteralPath $junctionPath -Force -ErrorAction SilentlyContinue
    if ($null -eq $legacyItem) { continue }
    if (($legacyItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) {
        Write-UpdateLog ('kept legacy directory ' + $name + ' (not a link)')
        continue
    }
    try {
        [System.IO.Directory]::Delete($junctionPath, $false)
        Write-UpdateLog ('removed legacy junction ' + $name)
    } catch {
        Write-UpdateLog ('failed to remove legacy junction ' + $name + ' : ' + $_.Exception.Message)
    }
}

if ($failed -eq 0) {
    try {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction Stop
        Write-UpdateLog 'staging directory removed'
    } catch {
        Write-UpdateLog 'staging directory could not be removed'
    }
}

$executable = Join-Path $InstallDirectory $ExecutableName
if (Test-Path -LiteralPath $executable) {
    try {
        Start-Process -FilePath $executable
        Write-UpdateLog ('restarted ' + $executable)
    } catch {
        Write-UpdateLog ('restart failed  ' + $_.Exception.Message)
    }
} else {
    Write-UpdateLog ('executable not found after update  ' + $executable)
}

Write-UpdateLog 'update apply finished'
";
    }
}

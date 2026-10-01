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

    public enum StartRideUpdateStage
    {
        Preparing,

        Downloading,

        Verifying,

        Extracting,

        Ready,
    }

    public readonly record struct StartRideUpdateProgress(StartRideUpdateStage Stage, long Received, long Total);

    public sealed class StartRideSelfUpdateService : ILauncherSelfUpdateService
    {
        private static string UserAgent => "StartRide-Launcher/" + BuildInfo.Version;

        private static readonly string[] LauncherBundleSuffixes =
        {
            ".exe",
            ".dll",
            ".deps.json",
            ".runtimeconfig.json",
            ".pdb",
        };

        private static readonly string[] LauncherBundleRequiredSuffixes =
        {
            ".exe",
            ".dll",
        };

        private static readonly string[] LauncherBundleDescriptorSuffixes =
        {
            ".deps.json",
            ".runtimeconfig.json",
        };

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

        private readonly string? _installDirectoryOverride;

        private readonly int? _processIdOverride;

        public StartRideSelfUpdateService(ILogger<StartRideSelfUpdateService>? logger = null)
        {
            _logger = logger ?? NullLogger<StartRideSelfUpdateService>.Instance;
        }

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
            }
            return client;
        }

        public static bool CanAutoInstall(LauncherUpdateInfo? update)
        {
            if (update == null)
            {
                return false;
            }

            if (update.AssetKind == LauncherUpdateAssetKind.WindowsX64Executable)
            {
                return true;
            }

            if (update.AssetKind != LauncherUpdateAssetKind.ZipPackage)
            {
                return false;
            }

            return ResolvePackageUrls(update).Count > 0;
        }

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
            }

            if (results.Count == 0 && !string.IsNullOrWhiteSpace(update.DownloadUrl))
            {
                results.Add(new LauncherUpdateDownloadUrl("清单直链", update.DownloadUrl, 0));
            }

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

        public Task<LauncherSelfUpdateStartResult> StartUpdateAsync(
            LauncherUpdateInfo update, CancellationToken cancellationToken)
        {
            return StartUpdateWithProgressAsync(update, null, cancellationToken);
        }

        public async Task<LauncherSelfUpdateStartResult> StartUpdateWithProgressAsync(
            LauncherUpdateInfo update, IProgress<StartRideUpdateProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!CanAutoInstall(update))
            {
                return LauncherSelfUpdateStartResult.Failed("更新清单里没有可用的安装包地址。");
            }

            Report(progress, StartRideUpdateStage.Preparing, 0, 0);

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
                    await DownloadAsync(candidate.Url, packagePath, update.SizeBytes, progress, cancellationToken).ConfigureAwait(false);
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

            Report(progress, StartRideUpdateStage.Verifying, 0, 0);
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

            Report(progress, StartRideUpdateStage.Extracting, 0, 0);
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

            string scriptPath = Path.Combine(rootDirectory, "apply.ps1");
            string logPath = Path.Combine(rootDirectory, "apply.log");
            try
            {
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

            StartRideUpdateJournal.Begin(BuildInfo.Version, version, logPath);
            Report(progress, StartRideUpdateStage.Ready, 1, 1);

            return LauncherSelfUpdateStartResult.Success(packagePath);
        }

        private static void Report(IProgress<StartRideUpdateProgress>? progress, StartRideUpdateStage stage, long received, long total)
        {
            if (progress == null)
            {
                return;
            }

            try
            {
                progress.Report(new StartRideUpdateProgress(stage, received, total));
            }
            catch
            {
            }
        }

        private static async Task DownloadAsync(
            string url, string destinationPath, long expectedSize,
            IProgress<StartRideUpdateProgress>? progress, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await Http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long total = expectedSize > 0 ? expectedSize : (response.Content.Headers.ContentLength ?? 0L);
            Report(progress, StartRideUpdateStage.Downloading, 0, total);

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(
                destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                Report(progress, StartRideUpdateStage.Downloading, received, total);
            }

            await target.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

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
            }
        }

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
        $partial = $Destination + '.srnew'
        try {
            Copy-Item -LiteralPath $Source -Destination $partial -Force -ErrorAction Stop
            # Commit by rename. Writing straight to $Destination leaves a few seconds where
            # the target file is half-written: a user who double-clicks the shortcut during
            # that window launches a broken exe (or the old one) and concludes the update
            # failed. Rename-over-existing is atomic on NTFS, so the target is never partial.
            Move-Item -LiteralPath $partial -Destination $Destination -Force -ErrorAction Stop
            return $true
        } catch {
            if ($attempt -eq 20) {
                Write-UpdateLog ('COPY FAILED  ' + $Destination + '  ' + $_.Exception.Message)
                return $false
            }
            Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
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

# Sweep leftover partials: a copy killed mid-flight (reboot, taskkill) leaves *.srnew behind.
Get-ChildItem -LiteralPath $InstallDirectory -Filter '*.srnew' -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
}

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

# The app resolves its data directories itself, so the old directory junctions
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

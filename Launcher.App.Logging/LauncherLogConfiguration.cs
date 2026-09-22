using System;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.File;

namespace Launcher.App.Logging;

internal static class LauncherLogConfiguration
{
	private sealed class LauncherLogFileLifecycleHooks(string logDirectory) : FileLifecycleHooks
	{
		public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
		{
			PruneOldLogFiles(logDirectory, DateTimeOffset.Now);
			return underlyingStream;
		}
	}

	public const int RetainedDays = 30;

	public const int MaxRetainedLauncherLogFiles = 20;

	public const long FileSizeLimitBytes = 20971520L;

	public const bool RollOnFileSizeLimit = true;

	public const string LogFileNamePrefix = "startride-";

	private const string LogDirectoryName = "log";

	private static readonly string[] LogFileSearchPatterns = new string[2] { "startride*.log", "updater-*.log" };

	public static ILogger CreateLogger(LoggingLevelSwitch levelSwitch, LoggingLevelSwitch microsoftLevelSwitch)
	{
		ArgumentNullException.ThrowIfNull(levelSwitch, "levelSwitch");
		ArgumentNullException.ThrowIfNull(microsoftLevelSwitch, "microsoftLevelSwitch");
		string text = ResolveLogDirectory();
		Directory.CreateDirectory(text);
		DateTimeOffset now = DateTimeOffset.Now;
		PruneOldLogFiles(text, now, 19);
		string path = Path.Combine(text, CreateLogFileName(now, Environment.ProcessId));
		LoggerSinkConfiguration writeTo = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch).MinimumLevel.Override("Microsoft", microsoftLevelSwitch).Enrich.FromLogContext().WriteTo;
		int? retainedFileCountLimit = null;
		long? fileSizeLimitBytes = 20971520L;
		FileLifecycleHooks hooks = new LauncherLogFileLifecycleHooks(text);
		return writeTo.File(path, LogEventLevel.Verbose, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}", null, fileSizeLimitBytes, null, buffered: false, shared: false, null, RollingInterval.Infinite, rollOnFileSizeLimit: true, retainedFileCountLimit, null, hooks).CreateLogger();
	}

	internal static string CreateLogFileName(DateTimeOffset startedAt, int processId)
	{
		if (processId < 0)
		{
			throw new ArgumentOutOfRangeException("processId");
		}
		return $"{"startride-"}{startedAt:yyyyMMdd-HHmmss-fff}-p{processId}.log";
	}

	/// <summary>
	/// 启动器日志目录。
	/// 铁律：**不再使用 EXE 旁的 StartRide\log**。
	/// 原因：EXE 旁写数据会导致"从不同目录启动各有一份配置/日志"，用户看到的设置与日志互相打架；
	/// 统一落到 %APPDATA%\StartRide\Log（与 AppSettings.ConfigDirectory 同一棵树）。
	/// </summary>
	public static string ResolveLogDirectory()
	{
		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"StartRide",
			"Log");
	}

	public static void PruneOldLogFiles(string logDirectory, DateTimeOffset now, int maxLauncherLogFiles = 20)
	{
		if (maxLauncherLogFiles < 0)
		{
			throw new ArgumentOutOfRangeException("maxLauncherLogFiles");
		}
		if (!Directory.Exists(logDirectory))
		{
			return;
		}
		DateTime utcDateTime = now.AddDays(-30.0).UtcDateTime;
		string[] logFileSearchPatterns = LogFileSearchPatterns;
		foreach (string searchPattern in logFileSearchPatterns)
		{
			foreach (string item in Directory.EnumerateFiles(logDirectory, searchPattern, SearchOption.TopDirectoryOnly))
			{
				DeleteIfExpired(item, utcDateTime);
			}
		}
		FileInfo[] array = (from path in Directory.EnumerateFiles(logDirectory, "startride*.log", SearchOption.TopDirectoryOnly)
			select new FileInfo(path) into file
			orderby file.LastWriteTimeUtc descending
			select file).ThenByDescending<FileInfo, string>((FileInfo file) => file.Name, StringComparer.OrdinalIgnoreCase).Skip(maxLauncherLogFiles).ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			TryDeleteFile(array[i].FullName);
		}
	}

	public static LauncherLogCleanupResult ClearLogFiles(string logDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory, "logDirectory");
		if (!Directory.Exists(logDirectory))
		{
			return LauncherLogCleanupResult.Empty;
		}
		int num = 0;
		int num2 = 0;
		string[] logFileSearchPatterns = LogFileSearchPatterns;
		foreach (string searchPattern in logFileSearchPatterns)
		{
			foreach (string item in Directory.EnumerateFiles(logDirectory, searchPattern, SearchOption.TopDirectoryOnly))
			{
				if (TryDeleteFile(item))
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		return new LauncherLogCleanupResult(num, num2);
	}

	private static void DeleteIfExpired(string path, DateTime cutoff)
	{
		try
		{
			if (File.GetLastWriteTimeUtc(path) < cutoff)
			{
				File.Delete(path);
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	private static bool TryDeleteFile(string path)
	{
		try
		{
			File.Delete(path);
			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}
}

using Serilog.Core;
using Serilog.Events;

namespace Launcher.App.Logging;

internal sealed class LauncherLogLevelController : ILauncherLogLevelController
{
	public bool IsDiagnosticLoggingEnabled { get; private set; }

	internal LoggingLevelSwitch LevelSwitch { get; }

	internal LoggingLevelSwitch MicrosoftLevelSwitch { get; }

	public LauncherLogLevelController(bool enableDiagnosticLogging)
	{
		LevelSwitch = new LoggingLevelSwitch(ResolveMinimumLevel(enableDiagnosticLogging));
		MicrosoftLevelSwitch = new LoggingLevelSwitch(ResolveMicrosoftMinimumLevel(enableDiagnosticLogging));
		IsDiagnosticLoggingEnabled = enableDiagnosticLogging;
	}

	public void SetDiagnosticLoggingEnabled(bool enabled)
	{
		IsDiagnosticLoggingEnabled = enabled;
		LevelSwitch.MinimumLevel = ResolveMinimumLevel(enabled);
		MicrosoftLevelSwitch.MinimumLevel = ResolveMicrosoftMinimumLevel(enabled);
	}

	internal static LogEventLevel ResolveMinimumLevel(bool enabled)
	{
		if (!enabled)
		{
			return LogEventLevel.Information;
		}
		return LogEventLevel.Verbose;
	}

	private static LogEventLevel ResolveMicrosoftMinimumLevel(bool enabled)
	{
		if (!enabled)
		{
			return LogEventLevel.Warning;
		}
		return LogEventLevel.Verbose;
	}
}

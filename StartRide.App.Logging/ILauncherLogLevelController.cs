namespace StartRide.App.Logging;

public interface ILauncherLogLevelController
{
	bool IsDiagnosticLoggingEnabled { get; }

	void SetDiagnosticLoggingEnabled(bool enabled);
}

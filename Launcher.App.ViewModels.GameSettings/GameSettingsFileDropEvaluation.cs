namespace Launcher.App.ViewModels.GameSettings;

public readonly record struct GameSettingsFileDropEvaluation(bool ShouldHandle, bool CanAccept, string Message)
{
	public static GameSettingsFileDropEvaluation Hidden => new GameSettingsFileDropEvaluation(ShouldHandle: false, CanAccept: false, string.Empty);

	public static GameSettingsFileDropEvaluation Accept(string message)
	{
		return new GameSettingsFileDropEvaluation(ShouldHandle: true, CanAccept: true, message);
	}

	public static GameSettingsFileDropEvaluation Reject(string message)
	{
		return new GameSettingsFileDropEvaluation(ShouldHandle: true, CanAccept: false, message);
	}
}

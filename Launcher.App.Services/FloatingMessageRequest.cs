namespace Launcher.App.Services;

public readonly record struct FloatingMessageRequest(string Message, bool AutoHide = true);

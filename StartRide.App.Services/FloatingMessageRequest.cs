namespace StartRide.App.Services;

public readonly record struct FloatingMessageRequest(string Message, bool AutoHide = true);

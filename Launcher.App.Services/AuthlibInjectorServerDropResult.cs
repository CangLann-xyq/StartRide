namespace Launcher.App.Services;

internal readonly record struct AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus Status, string? AuthenticationServer = null);

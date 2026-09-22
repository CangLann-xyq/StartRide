namespace Launcher.App.Services;

internal readonly record struct LauncherBackgroundPresentation(string Effect, bool IsWindowBackdropEnabled, bool IsImageBackgroundEnabled, bool IsImageControlBlurEnabled, int PageBackgroundOpacityPercent);

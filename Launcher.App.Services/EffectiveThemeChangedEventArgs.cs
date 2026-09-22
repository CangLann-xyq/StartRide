using System;

namespace Launcher.App.Services;

public sealed class EffectiveThemeChangedEventArgs : EventArgs
{
	public EffectiveTheme OldTheme { get; }

	public EffectiveTheme NewTheme { get; }

	public EffectiveThemeChangedEventArgs(EffectiveTheme oldTheme, EffectiveTheme newTheme)
	{
		OldTheme = oldTheme;
		NewTheme = newTheme;
	}
}

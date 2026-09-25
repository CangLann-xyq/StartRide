using System;

namespace StartRide.App.Services;

public interface IThemeService
{
	EffectiveTheme EffectiveTheme { get; }

	string BackgroundEffect { get; }

	event EventHandler<EffectiveThemeChangedEventArgs>? EffectiveThemeChanged;

	event EventHandler<BackgroundEffectChangedEventArgs>? BackgroundEffectChanged;

	void ApplyPreference(string? theme, bool followSystem, int backgroundOpacityPercent);

	void ApplyAccent(string? accentColor);

	void ApplyBackgroundOpacity(int opacityPercent);

	void ApplyBackgroundEffect(string? backgroundEffect, bool enableImageControlBlur);
}

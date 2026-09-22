using System;
using System.Windows;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace Launcher.App.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<ThemeService> logger;

	private readonly ThemeResourceLayerManager resourceLayerManager;

	private readonly ProgressiveBlurController progressiveBlurController;

	private string preferredTheme = "Dark";

	private string preferredAccentColor = "Blue";

	private string backgroundEffect = "Acrylic";

	private bool followSystem = true;

	private int backgroundOpacityPercent = 85;

	private bool enableImageControlBlur = true;

	private bool hasAppliedTheme;

	private bool isDisposed;

	public EffectiveTheme EffectiveTheme { get; private set; }

	public string BackgroundEffect => backgroundEffect;

	public event EventHandler<EffectiveThemeChangedEventArgs>? EffectiveThemeChanged;

	public event EventHandler<BackgroundEffectChangedEventArgs>? BackgroundEffectChanged;

	public ThemeService(IUiDispatcher uiDispatcher, ILogger<ThemeService>? logger = null)
		: this(uiDispatcher, logger, new WpfProgressiveBlurSupport(), new ThemeResourceLayerManager())
	{
	}

	internal ThemeService(IUiDispatcher uiDispatcher, ILogger<ThemeService>? logger, IProgressiveBlurSupport progressiveBlurSupport, ThemeResourceLayerManager resourceLayerManager)
	{
		this.uiDispatcher = uiDispatcher ?? throw new ArgumentNullException("uiDispatcher");
		this.logger = logger ?? NullLogger<ThemeService>.Instance;
		this.resourceLayerManager = resourceLayerManager ?? throw new ArgumentNullException("resourceLayerManager");
		progressiveBlurController = new ProgressiveBlurController(this.uiDispatcher, progressiveBlurSupport ?? throw new ArgumentNullException("progressiveBlurSupport"), this.logger);
		SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
	}

	public void ApplyPreference(string? theme, bool followSystem, int backgroundOpacityPercent)
	{
		preferredTheme = NormalizeTheme(theme);
		this.followSystem = followSystem;
		this.backgroundOpacityPercent = NormalizeBackgroundOpacity(backgroundOpacityPercent);
		EffectiveTheme effectiveTheme = EffectiveTheme;
		EffectiveTheme effectiveTheme2 = (EffectiveTheme = ResolveEffectiveTheme(preferredTheme, followSystem));
		uiDispatcher.Invoke(delegate
		{
			ApplyAppearanceResourcesCore();
			progressiveBlurController.Initialize();
		});
		if (!hasAppliedTheme)
		{
			hasAppliedTheme = true;
		}
		else if (effectiveTheme != effectiveTheme2)
		{
			EffectiveThemeChanged?.Invoke(this, new EffectiveThemeChangedEventArgs(effectiveTheme, effectiveTheme2));
		}
	}

	public void ApplyAccent(string? accentColor)
	{
		string text = LauncherAccentColors.Normalize(accentColor);
		if (!string.IsNullOrWhiteSpace(accentColor) && !string.Equals(accentColor, text, StringComparison.OrdinalIgnoreCase))
		{
			logger.LogWarning("Invalid launcher accent color preference encountered. AccentColor={AccentColor} FallingBackTo={FallbackAccentColor}", accentColor, text);
		}
		preferredAccentColor = text;
		uiDispatcher.Invoke(ApplyAppearanceResourcesCore);
	}

	public void ApplyBackgroundOpacity(int opacityPercent)
	{
		backgroundOpacityPercent = NormalizeBackgroundOpacity(opacityPercent);
		uiDispatcher.Invoke(ApplyPageBackgroundOpacityCore);
	}

	public void ApplyBackgroundEffect(string? backgroundEffect, bool enableImageControlBlur)
	{
		string text = this.backgroundEffect;
		string text2 = (this.backgroundEffect = LauncherBackgroundEffects.Normalize(backgroundEffect));
		this.enableImageControlBlur = enableImageControlBlur;
		uiDispatcher.Invoke(ApplyAppearanceResourcesCore);
		if (!string.Equals(text, text2, StringComparison.Ordinal))
		{
			BackgroundEffectChanged?.Invoke(this, new BackgroundEffectChangedEventArgs(text, text2));
		}
	}

	public void Dispose()
	{
		if (!isDisposed)
		{
			SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
			progressiveBlurController.Dispose();
			isDisposed = true;
		}
	}

	private void ApplyAppearanceResourcesCore()
	{
		System.Windows.Application current = System.Windows.Application.Current;
		if (current != null)
		{
			LauncherBackgroundPresentation launcherBackgroundPresentation = LauncherBackgroundPresentationPolicy.Resolve(backgroundEffect, backgroundOpacityPercent, enableImageControlBlur);
			resourceLayerManager.ApplyLayers(current.Resources, EffectiveTheme, preferredAccentColor, launcherBackgroundPresentation.IsImageBackgroundEnabled, launcherBackgroundPresentation.IsImageControlBlurEnabled);
			ThemeResourceLayerManager.ApplyPageBackgroundOpacity(current.Resources, launcherBackgroundPresentation.PageBackgroundOpacityPercent);
			logger.LogDebug("Launcher appearance resources applied. Theme={Theme} AccentColor={AccentColor} BackgroundEffect={BackgroundEffect} ImageLayerEnabled={ImageLayerEnabled} ImageControlBlurEnabled={ImageControlBlurEnabled}", EffectiveTheme, preferredAccentColor, launcherBackgroundPresentation.Effect, launcherBackgroundPresentation.IsImageBackgroundEnabled, launcherBackgroundPresentation.IsImageControlBlurEnabled);
		}
	}

	private void ApplyPageBackgroundOpacityCore()
	{
		System.Windows.Application current = System.Windows.Application.Current;
		if (current != null)
		{
			LauncherBackgroundPresentation launcherBackgroundPresentation = LauncherBackgroundPresentationPolicy.Resolve(backgroundEffect, backgroundOpacityPercent, enableImageControlBlur);
			ThemeResourceLayerManager.ApplyPageBackgroundOpacity(current.Resources, launcherBackgroundPresentation.PageBackgroundOpacityPercent);
		}
	}

	private EffectiveTheme ResolveEffectiveTheme(string theme, bool useSystemTheme)
	{
		if (useSystemTheme)
		{
			return ResolveSystemTheme();
		}
		if (!string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
		{
			return EffectiveTheme.Dark;
		}
		return EffectiveTheme.Light;
	}

	private static EffectiveTheme ResolveSystemTheme()
	{
		try
		{
			return (Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", 0) is int num && num > 0) ? EffectiveTheme.Light : EffectiveTheme.Dark;
		}
		catch
		{
			return EffectiveTheme.Dark;
		}
	}

	private static string NormalizeTheme(string? theme)
	{
		if (!string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
		{
			return "Dark";
		}
		return "Light";
	}

	private static int NormalizeBackgroundOpacity(int opacityPercent)
	{
		return Math.Clamp(opacityPercent, 0, 100);
	}

	private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		if (!followSystem)
		{
			return;
		}
		UserPreferenceCategory category = e.Category;
		if (category == UserPreferenceCategory.General || category == UserPreferenceCategory.VisualStyle || category == UserPreferenceCategory.Color)
		{
			uiDispatcher.Post(delegate
			{
				ApplyPreference(preferredTheme, followSystem, backgroundOpacityPercent);
			});
		}
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

internal sealed class ThemeResourceLayerManager
{
	internal const string PageBackgroundOpacityResourceKey = "Opacity.Page.Background";

	private const string DarkThemeSource = "pack://application:,,,/StartRide;component/Resources/Themes/Dark.xaml";

	private const string LightThemeSource = "pack://application:,,,/StartRide;component/Resources/Themes/Light.xaml";

	private const string AccentThemeSourcePrefix = "pack://application:,,,/StartRide;component/Resources/Themes/Accents/";

	private const string ImageBackgroundSource = "pack://application:,,,/StartRide;component/Resources/Themes/Backgrounds/Image.xaml";

	private const string ImageBlurSource = "pack://application:,,,/StartRide;component/Resources/Themes/Backgrounds/ImageBlur.xaml";

	private readonly Func<string, ResourceDictionary> dictionaryFactory;

	private ResourceDictionary? themeLayer;

	private string? themeLayerSource;

	private ResourceDictionary? accentLayer;

	private string? accentLayerSource;

	private ResourceDictionary? imageLayer;

	private ResourceDictionary? imageBlurLayer;

	public ThemeResourceLayerManager()
		: this((string source) => new ResourceDictionary
		{
			Source = new Uri(source, UriKind.Absolute)
		})
	{
	}

	internal ThemeResourceLayerManager(Func<string, ResourceDictionary> dictionaryFactory)
	{
		this.dictionaryFactory = dictionaryFactory ?? throw new ArgumentNullException("dictionaryFactory");
	}

	public void ApplyLayers(ResourceDictionary applicationResources, EffectiveTheme theme, string accentColor, bool imageBackgroundEnabled, bool imageControlBlurEnabled)
	{
		ArgumentNullException.ThrowIfNull(applicationResources, "applicationResources");
		Collection<ResourceDictionary> mergedDictionaries = applicationResources.MergedDictionaries;
		RemoveLayer(mergedDictionaries, themeLayer);
		RemoveLayer(mergedDictionaries, accentLayer);
		RemoveLayer(mergedDictionaries, imageLayer);
		RemoveLayer(mergedDictionaries, imageBlurLayer);
		string themeSource = GetThemeSource(theme);
		themeLayer = GetOrCreateLayer(themeLayer, ref themeLayerSource, themeSource);
		string accentThemeSource = GetAccentThemeSource(accentColor);
		accentLayer = GetOrCreateLayer(accentLayer, ref accentLayerSource, accentThemeSource);
		mergedDictionaries.Add(themeLayer);
		mergedDictionaries.Add(accentLayer);
		if (!imageBackgroundEnabled)
		{
			return;
		}
		if (imageLayer == null)
		{
			imageLayer = dictionaryFactory("pack://application:,,,/StartRide;component/Resources/Themes/Backgrounds/Image.xaml");
		}
		mergedDictionaries.Add(imageLayer);
		if (imageControlBlurEnabled)
		{
			if (imageBlurLayer == null)
			{
				imageBlurLayer = dictionaryFactory("pack://application:,,,/StartRide;component/Resources/Themes/Backgrounds/ImageBlur.xaml");
			}
			mergedDictionaries.Add(imageBlurLayer);
		}
	}

	public static void ApplyPageBackgroundOpacity(ResourceDictionary applicationResources, int opacityPercent)
	{
		ArgumentNullException.ThrowIfNull(applicationResources, "applicationResources");
		applicationResources["Opacity.Page.Background"] = (double)Math.Clamp(opacityPercent, 0, 100) / 100.0;
	}

	internal static string GetThemeSource(EffectiveTheme theme)
	{
		if (theme != EffectiveTheme.Light)
		{
			return "pack://application:,,,/StartRide;component/Resources/Themes/Dark.xaml";
		}
		return "pack://application:,,,/StartRide;component/Resources/Themes/Light.xaml";
	}

	internal static string GetAccentThemeSource(string accentColor)
	{
		return "pack://application:,,,/StartRide;component/Resources/Themes/Accents/" + LauncherAccentColors.Normalize(accentColor) + ".xaml";
	}

	private ResourceDictionary GetOrCreateLayer(ResourceDictionary? currentLayer, ref string? currentSource, string nextSource)
	{
		if (currentLayer != null && string.Equals(currentSource, nextSource, StringComparison.OrdinalIgnoreCase))
		{
			return currentLayer;
		}
		currentSource = nextSource;
		return dictionaryFactory(nextSource);
	}

	private static void RemoveLayer(ICollection<ResourceDictionary> dictionaries, ResourceDictionary? layer)
	{
		if (layer != null)
		{
			while (dictionaries.Remove(layer))
			{
			}
		}
	}
}

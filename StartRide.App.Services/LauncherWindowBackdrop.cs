using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

public static class LauncherWindowBackdrop
{
	public static void Attach(Window window, IThemeService themeService)
	{
		window.SourceInitialized += delegate
		{
			Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
		};
		Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
		EventHandler<EffectiveThemeChangedEventArgs> themeChangedHandler = delegate
		{
			Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
		};
		EventHandler<BackgroundEffectChangedEventArgs> backgroundEffectChangedHandler = delegate
		{
			Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
		};
		themeService.EffectiveThemeChanged += themeChangedHandler;
		themeService.BackgroundEffectChanged += backgroundEffectChangedHandler;
		window.Closed += delegate
		{
			themeService.EffectiveThemeChanged -= themeChangedHandler;
			themeService.BackgroundEffectChanged -= backgroundEffectChangedHandler;
		};
	}

	private static void Apply(Window window, IThemeService themeService, NativeBackdrop.DwmSystemBackdropType enabledBackdropType)
	{
		bool isBackdropEnabled = LauncherBackgroundEffects.IsAcrylic(themeService.BackgroundEffect);
		NativeBackdrop.DwmSystemBackdropType backdropType = ((!isBackdropEnabled) ? NativeBackdrop.DwmSystemBackdropType.None : enabledBackdropType);
		if (!((DispatcherObject)window).Dispatcher.CheckAccess())
		{
			((DispatcherObject)window).Dispatcher.Invoke((Action)delegate
			{
				ApplyCore(window, backdropType, themeService.EffectiveTheme, isBackdropEnabled);
			});
		}
		else
		{
			ApplyCore(window, backdropType, themeService.EffectiveTheme, isBackdropEnabled);
		}
	}

	private static void ApplyCore(Window window, NativeBackdrop.DwmSystemBackdropType backdropType, EffectiveTheme theme, bool isBackdropEnabled)
	{
		ApplyWindowChrome(window, isBackdropEnabled);
		ApplyWindowBackground(window, theme, isBackdropEnabled);
		NativeBackdrop.ApplyToWindow(window, backdropType, theme, isBackdropEnabled);
	}

	private static void ApplyWindowChrome(Window window, bool isBackdropEnabled)
	{
		WindowChrome windowChrome = WindowChrome.GetWindowChrome(window);
		if (windowChrome != null)
		{
			windowChrome.GlassFrameThickness = (isBackdropEnabled ? new Thickness(-1.0) : new Thickness(0.0));
		}
	}

	private static void ApplyWindowBackground(Window window, EffectiveTheme theme, bool isBackdropEnabled)
	{
		if (isBackdropEnabled)
		{
			window.Background = Brushes.Transparent;
		}
		else if (System.Windows.Application.Current?.TryFindResource("Brush.Surface.Window") is Brush background)
		{
			window.Background = background;
		}
		else
		{
			window.Background = new SolidColorBrush(NativeBackdrop.GetOpaqueWindowBackgroundColor(theme));
		}
	}
}

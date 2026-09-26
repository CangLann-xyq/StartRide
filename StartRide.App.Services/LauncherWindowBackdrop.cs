using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

public static class LauncherWindowBackdrop
{
	/// <summary>窗口样式被运行时改过（例如进出全屏）后重新下发一次 DWM 外观。</summary>
	public static void Reapply(Window window, IThemeService themeService)
	{
		Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
	}

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
			// StartRide：恒为 0。原来亚克力开启时会设 -1（= DwmExtendFrameIntoClientArea(-1,-1,-1,-1)
			// 的玻璃框），DWM 会在客户区外描一圈亮色玻璃边 —— 用户看到的「边缘白色渐变」就是它。
			// 亚克力的可见性只依赖 CompositionTarget.BackgroundColor=Transparent，不依赖这圈玻璃框。
			windowChrome.GlassFrameThickness = new Thickness(0.0);
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

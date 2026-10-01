using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

public static class LauncherWindowBackdrop
{
	private const int SettlePassCount = 6;

	private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(200);

	private static readonly Dictionary<Window, SettleState> SettlingWindows = new Dictionary<Window, SettleState>();

	private sealed class SettleState
	{
		public DispatcherTimer? Timer;

		public int Remaining;
	}

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
		window.ContentRendered += delegate
		{
			Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
			ScheduleSettlePasses(window, themeService);
		};
		window.IsVisibleChanged += delegate
		{
			if (window.IsVisible)
			{
				Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
				ScheduleSettlePasses(window, themeService);
			}
		};
		window.Activated += delegate
		{

			Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
		};
		window.SizeChanged += delegate
		{

			if (SettlingWindows.ContainsKey(window))
			{
				Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
			}
		};
		window.Closed += delegate
		{
			StopSettlePasses(window);
			themeService.EffectiveThemeChanged -= themeChangedHandler;
			themeService.BackgroundEffectChanged -= backgroundEffectChangedHandler;
		};
	}

	private static void ScheduleSettlePasses(Window window, IThemeService themeService)
	{
		if (SettlingWindows.TryGetValue(window, out SettleState? existing))
		{
			existing.Remaining = SettlePassCount;
			return;
		}
		SettleState state = new SettleState
		{
			Remaining = SettlePassCount
		};
		state.Timer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher)
		{
			Interval = SettleInterval
		};
		SettlingWindows[window] = state;
		state.Timer.Tick += delegate
		{
			if (state.Remaining <= 0)
			{
				StopSettlePasses(window);
				return;
			}
			state.Remaining--;
			Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
		};
		state.Timer.Start();
	}

	private static void StopSettlePasses(Window window)
	{
		if (SettlingWindows.TryGetValue(window, out SettleState? state))
		{
			state.Timer?.Stop();
			SettlingWindows.Remove(window);
		}
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

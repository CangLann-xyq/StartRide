using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

/// <summary>
/// 主窗口的 DWM 外观（亚克力 / Mica）下发。
///
/// ⚠️ 时间线（踩了整整两轮才摸清，别再动）：
///   · 构造期 Attach()：窗口还没句柄，ApplyToWindow 直接返回 false（无害）。
///   · SourceInitialized：句柄刚建好，属性写得进去，但**窗口还没被 DWM 合成**。
///   · Loaded：发生在第一帧渲染**之前**，同样太早。
///   · ContentRendered：第一帧真的画出来了 —— 从这里往后 DWM 才会认。
/// 症状就是「启动后是一块死板的主题底色，手动拉一下窗口尺寸，亚克力才突然出现」
/// （拉尺寸 = 强制 DWM 重新合成，它这才去读 SystemBackdropType）。
/// 所以 SourceInitialized 之后要排一串「迟到下发」把用户那次手动拉伸替掉。
/// </summary>
public static class LauncherWindowBackdrop
{
	/// <summary>迟到下发的次数。</summary>
	private const int SettlePassCount = 6;

	/// <summary>迟到下发的间隔（总覆盖约 1.2 秒，够冷启动后 DWM 把窗口合成完）。</summary>
	private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(200);

	/// <summary>正在做迟到下发的窗口。key 是窗口本身，Closed 时清掉。</summary>
	private static readonly Dictionary<Window, SettleState> SettlingWindows = new Dictionary<Window, SettleState>();

	private sealed class SettleState
	{
		public DispatcherTimer? Timer;

		public int Remaining;
	}

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
			ScheduleSettlePasses(window, themeService);
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
		};
		window.IsVisibleChanged += delegate
		{
			if (window.IsVisible)
			{
				Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
			}
		};
		window.Activated += delegate
		{
			// 启动器被别的窗口挡着起来时，「显示」和「拿到前台」不是同一时刻，
			// 激活事件可能落在迟到下发之后 —— 只要还在收尾期内就再补一次。
			if (SettlingWindows.ContainsKey(window))
			{
				Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
			}
		};
		window.SizeChanged += delegate
		{
			// 实测"手动拉一下窗口尺寸，亚克力就突然出现" —— 说明「尺寸变化 → DWM 重新合成」
			// 这条路是通的。启动过程里本来就会经历一次尺寸落定（XAML 的 1000x700 换成
			// 用户保存的窗口尺寸），顺手补一次，等于把用户那一下手动拉伸替掉。
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

	/// <summary>
	/// 排一串「迟到下发」。全部是幂等的：重复写同一个 backdrop 类型不会有视觉副作用，
	/// 只在 DWM 真的没认的时候起作用。
	/// </summary>
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
			// StartRide：恒为 0。原来亚克力开启时会设 -1（= DwmExtendFrameIntoClientArea(-1,-1,-1,-1)
			// 的玻璃框），DWM 会在客户区外描一圈亮色玻璃边 —— 用户看到的「边缘白色渐变」就是它。
			// 亚克力的可见性只依赖 CompositionTarget.BackgroundColor=Transparent，不依赖这圈玻璃框。
			// ⚠️ 但**改这里的属性会让 WindowChromeWorker 自己重下发一次 DwmExtendFrameIntoClientArea(0)**，
			// 把 NativeBackdrop 里的 -1 踩掉；所以改完必须紧跟一次 ApplyToWindow（中间不能 await/延迟）。
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

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
	// 首帧稳定期：DWM 在窗口完成首次合成之后才会真正接受
	// DWMWA_SYSTEMBACKDROP_TYPE。用渲染帧计数代替旧的 6x200ms 定时器，
	// 保证每次重申都落在「窗口已经画出来」之后，而不是靠猜一个时间窗。
	private const int SettleFrameCount = 150;

	private const int SettleApplyEveryNFrames = 15;

	// 拖窗口时 SizeChanged 很密，80ms 节流避免反复打 P/Invoke。
	private const int SizeApplyThrottleMs = 80;

	private static readonly Dictionary<Window, SettleState> SettlingWindows = new Dictionary<Window, SettleState>();

	private static readonly Dictionary<Window, long> LastSizeApply = new Dictionary<Window, long>();

	private sealed class SettleState
	{
		public EventHandler? RenderingHandler;

		public int Frames;

		public int Applied;
	}

	public static void Reapply(Window window, IThemeService themeService)
	{
		Apply(window, themeService, NativeBackdrop.DwmSystemBackdropType.TransientWindow);
	}

	public static void Attach(Window window, IThemeService themeService)
	{
		NativeBackdrop.DwmSystemBackdropType enabled = NativeBackdrop.DwmSystemBackdropType.TransientWindow;

		// 构造函数里 HWND 还没创建：这一步只会设置 window.Background，
		// DWM 部分在 NativeBackdrop.ApplyToWindow 里直接返回 false。保留它是为了让
		// 首帧背景就是透明的；真正生效靠 SourceInitialized + 下面的首帧稳定期。
		Apply(window, themeService, enabled);

		window.SourceInitialized += delegate
		{
			Apply(window, themeService, enabled);
		};

		EventHandler<EffectiveThemeChangedEventArgs> themeChangedHandler = delegate
		{
			Apply(window, themeService, enabled);
		};
		EventHandler<BackgroundEffectChangedEventArgs> backgroundEffectChangedHandler = delegate
		{
			Apply(window, themeService, enabled);
		};
		themeService.EffectiveThemeChanged += themeChangedHandler;
		themeService.BackgroundEffectChanged += backgroundEffectChangedHandler;

		window.ContentRendered += delegate
		{
			Apply(window, themeService, enabled);
			ScheduleFirstFramePasses(window, themeService, enabled);
		};
		window.IsVisibleChanged += delegate
		{
			if (window.IsVisible)
			{
				Apply(window, themeService, enabled);
				ScheduleFirstFramePasses(window, themeService, enabled);
			}
		};
		window.Activated += delegate
		{
			Apply(window, themeService, enabled);
		};

		window.SizeChanged += delegate
		{
			// 旧实现在这里带 SettlingWindows.ContainsKey 门控：只有启动后 1.2s 内
			// 拖窗口才会重申 DWM 状态，之后尺寸变化完全不处理 —— 这正是
			// 「打开后得先拉一下窗口尺寸，亚克力才生效」的直接来源。
			// 现在任何尺寸变化都幂等重申一次（DWM 重建 redirection surface 后
			// 本来也可能把 backdrop 丢掉），拖拽时用节流控制频率。
			long now = Environment.TickCount64;
			if (LastSizeApply.TryGetValue(window, out long last) && now - last < SizeApplyThrottleMs)
			{
				return;
			}
			LastSizeApply[window] = now;
			Apply(window, themeService, enabled);
		};

		window.Closed += delegate
		{
			StopFirstFramePasses(window);
			LastSizeApply.Remove(window);
			themeService.EffectiveThemeChanged -= themeChangedHandler;
			themeService.BackgroundEffectChanged -= backgroundEffectChangedHandler;
		};
	}

	private static void ScheduleFirstFramePasses(Window window, IThemeService themeService,
		NativeBackdrop.DwmSystemBackdropType enabled)
	{
		if (SettlingWindows.TryGetValue(window, out SettleState? existing))
		{
			existing.Frames = 0;
			return;
		}
		SettleState state = new SettleState();
		SettlingWindows[window] = state;

		// ContentRendered 当帧之后立刻补一次：覆盖「事件触发时 DWM 还没合成完」的窗口。
		window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate
		{
			if (SettlingWindows.ContainsKey(window))
			{
				Apply(window, themeService, enabled);
			}
		}));

		EventHandler handler = null!;
		handler = delegate
		{
			if (!SettlingWindows.ContainsKey(window))
			{
				CompositionTarget.Rendering -= handler;
				return;
			}
			state.Frames++;
			if (state.Frames % SettleApplyEveryNFrames == 0)
			{
				Apply(window, themeService, enabled);
				state.Applied++;
			}
			if (state.Frames >= SettleFrameCount)
			{
				CompositionTarget.Rendering -= handler;
				StopFirstFramePasses(window);
				VerifyFirstFrame(window, themeService, enabled, state.Applied);
			}
		};
		state.RenderingHandler = handler;
		CompositionTarget.Rendering += handler;
	}

	private static void StopFirstFramePasses(Window window)
	{
		if (SettlingWindows.TryGetValue(window, out SettleState? state))
		{
			if (state.RenderingHandler != null)
			{
				CompositionTarget.Rendering -= state.RenderingHandler;
			}
			SettlingWindows.Remove(window);
		}
	}

	private static void VerifyFirstFrame(Window window, IThemeService themeService,
		NativeBackdrop.DwmSystemBackdropType enabled, int applied)
	{
		int expected = LauncherBackgroundEffects.IsAcrylic(themeService.BackgroundEffect)
			? (int)enabled
			: (int)NativeBackdrop.DwmSystemBackdropType.None;
		int actual = NativeBackdrop.ReadBackdropType(window);
		if (actual < 0)
		{
			Serilog.Log.Information("首帧亚克力稳定期结束（重申 {Applied} 次）；当前系统不支持读回 backdrop 类型", applied);
			return;
		}
		if (actual == expected)
		{
			Serilog.Log.Information("首帧亚克力已稳定：BackdropType={Type}，首帧期重申 {Applied} 次", actual, applied);
		}
		else
		{
			Serilog.Log.Warning("首帧亚克力未生效：期望 {Expected}，实际 {Actual}，首帧期重申 {Applied} 次",
				expected, actual, applied);
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

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace StartRide.App.Services;

internal static class NativeBackdrop
{
	private enum DwmWindowAttribute
	{
		UseImmersiveDarkMode = 20,
		WindowCornerPreference = 33,
		BorderColor = 34,
		SystemBackdropType = 38
	}

	private enum DwmWindowCornerPreference
	{
		Round = 2
	}

	internal enum DwmSystemBackdropType
	{
		None = 1,
		MainWindow,
		TransientWindow,
		TabbedWindow
	}

	private struct Margins
	{
		public int Left;

		public int Right;

		public int Top;

		public int Bottom;
	}

	public static void Enable(Window window, DwmSystemBackdropType backdropType, EffectiveTheme theme)
	{
		window.SourceInitialized += delegate
		{
			ApplyToWindow(window, backdropType, theme);
		};
	}

	public static bool ApplyToWindow(Window window, DwmSystemBackdropType backdropType, EffectiveTheme theme, bool useTransparentSurface = true)
	{
		nint handle = new WindowInteropHelper(window).Handle;
		if (handle == IntPtr.Zero)
		{
			return false;
		}
		HwndSource hwndSource = HwndSource.FromHwnd(handle);
		if (hwndSource?.CompositionTarget != null)
		{
			// ⚠️ 亚克力能不能透出来，只取决于这里 —— WPF 的合成表面必须透明，
			// 否则 WPF 会用自己的底色把 DWM backdrop 盖掉（和 MainWindow 里那层
			// 硬编码不透明的 #FF0F1014 是同一个道理，只是层级更低）。
			hwndSource.CompositionTarget.BackgroundColor = (useTransparentSurface ? Colors.Transparent : GetOpaqueWindowBackgroundColor(theme));
		}
		// ⚠️⚠️ extendIntoClientArea 必须是 true（= DwmExtendFrameIntoClientArea(-1,-1,-1,-1)）：
		// DWMWA_SYSTEMBACKDROP_TYPE 只声明"我要哪种材质"，真正把材质画到**客户区**里的就是这一步
		// —— 它把整个客户区并进 DWM 的 frame，客户区里 alpha<1 的像素才会和 backdrop 合成。
		// 实测（自建纯色底窗 + 桌面亮/暗块 + 前台硬校验）：
		//   margins = 0  → 左栏就是 #181818 原样（20.0），背后一片黑，完全不透 = 亚克力消失
		//   margins = -1 → 左栏 33.0 / 右面板 37.0 / 标题条 38.9，凭空多出壁纸来源的材质
		// 别再用"注释说不要玻璃框"当理由把它改成 0：那圈亮边是
		// WindowChrome.GlassFrameThickness=-1 画的（已在 LauncherWindowBackdrop 恒为 0），
		// 这里的 -1 只负责把 backdrop 铺进客户区，不产生亮边。
		return TryApply(handle, backdropType, theme, extendIntoClientArea: true);
	}

	public static bool TryApplyToPopup(Popup popup, DwmSystemBackdropType backdropType, EffectiveTheme theme)
	{
		if (popup.Child == null)
		{
			return false;
		}
		if (!(PresentationSource.FromVisual(popup.Child) is HwndSource hwndSource))
		{
			return false;
		}
		if (hwndSource.CompositionTarget != null)
		{
			hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;
		}
		return TryApply(hwndSource.Handle, backdropType, theme);
	}

	public static bool TryApply(nint handle, DwmSystemBackdropType backdropType, EffectiveTheme theme, bool extendIntoClientArea = true)
	{
		if (handle == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			Margins margins = new Margins
			{
				Left = (extendIntoClientArea ? (-1) : 0),
				Right = (extendIntoClientArea ? (-1) : 0),
				Top = (extendIntoClientArea ? (-1) : 0),
				Bottom = (extendIntoClientArea ? (-1) : 0)
			};
			DwmExtendFrameIntoClientArea(handle, ref margins);
			int attributeValue = ((theme == EffectiveTheme.Dark) ? 1 : 0);
			DwmSetWindowAttribute(handle, DwmWindowAttribute.UseImmersiveDarkMode, ref attributeValue, 4);
			int attributeValue2 = 2;
			DwmSetWindowAttribute(handle, DwmWindowAttribute.WindowCornerPreference, ref attributeValue2, 4);
			int attributeValue3 = -2;
			DwmSetWindowAttribute(handle, DwmWindowAttribute.BorderColor, ref attributeValue3, 4);
			// ⚠️⚠️ 先设 None、再设目标值 —— 这是「启动后必须手动拖一下窗口尺寸，亚克力才出现」
			// 的根因所在（2026-09-27 定位）：
			//   DWM 对「值没变」的 DwmSetWindowAttribute 直接返回成功但不做任何事，
			//   而 LauncherWindowBackdrop 那 6 次「迟到下发」设的都是同一个值 ——
			//   等于全是空转。真正让材质冒出来的是用户拖窗口那一下：尺寸变化强制 DWM
			//   重新合成，它这才去读 SystemBackdropType。
			//   这里先归零再设回，让每一次下发都真的触发一次重新合成；
			//   两次调用在同一个消息循环里完成，中间态来不及显示，肉眼不可见。
			int noneValue = (int)DwmSystemBackdropType.None;
			DwmSetWindowAttribute(handle, DwmWindowAttribute.SystemBackdropType, ref noneValue, 4);
			int attributeValue4 = (int)backdropType;
			return DwmSetWindowAttribute(handle, DwmWindowAttribute.SystemBackdropType, ref attributeValue4, 4) == 0;
		}
		catch (DllNotFoundException)
		{
			return false;
		}
		catch (EntryPointNotFoundException)
		{
			return false;
		}
		catch (COMException)
		{
			return false;
		}
	}

	public static Color GetOpaqueWindowBackgroundColor(EffectiveTheme theme)
	{
		if (theme != EffectiveTheme.Light)
		{
			return Color.FromRgb(21, 21, 21);
		}
		return Colors.White;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, DwmWindowAttribute attribute, ref int attributeValue, int attributeSize);

	[DllImport("dwmapi.dll")]
	private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
}

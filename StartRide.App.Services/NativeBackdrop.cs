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
		// StartRide：不再把 DWM 的玻璃框撑进客户区（原来是传 -1，会产生一圈亮边）。
		// DWMWA_SYSTEMBACKDROP_TYPE 本身就是整窗生效的，不需要 ExtendFrameIntoClientArea。
		return TryApply(handle, backdropType, theme);
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

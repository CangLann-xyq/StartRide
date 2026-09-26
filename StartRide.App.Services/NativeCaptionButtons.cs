using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StartRide.App.Services;

internal static class NativeCaptionButtons
{
	private const int GwlStyle = -16;

	private const long WsSysMenu = 524288L;

	private const long WsMaximizeBox = 65536L;

	private const uint SwpNoSize = 1u;

	private const uint SwpNoMove = 2u;

	private const uint SwpNoZOrder = 4u;

	private const uint SwpNoActivate = 16u;

	private const uint SwpFrameChanged = 32u;

	public static void Hide(Window window)
	{
		window.SourceInitialized += delegate
		{
			Apply(window);
		};
		if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
		{
			Apply(window);
		}
	}

	/// <summary>
	/// 重新剥一次窗口的原生标题栏样式位（<see cref="Hide"/> 只管首次，这里管"事后"）。
	/// </summary>
	/// <remarks>
	/// 只在窗口的 <c>WindowStyle</c> 被改回来之后需要：WPF 写回 SingleBorderWindow 时会把
	/// 整套样式位重设一遍，把这里剥掉的边框/系统菜单又装回去。装回来之后，任何一次最大化
	/// 都会按「工作区再往每边扩 8px 边框」算尺寸 —— 实测 Esc 退出全屏后窗口矩形变成
	/// (-8,-8)-(1928,1040)（1936x1048），四边都探到屏幕外面去，底部还被任务栏压住。
	/// SourceInitialized 不会再触发第二次，所以必须能从外面主动调。
	/// </remarks>
	public static void Reapply(Window window)
	{
		Apply(window);
	}

	private static void Apply(Window window)
	{
		nint handle = new WindowInteropHelper(window).Handle;
		if (handle != IntPtr.Zero)
		{
			long windowStyle = GetWindowStyle(handle);
			long num = windowStyle & -589825;
			if (num != windowStyle)
			{
				SetWindowStyle(handle, num);
				SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 55u);
			}
		}
	}

	private static long GetWindowStyle(nint handle)
	{
		if (IntPtr.Size != 8)
		{
			return ((IntPtr)GetWindowLong(handle, -16)).ToInt64();
		}
		return ((IntPtr)GetWindowLongPtr(handle, -16)).ToInt64();
	}

	private static nint SetWindowStyle(nint handle, long style)
	{
		if (IntPtr.Size != 8)
		{
			return SetWindowLong(handle, -16, new IntPtr((int)style));
		}
		return SetWindowLongPtr(handle, -16, new IntPtr(style));
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetWindowLong(nint hWnd, int nIndex, nint dwNewLong);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}

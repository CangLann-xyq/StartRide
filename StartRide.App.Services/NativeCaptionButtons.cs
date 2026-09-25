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

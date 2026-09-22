using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace StartRide.Core
{
    /// <summary>
    /// 托盘图标。设置里一直有「最小化到托盘」这个开关，但整个工程里根本没有托盘实现，
    /// 所以这个开关以前点了等于没点。这里用 shell32 的 Shell_NotifyIcon 自己实现，
    /// 不引 WinForms，避免和 WPF 的 Application/MessageBox 命名打架。
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        private const int WM_APP = 0x8000;
        private const int WM_TRAYICON = WM_APP + 1;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;

            public int dwState;
            public int dwStateMask;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;

            public int uVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;

            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconExW(string lpszFile, int nIconIndex,
            IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly Window _window;
        private HwndSource? _source;
        private IntPtr _hIcon = IntPtr.Zero;
        private bool _added;
        private bool _disposed;

        private ContextMenu? _menu;

        /// <summary>双击 / 菜单「打开主界面」时调用。</summary>
        public Action? OnActivate { get; set; }

        /// <summary>菜单「启动游戏」时调用。</summary>
        public Action? OnLaunchGame { get; set; }

        /// <summary>菜单「打开游戏目录」时调用。</summary>
        public Action? OnOpenGameFolder { get; set; }

        /// <summary>菜单「退出启动器」时调用。</summary>
        public Action? OnExit { get; set; }

        public bool IsVisible => _added;

        public TrayIcon(Window window, string tooltip)
        {
            _window = window;
            Tooltip = tooltip;
        }

        public string Tooltip { get; set; }

        /// <summary>挂到主窗口的消息循环上（窗口句柄必须已经创建）。</summary>
        public bool Install()
        {
            try
            {
                if (_added) return true;

                IntPtr hwnd = new WindowInteropHelper(_window).Handle;
                if (hwnd == IntPtr.Zero) return false;

                _source = HwndSource.FromHwnd(hwnd);
                _source?.AddHook(WndProc);

                _hIcon = LoadAppIcon();
                if (_hIcon == IntPtr.Zero) return false;

                var data = BuildData(hwnd);
                _added = Shell_NotifyIconW(NIM_ADD, ref data);
                return _added;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>更新提示文字（比如"游戏正在运行"）。</summary>
        public void UpdateTooltip(string tooltip)
        {
            Tooltip = tooltip;
            if (!_added) return;
            try
            {
                var data = BuildData(new WindowInteropHelper(_window).Handle);
                Shell_NotifyIconW(NIM_MODIFY, ref data);
            }
            catch { }
        }

        private NOTIFYICONDATA BuildData(IntPtr hwnd)
        {
            return new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = string.IsNullOrEmpty(Tooltip) ? "StartRide 启动器" : Truncate(Tooltip, 120),
                szInfo = "",
                szInfoTitle = "",
            };
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        /// <summary>取本进程 EXE 的小图标当托盘图标（不引 System.Drawing）。</summary>
        private static IntPtr LoadAppIcon()
        {
            try
            {
                string exe = Environment.ProcessPath ?? "";
                if (exe.Length > 0)
                {
                    var small = new IntPtr[1];
                    uint n = ExtractIconExW(exe, 0, null, small, 1);
                    if (n > 0 && small[0] != IntPtr.Zero) return small[0];
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_TRAYICON) return IntPtr.Zero;

            int evt = lParam.ToInt32() & 0xFFFF;
            switch (evt)
            {
                case WM_LBUTTONDBLCLK:
                case WM_LBUTTONUP:
                    OnActivate?.Invoke();
                    handled = true;
                    break;

                case WM_RBUTTONUP:
                    ShowMenu();
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        private void ShowMenu()
        {
            try
            {
                _menu ??= BuildMenu();

                // 让托盘菜单能正常收起（否则点别处菜单不消失）
                IntPtr hwnd = new WindowInteropHelper(_window).Handle;
                if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);

                _menu.PlacementTarget = _window;
                _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                _menu.IsOpen = true;
            }
            catch
            {
                // 菜单弹不出来就退化成"把窗口叫回来"，不能让点击没反应
                OnActivate?.Invoke();
            }
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();

            var open = new MenuItem { Header = "打开 StartRide 主界面" };
            open.Click += (_, _) => OnActivate?.Invoke();
            menu.Items.Add(open);

            if (OnLaunchGame != null)
            {
                var launch = new MenuItem { Header = "启动 BeamNG.drive" };
                launch.Click += (_, _) => OnLaunchGame();
                menu.Items.Add(launch);
            }

            if (OnOpenGameFolder != null)
            {
                var folder = new MenuItem { Header = "打开游戏目录" };
                folder.Click += (_, _) => OnOpenGameFolder();
                menu.Items.Add(folder);
            }

            menu.Items.Add(new Separator());

            var exit = new MenuItem { Header = "退出启动器" };
            exit.Click += (_, _) => OnExit?.Invoke();
            menu.Items.Add(exit);

            return menu;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_added)
                {
                    var data = BuildData(new WindowInteropHelper(_window).Handle);
                    Shell_NotifyIconW(NIM_DELETE, ref data);
                    _added = false;
                }
            }
            catch { }

            try
            {
                if (_source != null)
                {
                    _source.RemoveHook(WndProc);
                    _source = null;
                }
            }
            catch { }

            try
            {
                if (_hIcon != IntPtr.Zero)
                {
                    DestroyIcon(_hIcon);
                    _hIcon = IntPtr.Zero;
                }
            }
            catch { }
        }
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace StartRide.Setup;

public partial class UninstallWindow : Window
{
    private string? _installDir;
    private bool _running;

    public UninstallWindow()
    {
        InitializeComponent();
        ApplyBranding();

        Loaded += (_, _) =>
        {
            _installDir = UninstallEngine.ResolveInstallDir();

            if (_installDir == null)
            {
                Headline.Text = "没有找到安装记录";
                Lead.Text = "这台电脑上没有登记过的 StartRide 安装（可能已经卸载，或者当初是解压即用的）。";
                WillDelete.Text = "无需任何操作。";
                UninstallButton.IsEnabled = false;
                return;
            }

            Lead.Text = $"将从下面的目录里移除 StartRide {ProductInfo.InstalledVersion() ?? ""}。";
            WillDelete.Text = "· 程序文件与内置联机模组\n"
                            + "· 桌面与开始菜单快捷方式\n"
                            + "· 应用和功能列表里的卸载项\n"
                            + _installDir;
            StatusText.Text = "安装位置：" + _installDir;
        };
    }

    private void ApplyBranding()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/startride_logo_full_dark.png", UriKind.Absolute);
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            BrandLogo.Source = image;
            Icon = image;
        }
        catch
        {
            // 图丢了不影响卸载
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Uninstall_OnClick(object sender, RoutedEventArgs e)
    {
        if (_running || _installDir == null) return;

        var confirm = MessageBox.Show(this,
            "确定要卸载 StartRide 吗？",
            "StartRide 卸载", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        _running = true;
        UninstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;

        try
        {
            UninstallEngine.Run(_installDir, PurgeDataCheck.IsChecked == true, m => StatusText.Text = m);
        }
        catch (Exception ex)
        {
            _running = false;
            UninstallButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            MessageBox.Show(this, "卸载过程中出错：\n\n" + ex.Message,
                "StartRide 卸载", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(this,
            "StartRide 已卸载。\n\n安装目录会在几秒内自动清理干净。",
            "StartRide 卸载", MessageBoxButton.OK, MessageBoxImage.Information);
        Application.Current.Shutdown();
    }
}

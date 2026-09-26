using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace StartRide.Setup;

public partial class InstallerWindow : Window
{
    private readonly InstallOptions _options = new();
    private CancellationTokenSource? _cts;
    private bool _installing;
    private bool _finished;
    private double _lastPercent;

    public InstallerWindow()
    {
        InitializeComponent();
        ApplyBranding();

        Loaded += (_, _) =>
        {
            PathInput.Text = _options.TargetDir;
            ResolveMode();
        };

        ProgressTrack.SizeChanged += (_, _) => ApplyFill(_lastPercent);
    }

    private void ApplyBranding()
    {
        try
        {
            BrandLogo.Source = LoadPng("Assets/startride_logo_full_dark.png");
            Icon = LoadPng("Assets/startride_icon_128.png");
        }
        catch
        {
            // 图丢了不影响安装，只是不好看
        }

        VersionLabel.Text = "版本 " + ProductInfo.Version;
        FooterHint.Text = Payload.DescribeSelf();
    }

    /// <summary>按"当前程序集"解析 pack URI。不写程序集名 —— 安装包与卸载器是两个不同的 AssemblyName，写死就有一个会找不到图。</summary>
    private static BitmapImage LoadPng(string relative)
    {
        var uri = new Uri("pack://application:,,,/" + relative, UriKind.Absolute);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>装过就写"升级"，没装过就写"安装"。</summary>
    private void ResolveMode()
    {
        string? installedVersion = ProductInfo.InstalledVersion();
        string? installedDir = ProductInfo.FindInstalledDir();

        if (installedDir != null)
        {
            _options.TargetDir = installedDir;
            PathInput.Text = installedDir;
        }

        if (!string.IsNullOrEmpty(installedVersion) && installedVersion != ProductInfo.Version)
        {
            WelcomeTitle.Text = "升级 StartRide";
            WelcomeLead.Text = $"检测到已安装 {installedVersion}，本次将升级到 {ProductInfo.Version}。" +
                               "安装位置与快捷方式沿用现有设置，你的账户、设置与存档不会被改动。";
            UpgradeHint.Text = $"将从 {installedVersion} 升级到 {ProductInfo.Version}";
            UpgradeHint.Visibility = Visibility.Visible;
            PrimaryButton.Content = "升级";
        }
        else if (!string.IsNullOrEmpty(installedVersion))
        {
            WelcomeTitle.Text = "重新安装 StartRide";
            WelcomeLead.Text = $"当前已经是 {ProductInfo.Version}。继续将用同一版本的文件覆盖安装，" +
                               "可用于修复缺失或损坏的文件。";
            PrimaryButton.Content = "重新安装";
        }
    }

    // ══════════════ 交互 ══════════════

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_installing && !_finished) CancelInstall();
        else Close();
    }

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 StartRide 的安装位置",
            InitialDirectory = Directory.Exists(PathInput.Text) ? PathInput.Text : ProductInfo.DefaultInstallDir,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) == true)
        {
            PathInput.Text = dialog.FolderName;
        }
    }

    private void DefaultPath_OnClick(object sender, RoutedEventArgs e)
        => PathInput.Text = ProductInfo.DefaultInstallDir;

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        if (_installing && !_finished) CancelInstall();
        else Close();
    }

    private void CancelInstall()
    {
        try { _cts?.Cancel(); } catch { }
        Close();
    }

    private async void Primary_OnClick(object sender, RoutedEventArgs e)
    {
        if (_finished)
        {
            FinishAndMaybeLaunch();
            return;
        }
        if (_installing) return;

        string target = PathInput.Text?.Trim() ?? "";
        if (target.Length == 0)
        {
            MessageBox.Show(this, "请先填写安装位置。", "StartRide 安装程序",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            target = Path.GetFullPath(target);
        }
        catch
        {
            MessageBox.Show(this, "安装位置不是有效的路径。", "StartRide 安装程序",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _options.TargetDir = target;
        _options.DesktopShortcut = DesktopCheck.IsChecked == true;
        _options.StartMenuShortcut = StartMenuCheck.IsChecked == true;
        _options.LaunchAfterwards = LaunchCheck.IsChecked == true;

        await RunInstallAsync();
    }

    private async System.Threading.Tasks.Task RunInstallAsync()
    {
        _installing = true;
        GoToStep(2);
        PanelWelcome.Visibility = Visibility.Collapsed;
        PanelDone.Visibility = Visibility.Collapsed;
        PanelProgress.Visibility = Visibility.Visible;
        PrimaryButton.IsEnabled = false;
        ProgressFile.Text = "";

        _cts = new CancellationTokenSource();
        var progress = new Progress<InstallProgress>(OnProgress);

        try
        {
            await InstallEngine.RunAsync(_options, progress, _cts.Token);
            _finished = true;

            GoToStep(3);
            PanelProgress.Visibility = Visibility.Collapsed;
            PanelDone.Visibility = Visibility.Visible;

            DoneLead.Text = "StartRide 已经装好了。可以直接从桌面快捷方式或开始菜单启动。";
            DonePath.Text = "安装位置：" + _options.TargetDir;
            FooterHint.Text = "安装位置：" + _options.TargetDir;

            PrimaryButton.Content = _options.LaunchAfterwards ? "完成并启动" : "完成";
            PrimaryButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            _installing = false;
            Close();
        }
        catch (Exception ex)
        {
            _installing = false;
            GoToStep(1);
            PanelProgress.Visibility = Visibility.Collapsed;
            PanelWelcome.Visibility = Visibility.Visible;
            PrimaryButton.IsEnabled = true;
            MessageBox.Show(this,
                "安装没有完成：\n\n" + ex.Message,
                "StartRide 安装程序", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnProgress(InstallProgress p)
    {
        _lastPercent = Math.Max(0, Math.Min(100, p.Percent));
        ApplyFill(_lastPercent);
        ProgressPercent.Text = ((int)Math.Round(_lastPercent)) + "%";
        ProgressFile.Text = p.Message;

        ProgressStage.Text = p.Stage switch
        {
            InstallStage.Preparing => "检查环境",
            InstallStage.Unpacking => "解包应用数据",
            InstallStage.Copying => "写入安装目录",
            InstallStage.Shortcuts => "创建快捷方式",
            InstallStage.Registering => "登记卸载信息",
            InstallStage.Finished => "完成",
            _ => "",
        };
    }

    private void ApplyFill(double percent)
    {
        double width = ProgressTrack.ActualWidth;
        if (width <= 0) return;
        ProgressFill.Width = Math.Max(0, width * percent / 100.0);
    }

    /// <summary>左侧步骤条的高亮切换。</summary>
    private void GoToStep(int step)
    {
        var active = (Border)FindName("Step" + step + "Mark");
        var activeNum = (System.Windows.Controls.TextBlock)FindName("Step" + step + "Num");
        var activeText = (System.Windows.Controls.TextBlock)FindName("Step" + step + "Text");

        active.Background = (Brush)FindResource("Brush.Accent");
        active.BorderThickness = new Thickness(0);
        activeNum.Foreground = Brushes.White;
        activeText.Foreground = (Brush)FindResource("Brush.Text");

        // 已走过的步骤打勾，没走到的保持灰。
        for (int i = 1; i <= 3; i++)
        {
            if (i == step) continue;
            var mark = (Border)FindName("Step" + i + "Mark");
            var num = (System.Windows.Controls.TextBlock)FindName("Step" + i + "Num");
            var text = (System.Windows.Controls.TextBlock)FindName("Step" + i + "Text");

            if (i < step)
            {
                mark.Background = (Brush)FindResource("Brush.Accent.Soft");
                mark.BorderBrush = (Brush)FindResource("Brush.Accent");
                mark.BorderThickness = new Thickness(1);
                num.Text = "✓";
                num.Foreground = (Brush)FindResource("Brush.Accent");
            }
            else
            {
                mark.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x21, 0x2B));
                mark.BorderBrush = (Brush)FindResource("Brush.Line");
                mark.BorderThickness = new Thickness(1);
                num.Text = i.ToString();
                num.Foreground = (Brush)FindResource("Brush.Text.Dim");
                text.Foreground = (Brush)FindResource("Brush.Text.Faint");
            }
        }
    }

    private void FinishAndMaybeLaunch()
    {
        if (_options.LaunchAfterwards)
        {
            try
            {
                string exe = ProductInfo.ExePathIn(_options.TargetDir);
                if (File.Exists(exe))
                {
                    Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = _options.TargetDir, UseShellExecute = true });
                }
            }
            catch
            {
                // 启动失败就只提示一次，不阻塞关闭
                MessageBox.Show(this, "没能自动启动，请手动打开 StartRide。",
                    "StartRide 安装程序", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        Close();
    }
}

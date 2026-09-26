using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        // 目标位置已经有 StartRide（或已有别的文件）时先问一句，见 ConfirmOverwrite
        if (!ConfirmOverwrite(target)) return;

        await RunInstallAsync();
    }

    /// <summary>
    /// 目标位置已经有 StartRide 时问一句"是否覆盖"。
    ///
    /// 为什么非要问：覆盖安装一直是"点完就没了"的重灾区，用户不知道自己要付出什么。
    /// 这里把两件事摊开讲清楚 ——
    ///   ① 会先**删掉**该目录下的程序文件（不清的话旧版本多出来的文件会永远留着）；
    ///   ② 用户自己的东西（Mods、账户、设置）不在这个目录里，不受影响。
    /// 返回 false = 用户点了"否"，整个安装中止，停在欢迎页。
    /// </summary>
    private bool ConfirmOverwrite(string target)
    {
        // 只有"看起来确实是 StartRide 安装目录"才允许清空；否则只做普通覆盖（绝不清空别人的目录）
        bool installDir = InstallEngine.LooksLikeInstallDirectory(target);

        bool nonEmpty;
        try
        {
            nonEmpty = Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any();
        }
        catch
        {
            nonEmpty = false;
        }

        if (!installDir && !nonEmpty) return true;      // 空目录 / 新目录：直接装，不打扰

        string title;
        string body;

        if (installDir)
        {
            string? installed = ProductInfo.InstalledVersion();
            string from = string.IsNullOrEmpty(installed) ? "" : $"（当前 {installed}）";
            title = "检测到已安装的 StartRide";
            body = "这个位置已经装了 StartRide" + from + "：\n\n"
                 + target + "\n\n"
                 + "继续将先删除该位置下的程序文件，再写入本次安装包提供的 "
                 + ProductInfo.Version + "。\n\n"
                 + "你的账户、设置、车辆与模组不在这个目录里，不会被删除。\n\n"
                 + "是否覆盖安装？";
        }
        else
        {
            title = "目标文件夹不是空的";
            body = "这个文件夹里已经有别的文件：\n\n"
                 + target + "\n\n"
                 + "继续会把 StartRide 写进去（同名文件会被覆盖），本次不会清空这个文件夹。\n\n"
                 + "是否继续？";
        }

        MessageBoxResult answer = MessageBox.Show(this, body, title,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return false;

        _options.ClearBeforeInstall = installDir;
        return true;
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
        var notes = new List<string>();

        try
        {
            await InstallEngine.RunAsync(_options, progress, _cts.Token, notes.Add);
            _finished = true;

            GoToStep(3);
            PanelProgress.Visibility = Visibility.Collapsed;
            PanelDone.Visibility = Visibility.Visible;

            string? warn = notes.FirstOrDefault(n => n.StartsWith("⚠"));
            DoneLead.Text = warn != null
                ? "StartRide 已经装好了。" + warn
                : "StartRide 已经装好了。桌面和开始菜单都能找到它，也可以直接从那儿启动。";
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
            InstallStage.Cleaning => "清理旧版本",
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

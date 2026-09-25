using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StartRide.Core;

namespace StartRide.App.Controls;

/// <summary>
/// 运行时从本机检测到的 BeamNG.drive 安装目录加载真实图标
/// （{GameDirectory}\icon-beamng.ico），替代上游打包的 Minecraft SVG 图标。
/// 若未检测到 BeamNG 或图标文件缺失，则回退为不显示（不报错、不崩）。
/// </summary>
public sealed class BeamNgIcon : Control
{
	public static readonly DependencyProperty IconSourceProperty;

	static BeamNgIcon()
	{
		IconSourceProperty = DependencyProperty.Register(
			nameof(IconSource),
			typeof(ImageSource),
			typeof(BeamNgIcon),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
	}

	/// <summary>已解析的真实图标（可为 null，此时控件不渲染内容）。</summary>
	public ImageSource? IconSource
	{
		get => (ImageSource?)GetValue(IconSourceProperty);
		private set => SetValue(IconSourceProperty, value);
	}

	public BeamNgIcon()
	{
		Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		Loaded -= OnLoaded;
		IconSource = TryLoadBeamNgIcon();
	}

	private static ImageSource? TryLoadBeamNgIcon()
	{
		try
		{
			string? dir = AppSettings.Load().GameDirectory;
			if (string.IsNullOrWhiteSpace(dir))
			{
				dir = AppSettings.DetectGameDirectory();
			}
			if (string.IsNullOrWhiteSpace(dir))
			{
				return null;
			}
			string ico = Path.Combine(dir, "icon-beamng.ico");
			if (!File.Exists(ico))
			{
				return null;
			}
			var decoder = new IconBitmapDecoder(
				new Uri(ico, UriKind.Absolute),
				BitmapCreateOptions.PreservePixelFormat,
				BitmapCacheOption.OnLoad);
			// 取最高分辨率那一帧
			BitmapFrame best = decoder.Frames[0];
			foreach (BitmapFrame frame in decoder.Frames)
			{
				if (frame.PixelWidth > best.PixelWidth)
				{
					best = frame;
				}
			}
			best.Freeze();
			return best;
		}
		catch
		{
			return null;
		}
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		ImageSource? src = IconSource;
		if (src == null || ActualWidth <= 0.0 || ActualHeight <= 0.0)
		{
			return;
		}
		double scale = Math.Min(ActualWidth / src.Width, ActualHeight / src.Height);
		double w = src.Width * scale;
		double h = src.Height * scale;
		double x = (ActualWidth - w) / 2.0;
		double y = (ActualHeight - h) / 2.0;
		drawingContext.DrawImage(src, new Rect(x, y, w, h));
	}
}

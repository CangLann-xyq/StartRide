using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Launcher.Domain.Models;

namespace StartRide.App.Controls.Account;

public sealed class SkinFrontPreviewControl : Image
{
	public static readonly DependencyProperty SkinSourceProperty;

	public static readonly DependencyProperty SkinModelProperty;

	public string? SkinSource
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(SkinSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SkinSourceProperty, (object)value);
		}
	}

	public MinecraftSkinModel? SkinModel
	{
		get
		{
			return (MinecraftSkinModel?)((DependencyObject)this).GetValue(SkinModelProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SkinModelProperty, (object)value);
		}
	}

	public SkinFrontPreviewControl()
	{
		base.Stretch = Stretch.Uniform;
		base.SnapsToDevicePixels = true;
		RenderOptions.SetBitmapScalingMode((DependencyObject)(object)this, BitmapScalingMode.NearestNeighbor);
	}

	private static void OnPreviewPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((SkinFrontPreviewControl)(object)d).RebuildPreview();
	}

	private void RebuildPreview()
	{
		if (string.IsNullOrWhiteSpace(SkinSource))
		{
			base.Source = null;
			return;
		}
		try
		{
			BitmapImage skin = SkinPreviewModelBuilder.LoadSkinBitmap(SkinSource);
			base.Source = SkinFrontPreviewRenderer.BuildFrontBitmap(skin, SkinModel);
		}
		catch
		{
			base.Source = null;
		}
	}

	static SkinFrontPreviewControl()
	{

		SkinSourceProperty = DependencyProperty.Register("SkinSource", typeof(string), typeof(SkinFrontPreviewControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
		SkinModelProperty = DependencyProperty.Register("SkinModel", typeof(MinecraftSkinModel?), typeof(SkinFrontPreviewControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
	}
}

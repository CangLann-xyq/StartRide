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
			BitmapImage skin = MinecraftSkinPreviewModelBuilder.LoadSkinBitmap(SkinSource);
			base.Source = MinecraftSkinFrontPreviewRenderer.BuildFrontBitmap(skin, SkinModel);
		}
		catch
		{
			base.Source = null;
		}
	}

	static SkinFrontPreviewControl()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		SkinSourceProperty = DependencyProperty.Register("SkinSource", typeof(string), typeof(SkinFrontPreviewControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
		SkinModelProperty = DependencyProperty.Register("SkinModel", typeof(MinecraftSkinModel?), typeof(SkinFrontPreviewControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
	}
}

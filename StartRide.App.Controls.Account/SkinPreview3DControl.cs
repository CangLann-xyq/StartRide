using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Launcher.Domain.Models;

namespace StartRide.App.Controls.Account;

public sealed class SkinPreview3DControl : Viewport3D
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

	public SkinPreview3DControl()
	{
		base.ClipToBounds = true;
		base.Camera = new PerspectiveCamera(new Point3D(0.0, 4.0, 52.0), new Vector3D(0.0, 0.0, -52.0), new Vector3D(0.0, 1.0, 0.0), 28.0);
	}

	private static void OnPreviewPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((SkinPreview3DControl)(object)d).RebuildPreview();
	}

	private void RebuildPreview()
	{
		base.Children.Clear();
		if (!string.IsNullOrWhiteSpace(SkinSource))
		{
			BitmapImage skin;
			try
			{
				skin = SkinPreviewModelBuilder.LoadSkinBitmap(SkinSource);
			}
			catch
			{
				return;
			}
			base.Children.Add(new ModelVisual3D
			{
				Content = new Model3DGroup
				{
					Children =
					{
						(Model3D)SkinPreviewModelBuilder.CreateAmbientLight(),
						(Model3D)SkinPreviewModelBuilder.CreateDirectionalLight(),
						(Model3D)SkinPreviewModelBuilder.BuildPlayerModel(skin, SkinModel)
					}
				}
			});
		}
	}

	static SkinPreview3DControl()
	{

		SkinSourceProperty = DependencyProperty.Register("SkinSource", typeof(string), typeof(SkinPreview3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
		SkinModelProperty = DependencyProperty.Register("SkinModel", typeof(MinecraftSkinModel?), typeof(SkinPreview3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
	}
}

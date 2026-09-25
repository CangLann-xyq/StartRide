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
				skin = MinecraftSkinPreviewModelBuilder.LoadSkinBitmap(SkinSource);
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
						(Model3D)MinecraftSkinPreviewModelBuilder.CreateAmbientLight(),
						(Model3D)MinecraftSkinPreviewModelBuilder.CreateDirectionalLight(),
						(Model3D)MinecraftSkinPreviewModelBuilder.BuildPlayerModel(skin, SkinModel)
					}
				}
			});
		}
	}

	static SkinPreview3DControl()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		SkinSourceProperty = DependencyProperty.Register("SkinSource", typeof(string), typeof(SkinPreview3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
		SkinModelProperty = DependencyProperty.Register("SkinModel", typeof(MinecraftSkinModel?), typeof(SkinPreview3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPreviewPropertyChanged)));
	}
}

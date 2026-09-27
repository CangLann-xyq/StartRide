using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace StartRide.App.Effects;

internal sealed class ProgressiveGaussianBlurEffect : ShaderEffect
{
	public static readonly DependencyProperty InputProperty;

	public static readonly DependencyProperty InputWidthProperty;

	public static readonly DependencyProperty InputHeightProperty;

	public static readonly DependencyProperty BlurLengthProperty;

	public static readonly DependencyProperty MaximumRadiusProperty;

	public static readonly DependencyProperty DirectionXProperty;

	public static readonly DependencyProperty DirectionYProperty;

	public Brush? Input
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(InputProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(InputProperty, (object)value);
		}
	}

	public double InputWidth
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(InputWidthProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(InputWidthProperty, (object)value);
		}
	}

	public double InputHeight
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(InputHeightProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(InputHeightProperty, (object)value);
		}
	}

	public double BlurLength
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(BlurLengthProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(BlurLengthProperty, (object)value);
		}
	}

	public double MaximumRadius
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(MaximumRadiusProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MaximumRadiusProperty, (object)value);
		}
	}

	public double DirectionX
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(DirectionXProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(DirectionXProperty, (object)value);
		}
	}

	public double DirectionY
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(DirectionYProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(DirectionYProperty, (object)value);
		}
	}

	private ProgressiveGaussianBlurEffect(PixelShader pixelShader)
	{
		base.PixelShader = pixelShader;
		UpdateShaderValue(InputProperty);
		UpdateShaderValue(InputWidthProperty);
		UpdateShaderValue(InputHeightProperty);
		UpdateShaderValue(BlurLengthProperty);
		UpdateShaderValue(MaximumRadiusProperty);
		UpdateShaderValue(DirectionXProperty);
		UpdateShaderValue(DirectionYProperty);
	}

	internal static bool TryCreate(double directionX, double directionY, out ProgressiveGaussianBlurEffect? effect, out Exception? exception)
	{
		if (!ProgressiveGaussianBlurShader.TryGet(out PixelShader shader, out exception) || shader == null)
		{
			effect = null;
			return false;
		}
		effect = new ProgressiveGaussianBlurEffect(shader)
		{
			DirectionX = directionX,
			DirectionY = directionY
		};
		return true;
	}

	private static DependencyProperty RegisterConstantProperty(string name, double defaultValue, int registerIndex, ValidateValueCallback validateValueCallback)
	{
		return DependencyProperty.Register(name, typeof(double), typeof(ProgressiveGaussianBlurEffect), (PropertyMetadata)(object)new UIPropertyMetadata(defaultValue, ShaderEffect.PixelShaderConstantCallback(registerIndex)), validateValueCallback);
	}

	private static bool IsFinite(object value)
	{
		return double.IsFinite((double)value);
	}

	private static bool IsPositiveFinite(object value)
	{
		double num = (double)value;
		if (double.IsFinite(num))
		{
			return num > 0.0;
		}
		return false;
	}

	private static bool IsNonNegativeFinite(object value)
	{
		double num = (double)value;
		if (double.IsFinite(num))
		{
			return num >= 0.0;
		}
		return false;
	}

	static ProgressiveGaussianBlurEffect()
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Expected O, but got Unknown
		InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(ProgressiveGaussianBlurEffect), 0, SamplingMode.Bilinear);
		InputWidthProperty = RegisterConstantProperty("InputWidth", 1.0, 0, new ValidateValueCallback(IsPositiveFinite));
		InputHeightProperty = RegisterConstantProperty("InputHeight", 1.0, 1, new ValidateValueCallback(IsPositiveFinite));
		BlurLengthProperty = RegisterConstantProperty("BlurLength", 0.0, 2, new ValidateValueCallback(IsNonNegativeFinite));
		MaximumRadiusProperty = RegisterConstantProperty("MaximumRadius", 24.0, 3, new ValidateValueCallback(IsNonNegativeFinite));
		DirectionXProperty = RegisterConstantProperty("DirectionX", 1.0, 4, new ValidateValueCallback(IsFinite));
		DirectionYProperty = RegisterConstantProperty("DirectionY", 0.0, 5, new ValidateValueCallback(IsFinite));
	}
}

using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StartRide.App.Views.Home;

public partial class HomePageView : UserControl, IComponentConnector
{
	private const double FallbackPanelWidth = 224.0;

	private const double FallbackPinnedContentGap = 24.0;

	private const double FallbackAnimationDurationMilliseconds = 320.0;

	private const double FallbackAnimationEasePower = 2.4;

	private static readonly Thickness FallbackPanelMargin;

	public static readonly DependencyProperty IsLaunchMenuPinnedProperty;

	private bool hasAppliedInitialContentAlignment;

	private int contentAnimationGeneration;

	public bool IsLaunchMenuPinned
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsLaunchMenuPinnedProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsLaunchMenuPinnedProperty, (object)value);
		}
	}

	public FrameworkElement RootElement => PageRoot;

	internal FrameworkElement ContentHostElement => HomeLaunchContentHost;

	internal TranslateTransform ContentTranslateTransform => HomeLaunchContentTranslate;

	internal double PinnedContentOffsetX => CalculatePinnedContentOffsetX();

	public HomePageView()
	{
		InitializeComponent();
		SetBinding(IsLaunchMenuPinnedProperty, new Binding("IsLaunchMenuPinned"));
		base.Loaded += OnLoaded;
	}

	private static void OnIsLaunchMenuPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is HomePageView homePageView)
		{
			homePageView.ApplyContentAlignment(homePageView.IsLoaded && homePageView.hasAppliedInitialContentAlignment);
		}
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		ApplyContentAlignment(animate: false);
		hasAppliedInitialContentAlignment = true;
	}

	private void ApplyContentAlignment(bool animate)
	{
		int generation = ++contentAnimationGeneration;
		double to = (IsLaunchMenuPinned ? CalculatePinnedContentOffsetX() : 0.0);
		AnimateDouble((DependencyObject)(object)HomeLaunchContentTranslate, TranslateTransform.XProperty, to, animate, generation);
	}

	private double CalculatePinnedContentOffsetX()
	{
		Thickness panelMargin = GetPanelMargin();
		double resourceDouble = GetResourceDouble("HomeLaunchMenuPanelWidth", 224.0);
		double resourceDouble2 = GetResourceDouble("HomeLaunchPinnedContentGap", 24.0);
		return (panelMargin.Left + resourceDouble + resourceDouble2) / 2.0;
	}

	private void AnimateDouble(DependencyObject target, DependencyProperty property, double to, bool animate, int generation)
	{
		IAnimatable animatable = target as IAnimatable;
		if (animatable == null)
		{
			target.SetValue(property, (object)to);
			return;
		}
		double currentDouble = GetCurrentDouble(target, property);
		animatable.BeginAnimation(property, null);
		target.SetValue(property, (object)currentDouble);
		if (!animate || Math.Abs(currentDouble - to) < 0.1)
		{
			target.SetValue(property, (object)to);
			return;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			From = currentDouble,
			To = to,
			Duration = GetAnimationDuration(),
			FillBehavior = FillBehavior.Stop,
			EasingFunction = CreateAnimationEasing()
		};
		doubleAnimation.Completed += delegate
		{
			if (generation == contentAnimationGeneration)
			{
				animatable.BeginAnimation(property, null);
				target.SetValue(property, (object)to);
			}
		};
		animatable.BeginAnimation(property, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
	}

	private static double GetCurrentDouble(DependencyObject target, DependencyProperty property)
	{
		double num = (double)target.GetValue(property);
		if (!double.IsNaN(num))
		{
			return num;
		}
		return 0.0;
	}

	private Duration GetAnimationDuration()
	{
		return new Duration(TimeSpan.FromMilliseconds(GetResourceDouble("HomeLaunchMenuAnimationDurationMilliseconds", 320.0)));
	}

	private IEasingFunction CreateAnimationEasing()
	{
		return new PowerEase
		{
			Power = GetResourceDouble("HomeLaunchMenuAnimationEasePower", 2.4),
			EasingMode = EasingMode.EaseOut
		};
	}

	private Thickness GetPanelMargin()
	{
		object obj = TryFindResource("HomeLaunchMenuPanelMargin");
		if (obj is Thickness)
		{
			return (Thickness)obj;
		}
		return FallbackPanelMargin;
	}

	private double GetResourceDouble(string key, double fallback)
	{
		object obj = TryFindResource(key);
		if (obj is double)
		{
			return (double)obj;
		}
		return fallback;
	}


	static HomePageView()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		FallbackPanelMargin = new Thickness(24.0, 24.0, 0.0, 24.0);
		IsLaunchMenuPinnedProperty = DependencyProperty.Register("IsLaunchMenuPinned", typeof(bool), typeof(HomePageView), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsLaunchMenuPinnedChanged)));
	}
}

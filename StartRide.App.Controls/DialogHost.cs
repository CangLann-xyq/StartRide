using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using StartRide.App.Services;

namespace StartRide.App.Controls;

[ContentProperty("DialogContent")]
public partial class DialogHost : UserControl, IComponentConnector
{
	private static readonly Duration FadeInDuration;

	private static readonly Duration FadeOutDuration;

	public static readonly DependencyProperty DialogWidthProperty;

	public static readonly DependencyProperty DialogContentProperty;

	public static readonly DependencyProperty IsOpenProperty;

	public static readonly DependencyProperty UseIntegratedOverlayProperty;

	private DialogOverlayService? integratedOverlayService;

	private Window? ownerWindow;

	private bool suppressIsOpenChanged;

	public double DialogWidth
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(DialogWidthProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(DialogWidthProperty, (object)value);
		}
	}

	public object? DialogContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(DialogContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(DialogContentProperty, value);
		}
	}

	public bool IsOpen
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsOpenProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsOpenProperty, (object)value);
		}
	}

	public bool UseIntegratedOverlay
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(UseIntegratedOverlayProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(UseIntegratedOverlayProperty, (object)value);
		}
	}

	public bool IsSizeAnimating => integratedOverlayService?.IsSizeAnimating ?? false;

	public Grid OverlayRoot => RootOverlay;

	public Border SurfaceBorder => Surface;

	public DialogHost()
	{
		InitializeComponent();
		base.Loaded += DialogHost_Loaded;
		base.Unloaded += DialogHost_Unloaded;
	}

	public void Show()
	{
		SetIsOpenValue(value: true);
		SetIsOpenCore(isOpen: true);
	}

	public void Hide(Action? completed = null)
	{
		SetIsOpenValue(value: false);
		SetIsOpenCore(isOpen: false, completed);
	}

	public void Prewarm()
	{
		if (EnsureIntegratedOverlayService())
		{
			integratedOverlayService.Prewarm(this);
		}
	}

	public void AnimateSizeChange(double previousHeight)
	{
		if (EnsureIntegratedOverlayService())
		{
			integratedOverlayService.AnimateSizeChange(this, previousHeight);
		}
	}

	private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is DialogHost { suppressIsOpenChanged: false } dialogHost)
		{
			dialogHost.SetIsOpenCore((bool)e.NewValue);
		}
	}

	private static void OnIntegratedOverlayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is DialogHost { IsLoaded: not false } dialogHost))
		{
			return;
		}
		dialogHost.ResetIntegratedOverlayService();
		if (dialogHost.UseIntegratedOverlay)
		{
			dialogHost.EnsureIntegratedOverlayService();
			if (dialogHost.IsOpen)
			{
				dialogHost.SetIsOpenCore(isOpen: true);
			}
		}
	}

	private void DialogHost_Loaded(object sender, RoutedEventArgs e)
	{
		if (UseIntegratedOverlay)
		{
			EnsureIntegratedOverlayService();
			Prewarm();
			if (IsOpen)
			{
				SetIsOpenCore(isOpen: true);
			}
		}
	}

	private void DialogHost_Unloaded(object sender, RoutedEventArgs e)
	{
		ResetIntegratedOverlayService();
	}

	private void SetIsOpenValue(bool value)
	{
		suppressIsOpenChanged = true;
		((DependencyObject)this).SetCurrentValue(IsOpenProperty, (object)value);
		suppressIsOpenChanged = false;
	}

	private void SetIsOpenCore(bool isOpen, Action? completed = null)
	{
		if (UseIntegratedOverlay && EnsureIntegratedOverlayService())
		{
			if (isOpen)
			{
				integratedOverlayService.Show(this);
			}
			else
			{
				integratedOverlayService.Hide(this, completed);
			}
			return;
		}
		OverlayRoot.BeginAnimation(UIElement.OpacityProperty, null);
		if (isOpen)
		{
			OverlayRoot.Visibility = Visibility.Visible;
			OverlayRoot.Opacity = 0.0;
			OverlayRoot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, FadeInDuration)
			{
				FillBehavior = FillBehavior.Stop
			});
			OverlayRoot.Opacity = 1.0;
			return;
		}
		double opacity = OverlayRoot.Opacity;
		if (opacity <= 0.0 || OverlayRoot.Visibility != Visibility.Visible)
		{
			OverlayRoot.Visibility = Visibility.Collapsed;
			OverlayRoot.Opacity = 0.0;
			completed?.Invoke();
			return;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation(opacity, 0.0, FadeOutDuration)
		{
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation.Completed += delegate
		{
			OverlayRoot.Visibility = Visibility.Collapsed;
			OverlayRoot.Opacity = 0.0;
			completed?.Invoke();
		};
		OverlayRoot.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private bool EnsureIntegratedOverlayService()
	{
		if (!UseIntegratedOverlay)
		{
			return false;
		}
		if (integratedOverlayService != null)
		{
			return true;
		}
		ownerWindow = Window.GetWindow((DependencyObject)(object)this);
		if (ownerWindow == null)
		{
			return false;
		}
		integratedOverlayService = new DialogOverlayService(ownerWindow);
		return true;
	}

	private void ResetIntegratedOverlayService()
	{
		if (ownerWindow != null)
		{
			ownerWindow = null;
		}
		integratedOverlayService = null;
	}


	static DialogHost()
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Expected O, but got Unknown
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Expected O, but got Unknown
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Expected O, but got Unknown
		FadeInDuration = TimeSpan.FromMilliseconds(140.0);
		FadeOutDuration = TimeSpan.FromMilliseconds(180.0);
		DialogWidthProperty = DependencyProperty.Register("DialogWidth", typeof(double), typeof(DialogHost), new PropertyMetadata((object)420.0));
		DialogContentProperty = DependencyProperty.Register("DialogContent", typeof(object), typeof(DialogHost), new PropertyMetadata((PropertyChangedCallback)null));
		IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(DialogHost), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsOpenChanged)));
		UseIntegratedOverlayProperty = DependencyProperty.Register("UseIntegratedOverlay", typeof(bool), typeof(DialogHost), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIntegratedOverlayPropertyChanged)));
	}
}

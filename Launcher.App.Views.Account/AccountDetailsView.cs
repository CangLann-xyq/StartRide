using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Launcher.App.Controls;
using Launcher.App.Services;
using Launcher.App.ViewModels.Account;

namespace Launcher.App.Views.Account;

public partial class AccountDetailsView : UserControl, IComponentConnector
{
	public static readonly DependencyProperty IsProgressiveBlurEnabledProperty;

	private readonly PageTransitionService accountTransitionService;

	private readonly ProgressiveBlurBandController? progressiveBlurController;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private string? currentAccountToken;

	public bool IsProgressiveBlurEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsProgressiveBlurEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsProgressiveBlurEnabledProperty, (object)value);
		}
	}

	internal FrameworkElement ProgressiveBlurLayerElement => PART_ProgressiveBlurLayer;

	internal FrameworkElement ProgressiveBlurVisualSourceElement => PART_ProgressiveBlurVisualSource;

	internal FrameworkElement ProgressiveBlurDirectHostElement => PART_ProgressiveBlurDirectHost;

	internal FrameworkElement ProgressiveBlurViewportElement => PART_ProgressiveBlurViewport;

	internal FrameworkElement ProgressiveBlurUpscaleHostElement => PART_ProgressiveBlurUpscaleHost;

	internal ScaleTransform ProgressiveBlurUpscaleTransform => PART_ProgressiveBlurUpscaleTransform;

	internal FrameworkElement ProgressiveBlurHorizontalHostElement => PART_ProgressiveBlurHorizontalHost;

	internal FrameworkElement ProgressiveBlurVerticalHostElement => PART_ProgressiveBlurVerticalHost;

	internal VisualBrush ProgressiveBlurBrush => PART_ProgressiveBlurBrush;

	internal ScrollViewer DetailsScrollViewerElement => AccountDetailsScrollViewer;

	public AccountDetailsView()
	{
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		InitializeComponent();
		progressiveBlurController = new ProgressiveBlurBandController(new ProgressiveBlurVisualParts(this, PART_ProgressiveBlurLayer, PART_ProgressiveBlurVisualSource, PART_ProgressiveBlurDirectHost, PART_ProgressiveBlurViewport, PART_ProgressiveBlurUpscaleHost, PART_ProgressiveBlurUpscaleTransform, PART_ProgressiveBlurHorizontalHost, PART_ProgressiveBlurVerticalHost, PART_ProgressiveBlurBrush), () => base.IsVisible && IsProgressiveBlurEnabled);
		accountTransitionService = PageTransitionService.CreateWithDynamicOrder(((DispatcherObject)this).Dispatcher, (string _) => DetailsContentRoot, GetCurrentAccountToken(), GetAccountOrder);
		base.Loaded += AccountDetailsView_Loaded;
		base.Unloaded += AccountDetailsView_Unloaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(AccountDetailsView_DataContextChanged);
		PART_ProgressiveBlurLayer.MouseWheel += ProgressiveBlurLayer_MouseWheel;
	}

	public void ScrollToTop()
	{
		AccountDetailsScrollViewer.ScrollToTop();
	}

	private void AccountDetailsView_Loaded(object sender, RoutedEventArgs e)
	{
		progressiveBlurController?.OnLoaded();
		currentAccountToken = GetCurrentAccountToken();
		accountTransitionService.SyncTo(currentAccountToken);
		ResetContentPresentation();
	}

	private void AccountDetailsView_Unloaded(object sender, RoutedEventArgs e)
	{
		progressiveBlurController?.OnUnloaded();
	}

	private static void OnProgressiveBlurEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is AccountDetailsView accountDetailsView)
		{
			bool becameEnabled = !(bool)e.OldValue && (bool)e.NewValue;
			accountDetailsView.progressiveBlurController?.OnEnabledChanged(becameEnabled);
		}
	}

	private void ProgressiveBlurLayer_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (!e.Handled && IsProgressiveBlurEnabled && e.OriginalSource == PART_ProgressiveBlurLayer)
		{
			MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
			{
				RoutedEvent = UIElement.MouseWheelEvent,
				Source = AccountDetailsScrollViewer
			};
			AccountDetailsScrollViewer.RaiseEvent(e2);
			e.Handled = e2.Handled;
		}
	}

	private void AccountDetailsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= AccountDetailsViewModel_PropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += AccountDetailsViewModel_PropertyChanged;
		}
		currentAccountToken = GetCurrentAccountToken();
		accountTransitionService.SyncTo(currentAccountToken);
		ResetContentPresentation();
	}

	private void AccountDetailsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedAccount")
		{
			string text = GetCurrentAccountToken();
			if (string.IsNullOrWhiteSpace(text))
			{
				currentAccountToken = null;
				accountTransitionService.SyncTo(null);
				ResetContentPresentation();
			}
			else if (!string.Equals(currentAccountToken, text, StringComparison.Ordinal))
			{
				currentAccountToken = text;
				ScrollToTop();
				AccountDetailsScrollViewer.UpdateLayout();
				DetailsContentRoot.UpdateLayout();
				accountTransitionService.MoveTo(text);
			}
		}
	}

	private string? GetCurrentAccountToken()
	{
		return (base.DataContext as AccountDetailsViewModel)?.SelectedAccount?.Id;
	}

	private IReadOnlyList<string> GetAccountOrder()
	{
		if (!(base.DataContext is AccountDetailsViewModel accountDetailsViewModel))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		list.AddRange(accountDetailsViewModel.AccountList.Accounts.Select((AccountItemViewModel account) => account.Id));
		return new _003C_003Ez__ReadOnlyList<string>(list);
	}

	private void ResetContentPresentation()
	{
		DetailsContentRoot.BeginAnimation(UIElement.OpacityProperty, null);
		DetailsContentRoot.Opacity = 1.0;
		TranslateTransform translateTransform = EnsureTranslateTransform();
		translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		translateTransform.Y = 0.0;
	}

	private TranslateTransform EnsureTranslateTransform()
	{
		if (DetailsContentRoot.RenderTransform is TranslateTransform result)
		{
			return result;
		}
		TranslateTransform translateTransform = new TranslateTransform();
		DetailsContentRoot.RenderTransform = translateTransform;
		return translateTransform;
	}


	static AccountDetailsView()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		IsProgressiveBlurEnabledProperty = DependencyProperty.Register("IsProgressiveBlurEnabled", typeof(bool), typeof(AccountDetailsView), new PropertyMetadata((object)false, new PropertyChangedCallback(OnProgressiveBlurEnabledChanged)));
	}
}

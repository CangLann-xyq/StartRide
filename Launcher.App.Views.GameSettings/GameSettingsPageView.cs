using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using Launcher.App.Services;
using Launcher.App.Utilities;
using Launcher.App.ViewModels.GameSettings;

namespace Launcher.App.Views.GameSettings;

public partial class GameSettingsPageView : UserControl, IComponentConnector
{
	private readonly SlidingContentTransitionCoordinator stepTransition;

	private SlidingContentTransitionCoordinator? secondaryMenuTransition;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private readonly DispatcherTimer memoryRefreshTimer;

	private bool isWaitingForSecondaryMenuTransition;

	public FrameworkElement RootElement => PageRoot;

	public GameSettingsPageView()
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		InitializeComponent();
		FrameworkElement frameworkElement = FindStepHost();
		stepTransition = new SlidingContentTransitionCoordinator(this, frameworkElement, FindStepContent<FrameworkElement>((DependencyObject)(object)frameworkElement, "InstanceListStep", "Instance list step was not found."), FindStepContent<FrameworkElement>((DependencyObject)(object)frameworkElement, "InstanceDetailsStep", "Instance details step was not found."));
		base.Loaded += GameSettingsPageView_Loaded;
		base.Unloaded += GameSettingsPageView_Unloaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(GameSettingsPageView_DataContextChanged);
		memoryRefreshTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		memoryRefreshTimer.Tick += MemoryRefreshTimer_Tick;
	}

	private void GameSettingsPageView_Loaded(object sender, RoutedEventArgs e)
	{
		stepTransition.Sync(IsDetailsStep());
		EnsureSecondaryMenuTransition();
		secondaryMenuTransition?.Sync(IsDetailsStep());
		RefreshMemorySnapshot();
		memoryRefreshTimer.Start();
	}

	private void GameSettingsPageView_Unloaded(object sender, RoutedEventArgs e)
	{
		memoryRefreshTimer.Stop();
	}

	private void MemoryRefreshTimer_Tick(object? sender, EventArgs e)
	{
		RefreshMemorySnapshot();
	}

	private void RefreshMemorySnapshot()
	{
		if (base.DataContext is GameSettingsPageViewModel gameSettingsPageViewModel)
		{
			gameSettingsPageViewModel.Details.Launch.RefreshSystemMemorySnapshot();
		}
	}

	private void GameSettingsPageView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= GameSettingsPageViewModel_PropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += GameSettingsPageViewModel_PropertyChanged;
		}
		stepTransition.Sync(IsDetailsStep());
		EnsureSecondaryMenuTransition();
		secondaryMenuTransition?.Sync(IsDetailsStep());
	}

	private void GameSettingsPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "CurrentStep" && sender is GameSettingsPageViewModel gameSettingsPageViewModel)
		{
			EnsureSecondaryMenuTransition();
			stepTransition.AnimateTo(gameSettingsPageViewModel.CurrentStep == GameSettingsPageStep.Details);
			secondaryMenuTransition?.AnimateTo(gameSettingsPageViewModel.CurrentStep == GameSettingsPageStep.Details);
		}
	}

	private void EnsureSecondaryMenuTransition()
	{
		if (secondaryMenuTransition != null)
		{
			if (isWaitingForSecondaryMenuTransition)
			{
				base.LayoutUpdated -= GameSettingsPageView_LayoutUpdated;
				isWaitingForSecondaryMenuTransition = false;
			}
			return;
		}
		FrameworkElement frameworkElement = TryFindStepContent<FrameworkElement>((DependencyObject)(object)this, "SecondaryMenuStepHost");
		if (frameworkElement == null)
		{
			WaitForSecondaryMenuTransition();
			return;
		}
		FrameworkElement frameworkElement2 = TryFindStepContent<FrameworkElement>((DependencyObject)(object)frameworkElement, "InstanceCategoryMenuLayer");
		FrameworkElement frameworkElement3 = TryFindStepContent<FrameworkElement>((DependencyObject)(object)frameworkElement, "DetailsSectionMenuLayer");
		if (frameworkElement2 == null || frameworkElement3 == null)
		{
			WaitForSecondaryMenuTransition();
			return;
		}
		secondaryMenuTransition = new SlidingContentTransitionCoordinator(this, frameworkElement, frameworkElement2, frameworkElement3, null, useSlideTransition: false, useScaleTransition: true, 0.96);
		secondaryMenuTransition.Sync(IsDetailsStep());
		if (isWaitingForSecondaryMenuTransition)
		{
			base.LayoutUpdated -= GameSettingsPageView_LayoutUpdated;
			isWaitingForSecondaryMenuTransition = false;
		}
	}

	private void WaitForSecondaryMenuTransition()
	{
		if (!isWaitingForSecondaryMenuTransition)
		{
			base.LayoutUpdated += GameSettingsPageView_LayoutUpdated;
			isWaitingForSecondaryMenuTransition = true;
		}
	}

	private void GameSettingsPageView_LayoutUpdated(object? sender, EventArgs e)
	{
		EnsureSecondaryMenuTransition();
	}

	private bool IsDetailsStep()
	{
		if (base.DataContext is GameSettingsPageViewModel gameSettingsPageViewModel)
		{
			return gameSettingsPageViewModel.CurrentStep == GameSettingsPageStep.Details;
		}
		return false;
	}

	private FrameworkElement FindStepHost()
	{
		return (GameSettingsListFrame.ListContent as FrameworkElement) ?? throw new InvalidOperationException("Game settings step host content is not available.");
	}

	private static T FindStepContent<T>(DependencyObject root, string tag, string errorMessage) where T : FrameworkElement
	{
		return TryFindStepContent<T>(root, tag) ?? throw new InvalidOperationException(errorMessage);
	}

	private static T? TryFindStepContent<T>(DependencyObject root, string tag) where T : FrameworkElement
	{
		return VisualTreeSearch.FindDescendant(root, (T element) => object.Equals(element.Tag, tag));
	}

}

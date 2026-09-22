using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Launcher.App.Services;
using Launcher.App.ViewModels.Settings;

namespace Launcher.App.Views.Settings;

public partial class SettingsPageView : UserControl, IComponentConnector
{
	private static readonly string[] SectionOrder = new string[7] { "General", "Download", "Language", "LaunchMemory", "Java", "Theme", "Info" };

	private readonly DispatcherTimer memoryRefreshTimer;

	private readonly PageTransitionService sectionTransitionService;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private FrameworkElement? sectionContentRoot;

	public FrameworkElement RootElement => PageRoot;

	public SettingsPageView()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		memoryRefreshTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		InitializeComponent();
		sectionTransitionService = new PageTransitionService(((DispatcherObject)this).Dispatcher, (string _) => sectionContentRoot, GetCurrentSectionId(), SectionOrder);
		memoryRefreshTimer.Tick += MemoryRefreshTimer_Tick;
		base.Loaded += SettingsPageView_Loaded;
		base.Unloaded += SettingsPageView_Unloaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(SettingsPageView_DataContextChanged);
	}

	private void SettingsPageView_Loaded(object sender, RoutedEventArgs e)
	{
		RefreshMemorySnapshot();
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
		memoryRefreshTimer.Start();
	}

	private void SettingsPageView_Unloaded(object sender, RoutedEventArgs e)
	{
		memoryRefreshTimer.Stop();
	}

	private void MemoryRefreshTimer_Tick(object? sender, EventArgs e)
	{
		RefreshMemorySnapshot();
	}

	private void RefreshMemorySnapshot()
	{
		if (base.DataContext is SettingsPageViewModel settingsPageViewModel)
		{
			settingsPageViewModel.LaunchMemory.RefreshSystemMemorySnapshot();
		}
	}

	private void SettingsPageView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= SettingsPageViewModel_PropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += SettingsPageViewModel_PropertyChanged;
		}
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
	}

	private void SettingsPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedSection" && sender is SettingsPageViewModel settingsPageViewModel && sectionContentRoot != null)
		{
			SettingsListFrame.ScrollViewer.ScrollToVerticalOffset(0.0);
			SettingsListFrame.ScrollViewer.UpdateLayout();
			sectionContentRoot.UpdateLayout();
			sectionTransitionService.MoveTo(settingsPageViewModel.SelectedSection?.Section.ToString() ?? SectionOrder[0]);
		}
	}

	private void ResetSectionPresentation()
	{
		if (sectionContentRoot != null)
		{
			sectionContentRoot.BeginAnimation(UIElement.OpacityProperty, null);
			sectionContentRoot.Opacity = 1.0;
			TranslateTransform translateTransform = EnsureTranslateTransform();
			translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			translateTransform.Y = 0.0;
		}
	}

	private TranslateTransform EnsureTranslateTransform()
	{
		if (sectionContentRoot?.RenderTransform is TranslateTransform result)
		{
			return result;
		}
		TranslateTransform translateTransform = new TranslateTransform();
		if (sectionContentRoot != null)
		{
			sectionContentRoot.RenderTransform = translateTransform;
		}
		return translateTransform;
	}

	private string GetCurrentSectionId()
	{
		return (base.DataContext as SettingsPageViewModel)?.SelectedSection?.Section.ToString() ?? SectionOrder[0];
	}

	private void SectionContentRoot_OnLoaded(object sender, RoutedEventArgs e)
	{
		sectionContentRoot = sender as FrameworkElement;
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
	}

	private void SectionContentRoot_OnUnloaded(object sender, RoutedEventArgs e)
	{
		if (sectionContentRoot == sender)
		{
			sectionContentRoot = null;
		}
	}

}

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
using Launcher.App.Utilities;
using Launcher.App.ViewModels.Resources;

namespace Launcher.App.Views.Resources;

public partial class ResourcesPageView : UserControl, IComponentConnector
{
	private static readonly string[] SectionOrder = new string[5] { "mods", "resource_packs", "shader_packs", "worlds", "modpacks" };

	private readonly PageTransitionService sectionTransitionService;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private FrameworkElement? sectionContentRoot;

	private bool isEnsureCurrentSectionLoadedQueued;

	public FrameworkElement RootElement => PageRoot;

	public ResourcesPageView()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		InitializeComponent();
		sectionTransitionService = new PageTransitionService(((DispatcherObject)this).Dispatcher, (string _) => sectionContentRoot, GetCurrentSectionId(), SectionOrder);
		base.Loaded += ResourcesPageView_Loaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(ResourcesPageView_DataContextChanged);
		base.IsVisibleChanged += new DependencyPropertyChangedEventHandler(ResourcesPageView_IsVisibleChanged);
	}

	private void ResourcesPageView_Loaded(object sender, RoutedEventArgs e)
	{
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
		QueueEnsureCurrentSectionLoadedIfVisible();
	}

	private void ResourcesPageView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= ResourcesPageViewModel_PropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += ResourcesPageViewModel_PropertyChanged;
		}
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
		QueueEnsureCurrentSectionLoadedIfVisible();
	}

	private void ResourcesPageView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		QueueEnsureCurrentSectionLoadedIfVisible();
	}

	private void ResourcesPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedSection" && sender is ResourcesPageViewModel resourcesPageViewModel && sectionContentRoot != null)
		{
			sectionContentRoot.UpdateLayout();
			ResetCurrentSectionScrollPosition();
			sectionTransitionService.MoveTo(resourcesPageViewModel.SelectedSection?.Id ?? SectionOrder[0]);
		}
	}

	private void ResetCurrentSectionScrollPosition()
	{
		if (sectionContentRoot == null)
		{
			return;
		}
		ResourcesModPageView resourcesModPageView = VisualTreeSearch.FindDescendant((DependencyObject)(object)sectionContentRoot, (ResourcesModPageView _) => true);
		if (resourcesModPageView == null)
		{
			return;
		}
		try
		{
			resourcesModPageView.ScrollViewer.ScrollToVerticalOffset(0.0);
			resourcesModPageView.RefreshViewport();
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void QueueEnsureCurrentSectionLoadedIfVisible()
	{
		if (!base.IsVisible || isEnsureCurrentSectionLoadedQueued || !(base.DataContext is ResourcesPageViewModel))
		{
			return;
		}
		isEnsureCurrentSectionLoadedQueued = true;
		UiTransitionGate.RunWhenIdle(delegate
		{
			isEnsureCurrentSectionLoadedQueued = false;
			if (base.IsVisible && base.DataContext is ResourcesPageViewModel resourcesPageViewModel)
			{
				UpdateLayout();
				ResetCurrentSectionScrollPosition();
				resourcesPageViewModel.BeginEnsureCurrentSectionLoaded();
			}
		});
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
		return (base.DataContext as ResourcesPageViewModel)?.SelectedSection?.Id ?? SectionOrder[0];
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

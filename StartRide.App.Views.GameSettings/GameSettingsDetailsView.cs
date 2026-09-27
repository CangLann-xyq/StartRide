using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using StartRide.App.Services;
using StartRide.App.ViewModels.GameSettings;

namespace StartRide.App.Views.GameSettings;

public partial class GameSettingsDetailsView : UserControl, IComponentConnector
{
	private static readonly string[] SectionOrder = new string[9] { "general", "launch", "java", "mod_management", "saves", "resource_packs", "shaders", "backup", "export" };

	private INotifyPropertyChanged? currentViewModelNotifier;

	private readonly PageTransitionService sectionTransitionService;

	internal ScrollViewer ScrollViewerControl => DetailsScrollViewer;

	public GameSettingsDetailsView()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		InitializeComponent();
		sectionTransitionService = new PageTransitionService(((DispatcherObject)this).Dispatcher, (string _) => GetCurrentTransitionRoot(), GetCurrentSectionId(), SectionOrder);
		base.Loaded += GameSettingsDetailsView_Loaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(GameSettingsDetailsView_DataContextChanged);
	}

	private void GameSettingsDetailsView_Loaded(object sender, RoutedEventArgs e)
	{
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
	}

	private void GameSettingsDetailsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= GameSettingsDetailsViewModel_PropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += GameSettingsDetailsViewModel_PropertyChanged;
		}
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
	}

	private void GameSettingsDetailsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedSection" && sender is GameSettingsDetailsViewModel gameSettingsDetailsViewModel)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				DetailsScrollViewer.ScrollToVerticalOffset(0.0);
			}, (DispatcherPriority)4, Array.Empty<object>());
			sectionTransitionService.MoveTo(gameSettingsDetailsViewModel.SelectedSection?.Id ?? SectionOrder[0]);
		}
	}

	private void ResetSectionPresentation()
	{
		ResetTransitionElement(SectionContentRoot);
		ResetTransitionElement(FullViewportSectionContentRoot);
	}

	private FrameworkElement GetCurrentTransitionRoot()
	{
		if ((!((base.DataContext as GameSettingsDetailsViewModel)?.CurrentSectionViewModel?.UsesFullViewportLayout)) ?? true)
		{
			return SectionContentRoot;
		}
		return FullViewportSectionContentRoot;
	}

	private static void ResetTransitionElement(FrameworkElement element)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.Opacity = 1.0;
		TranslateTransform translateTransform = EnsureTranslateTransform(element);
		translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		translateTransform.Y = 0.0;
	}

	private static TranslateTransform EnsureTranslateTransform(FrameworkElement element)
	{
		if (element.RenderTransform is TranslateTransform result)
		{
			return result;
		}
		return (TranslateTransform)(element.RenderTransform = new TranslateTransform());
	}

	private string GetCurrentSectionId()
	{
		return (base.DataContext as GameSettingsDetailsViewModel)?.SelectedSection?.Id ?? SectionOrder[0];
	}

}

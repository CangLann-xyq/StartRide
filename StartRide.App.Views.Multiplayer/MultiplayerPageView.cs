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
using StartRide.App.ViewModels.Multiplayer;

namespace StartRide.App.Views.Multiplayer;

public partial class MultiplayerPageView : UserControl, IComponentConnector
{
	private static readonly string[] SectionOrder = new string[2] { "CreateLobby", "JoinLobby" };

	private readonly PageTransitionService sectionTransitionService;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private FrameworkElement? sectionContentRoot;

	public FrameworkElement RootElement => PageRoot;

	public MultiplayerPageView()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		InitializeComponent();
		sectionTransitionService = new PageTransitionService(((DispatcherObject)this).Dispatcher, (string _) => sectionContentRoot, GetCurrentSectionId(), SectionOrder);
		base.Loaded += MultiplayerPageView_Loaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(MultiplayerPageView_DataContextChanged);
	}

	private void MultiplayerPageView_Loaded(object sender, RoutedEventArgs e)
	{
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
	}

	private void MultiplayerPageView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= MultiplayerPageViewModel_PropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += MultiplayerPageViewModel_PropertyChanged;
		}
		sectionTransitionService.SyncTo(GetCurrentSectionId());
		ResetSectionPresentation();
	}

	private void MultiplayerPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedSection" && sender is MultiplayerPageViewModel multiplayerPageViewModel && sectionContentRoot != null)
		{
			MultiplayerListFrame.ScrollViewer.ScrollToVerticalOffset(0.0);
			MultiplayerListFrame.ScrollViewer.UpdateLayout();
			sectionContentRoot.UpdateLayout();
			sectionTransitionService.MoveTo(multiplayerPageViewModel.SelectedSection?.Section.ToString() ?? SectionOrder[0]);
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
		return (base.DataContext as MultiplayerPageViewModel)?.SelectedSection?.Section.ToString() ?? SectionOrder[0];
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

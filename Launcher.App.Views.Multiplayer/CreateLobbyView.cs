using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Launcher.App.Services;
using Launcher.App.ViewModels.Multiplayer;

namespace Launcher.App.Views.Multiplayer;

public partial class CreateLobbyView : UserControl, IComponentConnector
{
	private readonly SlidingContentTransitionCoordinator stepTransition;

	private INotifyPropertyChanged? currentViewModelNotifier;

	public CreateLobbyView()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		InitializeComponent();
		stepTransition = new SlidingContentTransitionCoordinator(this, CreateLobbyStepHost, CreateLobbySetupLayer, CreatedLobbyLayer);
		base.Loaded += CreateLobbyView_Loaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(CreateLobbyView_DataContextChanged);
	}

	private void CreateLobbyView_Loaded(object sender, RoutedEventArgs e)
	{
		stepTransition.Sync(IsLobbyStep());
	}

	private void CreateLobbyView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
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
		stepTransition.Sync(IsLobbyStep());
	}

	private void MultiplayerPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "CreateLobbyStep")
		{
			stepTransition.AnimateTo(IsLobbyStep());
		}
	}

	private bool IsLobbyStep()
	{
		if (base.DataContext is MultiplayerPageViewModel multiplayerPageViewModel)
		{
			return multiplayerPageViewModel.CreateLobbyStep == MultiplayerCreateLobbyStep.Lobby;
		}
		return false;
	}

}

using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using StartRide.App.Services;
using StartRide.App.ViewModels.Account;

namespace StartRide.App.Views.Account;

public partial class AccountPageView : UserControl, IComponentConnector
{
	private readonly SlidingContentTransitionCoordinator selectionTransition;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private bool? hasSelectedAccountState;

	public FrameworkElement RootElement => PageRoot;

	public AccountPageView()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		InitializeComponent();
		selectionTransition = new SlidingContentTransitionCoordinator(this, AccountContentHost, AccountEmptyStateView, AccountDetailsView);
		base.Loaded += AccountPageView_Loaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(AccountPageView_DataContextChanged);
	}

	private async void SecondaryMenuOptionButton_OnRefreshRequested(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is AccountPageViewModel accountPageViewModel)
		{
			await accountPageViewModel.Appearance.RefreshCurrentSecondaryContentAsync();
		}
	}

	private void AccountPageView_Loaded(object sender, RoutedEventArgs e)
	{
		hasSelectedAccountState = HasSelectedAccount();
		selectionTransition.Sync(hasSelectedAccountState.Value);
	}

	private void AccountPageView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= AccountPageViewModel_PropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += AccountPageViewModel_PropertyChanged;
		}
		hasSelectedAccountState = HasSelectedAccount();
		selectionTransition.Sync(hasSelectedAccountState.Value);
	}

	private void AccountPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedAccount")
		{
			bool flag = HasSelectedAccount();
			if (hasSelectedAccountState != flag)
			{
				selectionTransition.AnimateTo(flag);
			}
			hasSelectedAccountState = flag;
		}
	}

	private bool HasSelectedAccount()
	{
		if (base.DataContext is AccountPageViewModel accountPageViewModel)
		{
			return accountPageViewModel.SelectedAccount != null;
		}
		return false;
	}

}

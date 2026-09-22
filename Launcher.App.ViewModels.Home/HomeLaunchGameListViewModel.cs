using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Home;

public sealed class HomeLaunchGameListViewModel : ObservableObject
{
	private readonly IStatusService statusService;

	private readonly Func<GameInstance, Task<bool>> selectLaunchInstance;

	private readonly Func<bool, Task<bool>> setLaunchMenuPinned;

	private long appliedCatalogRevision = -1L;

	[ObservableProperty]
	private GameInstance? selectedInstance;

	[ObservableProperty]
	private bool isLaunchMenuPinned;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? toggleLaunchMenuPinnedCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<HomeLaunchInstanceItem?>? selectLaunchInstanceCommand;

	public ObservableCollection<HomeLaunchInstanceItem> LaunchInstances { get; } = new ObservableCollection<HomeLaunchInstanceItem>();

	public bool HasLaunchInstances => LaunchInstances.Count > 0;

	public bool HasNoLaunchInstances => !HasLaunchInstances;

	public HomeLaunchInstanceItem? SelectedLaunchInstanceItem => LaunchInstances.FirstOrDefault((HomeLaunchInstanceItem item) => item.IsSelected);

	public bool HasSelectedLaunchInstance => SelectedLaunchInstanceItem != null;

	public string LaunchMenuPinTooltip
	{
		get
		{
			if (!IsLaunchMenuPinned)
			{
				return Strings.Home_PinLaunchMenuTooltip;
			}
			return Strings.Home_UnpinLaunchMenuTooltip;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameInstance? SelectedInstance
	{
		get
		{
			return selectedInstance;
		}
		set
		{
			if (!EqualityComparer<GameInstance>.Default.Equals(selectedInstance, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedInstance);
				selectedInstance = value;
				OnSelectedInstanceChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedInstance);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLaunchMenuPinned
	{
		get
		{
			return isLaunchMenuPinned;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLaunchMenuPinned, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLaunchMenuPinned);
				isLaunchMenuPinned = value;
				OnIsLaunchMenuPinnedChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLaunchMenuPinned);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ToggleLaunchMenuPinnedCommand => toggleLaunchMenuPinnedCommand ?? (toggleLaunchMenuPinnedCommand = new AsyncRelayCommand(ToggleLaunchMenuPinnedAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<HomeLaunchInstanceItem?> SelectLaunchInstanceCommand => selectLaunchInstanceCommand ?? (selectLaunchInstanceCommand = new AsyncRelayCommand<HomeLaunchInstanceItem>(SelectLaunchInstanceAsync));

	public HomeLaunchGameListViewModel(IStatusService statusService, Func<GameInstance, Task<bool>> selectLaunchInstance, Func<bool, Task<bool>>? setLaunchMenuPinned = null)
	{
		this.statusService = statusService;
		this.selectLaunchInstance = selectLaunchInstance;
		this.setLaunchMenuPinned = setLaunchMenuPinned ?? ((Func<bool, Task<bool>>)((bool _) => Task.FromResult(result: true)));
	}

	public void SetLaunchMenuPinned(bool isPinned)
	{
		IsLaunchMenuPinned = isPinned;
	}

	public void SetSelectedInstance(GameInstance? instance)
	{
		SelectedInstance = instance;
	}

	public void SetLaunchInstances(IEnumerable<GameInstance> instances)
	{
		ReconcileLaunchInstances(instances);
	}

	public bool ApplyInstanceCatalog(IEnumerable<GameInstance> instances, long catalogRevision)
	{
		if (appliedCatalogRevision == catalogRevision)
		{
			return false;
		}
		bool result = ReconcileLaunchInstances(instances);
		appliedCatalogRevision = catalogRevision;
		return result;
	}

	private bool ReconcileLaunchInstances(IEnumerable<GameInstance> instances)
	{
		string selectedInstanceId = SelectedInstance?.Id;
		Dictionary<string, HomeLaunchInstanceItem> dictionary = LaunchInstances.Where((HomeLaunchInstanceItem item) => !string.IsNullOrWhiteSpace(item.Instance.Id)).GroupBy<HomeLaunchInstanceItem, string>((HomeLaunchInstanceItem item) => item.Instance.Id, StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, HomeLaunchInstanceItem>, string, HomeLaunchInstanceItem>((IGrouping<string, HomeLaunchInstanceItem> group) => group.Key, (IGrouping<string, HomeLaunchInstanceItem> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		List<HomeLaunchInstanceItem> list = new List<HomeLaunchInstanceItem>();
		bool flag = false;
		foreach (GameInstance item in instances.OrderByDescending((GameInstance instance) => instance.CreatedAt))
		{
			if (!string.IsNullOrWhiteSpace(item.Id) && dictionary.TryGetValue(item.Id, out var value))
			{
				flag |= value.Update(item, item.VersionType);
				list.Add(value);
			}
			else
			{
				list.Add(CreateLaunchInstanceItem(item));
				flag = true;
			}
		}
		bool flag2 = ApplyLaunchInstances(list);
		UpdateLaunchInstanceSelection(selectedInstanceId);
		if (flag | flag2)
		{
			NotifyLaunchInstancesChanged();
		}
		return flag | flag2;
	}

	[RelayCommand]
	private async Task ToggleLaunchMenuPinnedAsync()
	{
		bool previousValue = IsLaunchMenuPinned;
		bool arg = (IsLaunchMenuPinned = !previousValue);
		try
		{
			if (await setLaunchMenuPinned(arg))
			{
				return;
			}
		}
		catch (Exception)
		{
		}
		IsLaunchMenuPinned = previousValue;
		statusService.Report(Strings.Status_SettingsSaveFailed);
	}

	[RelayCommand]
	private async Task SelectLaunchInstanceAsync(HomeLaunchInstanceItem? item)
	{
		if (item == null)
		{
			return;
		}
		GameInstance previousSelectedInstance = SelectedInstance;
		SetSelectedInstance(item.Instance);
		try
		{
			if (!(await selectLaunchInstance(item.Instance)))
			{
				SetSelectedInstance(previousSelectedInstance);
				statusService.Report(Strings.Status_LaunchInstanceSelectionFailed);
			}
			else
			{
				SetSelectedInstance(item.Instance);
				statusService.Report(string.Format(Strings.Status_LaunchInstanceSelectedFormat, item.Name));
			}
		}
		catch (Exception)
		{
			SetSelectedInstance(previousSelectedInstance);
			statusService.Report(Strings.Status_LaunchInstanceSelectionFailed);
		}
	}

	private void NotifyLaunchInstancesChanged()
	{
		OnPropertyChanged("HasLaunchInstances");
		OnPropertyChanged("HasNoLaunchInstances");
		OnPropertyChanged("SelectedLaunchInstanceItem");
		OnPropertyChanged("HasSelectedLaunchInstance");
	}

	private void UpdateLaunchInstanceSelection(string? selectedInstanceId)
	{
		foreach (HomeLaunchInstanceItem launchInstance in LaunchInstances)
		{
			launchInstance.IsSelected = !string.IsNullOrWhiteSpace(selectedInstanceId) && string.Equals(launchInstance.Instance.Id, selectedInstanceId, StringComparison.OrdinalIgnoreCase);
		}
		OnPropertyChanged("SelectedLaunchInstanceItem");
		OnPropertyChanged("HasSelectedLaunchInstance");
	}

	private HomeLaunchInstanceItem CreateLaunchInstanceItem(GameInstance instance)
	{
		return new HomeLaunchInstanceItem(instance, instance.VersionType);
	}

	private bool ApplyLaunchInstances(IReadOnlyList<HomeLaunchInstanceItem> instances)
	{
		bool result = false;
		for (int i = 0; i < instances.Count; i++)
		{
			HomeLaunchInstanceItem homeLaunchInstanceItem = instances[i];
			if (i < LaunchInstances.Count && LaunchInstances[i] == homeLaunchInstanceItem)
			{
				continue;
			}
			int num = -1;
			for (int j = i + 1; j < LaunchInstances.Count; j++)
			{
				if (LaunchInstances[j] == homeLaunchInstanceItem)
				{
					num = j;
					break;
				}
			}
			if (num >= 0)
			{
				LaunchInstances.Move(num, i);
			}
			else
			{
				LaunchInstances.Insert(i, homeLaunchInstanceItem);
			}
			result = true;
		}
		while (LaunchInstances.Count > instances.Count)
		{
			LaunchInstances.RemoveAt(LaunchInstances.Count - 1);
			result = true;
		}
		return result;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedInstanceChanged(GameInstance? value)
	{
		UpdateLaunchInstanceSelection(value?.Id);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsLaunchMenuPinnedChanged(bool value)
	{
		OnPropertyChanged("LaunchMenuPinTooltip");
	}
}

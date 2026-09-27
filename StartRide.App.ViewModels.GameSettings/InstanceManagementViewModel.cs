using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceManagementViewModel : ObservableObject
{
	private readonly ISettingsService settingsService;

	private readonly IGameInstanceService instanceService;

	private readonly IInstanceBackupService? backupService;

	private readonly IStatusService statusService;

	private readonly ILogger<InstanceManagementViewModel> logger;

	private readonly object refreshInstancesSync = new object();

	private readonly SemaphoreSlim refreshInstancesGate = new SemaphoreSlim(1, 1);

	private LauncherSettings settings = new LauncherSettings();

	private long refreshRequestGeneration;

	private long appliedRefreshGeneration;

	private string? lastRefreshedMinecraftDirectory;

	private bool hasLoadedInstances;

	private IReadOnlyList<InstanceCatalogEntrySnapshot> catalogSnapshot = Array.Empty<InstanceCatalogEntrySnapshot>();

	private long catalogRevision;

	[ObservableProperty]
	private GameInstance? selectedInstance;

	[ObservableProperty]
	private string newInstanceName = string.Empty;

	public ObservableCollection<GameInstance> Instances { get; } = new ObservableCollection<GameInstance>();

	public bool HasLoadedInstances => hasLoadedInstances;

	public long CatalogRevision => Interlocked.Read(in catalogRevision);

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
				GameInstance oldValue = selectedInstance;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedInstance);
				selectedInstance = value;
				OnSelectedInstanceChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedInstance);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string NewInstanceName
	{
		get
		{
			return newInstanceName;
		}
		[MemberNotNull("newInstanceName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(newInstanceName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NewInstanceName);
				newInstanceName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NewInstanceName);
			}
		}
	}

	public InstanceManagementViewModel(ISettingsService settingsService, IGameInstanceService instanceService, IStatusService statusService, IInstanceBackupService? backupService = null, ILogger<InstanceManagementViewModel>? logger = null)
	{
		this.settingsService = settingsService;
		this.instanceService = instanceService;
		this.backupService = backupService;
		this.statusService = statusService;
		this.logger = logger ?? NullLogger<InstanceManagementViewModel>.Instance;
	}

	public async Task PrimeInstancesAsync(LauncherSettings launcherSettings)
	{
		settings = launcherSettings;
		IReadOnlyList<GameInstance> loadedInstances = await instanceService.GetStoredInstancesAsync(launcherSettings);
		ApplyInstanceSnapshot(launcherSettings.MinecraftDirectory, loadedInstances, markAsFullyLoaded: false);
		logger.LogDebug("Game management instances primed. Count={InstanceCount} SelectedInstanceId={SelectedInstanceId} CatalogRevision={CatalogRevision}", Instances.Count, SelectedInstance?.Id, CatalogRevision);
	}

	public async Task InitializeAsync(LauncherSettings launcherSettings)
	{
		settings = launcherSettings;
		await EnsureInstancesLoadedAsync();
	}

	public async Task EnsureInstancesLoadedAsync()
	{
		if (!hasLoadedInstances)
		{
			await RefreshInstancesAsync();
		}
	}

	public async Task RefreshInstancesAsync()
	{
		long requestedGeneration;
		lock (refreshInstancesSync)
		{
			requestedGeneration = ++refreshRequestGeneration;
		}
		await refreshInstancesGate.WaitAsync();
		try
		{
			lock (refreshInstancesSync)
			{
				if (appliedRefreshGeneration >= requestedGeneration && lastRefreshedMinecraftDirectory != null && PathsEqual(lastRefreshedMinecraftDirectory, settings.MinecraftDirectory))
				{
					return;
				}
			}
			while (true)
			{
				long generation;
				string requestedMinecraftDirectory;
				lock (refreshInstancesSync)
				{
					generation = refreshRequestGeneration;
					requestedMinecraftDirectory = settings.MinecraftDirectory;
				}
				IReadOnlyList<GameInstance> loadedInstances = await LoadInstanceSnapshotAsync(requestedMinecraftDirectory);
				lock (refreshInstancesSync)
				{
					if (generation != refreshRequestGeneration || !PathsEqual(requestedMinecraftDirectory, settings.MinecraftDirectory))
					{
						logger.LogDebug("Discarded stale instance refresh. RefreshGeneration={RefreshGeneration} CurrentGeneration={CurrentGeneration} RequestedDirectory={RequestedDirectory} CurrentDirectory={CurrentDirectory}", generation, refreshRequestGeneration, requestedMinecraftDirectory, settings.MinecraftDirectory);
						continue;
					}
					ApplyInstanceSnapshot(requestedMinecraftDirectory, loadedInstances, markAsFullyLoaded: true);
					appliedRefreshGeneration = generation;
					break;
				}
			}
		}
		finally
		{
			refreshInstancesGate.Release();
		}
	}

	public async Task<GameInstance?> CreateInstanceAsync(MinecraftVersionInfo? minecraftVersion, LoaderKind loader, LoaderVersionInfo? loaderVersion, IProgress<LauncherProgress>? progress)
	{
		if ((object)minecraftVersion == null)
		{
			ReportStatus(Strings.Status_SelectMinecraftVersionFirst);
			return null;
		}
		string loaderVersion2 = ((loader == LoaderKind.Vanilla) ? null : loaderVersion?.Version);
		GameInstance instance;
		try
		{
			IGameInstanceService gameInstanceService = instanceService;
			string name = minecraftVersion.Name;
			string name2 = NewInstanceName;
			DownloadSourcePreference downloadSourcePreference = settings.DownloadSourcePreference;
			int downloadSpeedLimitMbPerSecond = settings.DownloadSpeedLimitMbPerSecond;
			instance = await gameInstanceService.CreateInstanceAsync(name, loader, loaderVersion2, name2, progress, default(CancellationToken), downloadSourcePreference, downloadSpeedLimitMbPerSecond);
		}
		catch (DuplicateGameInstanceNameException)
		{
			ReportStatus(Strings.Status_DuplicateInstanceName);
			return null;
		}
		await refreshInstancesGate.WaitAsync();
		try
		{
			instance.VersionType = minecraftVersion.Type;
			Instances.Add(instance);
			SelectedInstance = instance;
			UpdateCatalogSnapshotAndRevision();
		}
		finally
		{
			refreshInstancesGate.Release();
		}
		ReportStatus(string.Format(Strings.Status_InstanceCreatedFormat, instance.Name));
		return instance;
	}

	public async Task SaveSettingsAsync()
	{
		string minecraftDirectory = settings.MinecraftDirectory;
		if (lastRefreshedMinecraftDirectory == null || !PathsEqual(lastRefreshedMinecraftDirectory, minecraftDirectory))
		{
			logger.LogWarning("Skipped saving instance defaults because the visible list belongs to a different Minecraft directory. CurrentDirectory={CurrentDirectory} RefreshedDirectory={RefreshedDirectory}", minecraftDirectory, lastRefreshedMinecraftDirectory);
			return;
		}
		string defaultInstanceId = settings.DefaultInstanceId;
		await settingsService.UpdateAsync(delegate(LauncherSettings latest)
		{
			if (PathsEqual(latest.MinecraftDirectory, minecraftDirectory))
			{
				latest.DefaultInstanceId = defaultInstanceId;
			}
		});
		ReportStatus(Strings.Status_SettingsSaved);
	}

	public async Task SaveInstanceAsync()
	{
		if (SelectedInstance != null)
		{
			await instanceService.SaveInstanceAsync(SelectedInstance);
			ReportStatus(Strings.Status_InstanceSettingsSaved);
		}
	}

	public async Task SetDefaultInstanceAsync()
	{
		if (SelectedInstance != null)
		{
			ReportStatus((await SelectLaunchInstanceAsync(SelectedInstance)) ? string.Format(Strings.Status_DefaultInstanceSetFormat, SelectedInstance.Name) : Strings.Status_LaunchInstanceSelectionFailed);
		}
	}

	public async Task<bool> SelectLaunchInstanceAsync(GameInstance instance)
	{
		GameInstance previousSelected = SelectedInstance;
		GameInstance selected = Instances.FirstOrDefault((GameInstance existing) => string.Equals(existing.Id, instance.Id, StringComparison.OrdinalIgnoreCase));
		if (selected == null)
		{
			return false;
		}
		SelectedInstance = selected;
		try
		{
			if (!(await instanceService.SetDefaultInstanceAsync(selected.Id)))
			{
				SelectedInstance = previousSelected;
				return false;
			}
		}
		catch (Exception exception)
		{
			SelectedInstance = previousSelected;
			logger.LogWarning(exception, "Default game instance selection failed. InstanceId={InstanceId}", selected.Id);
			return false;
		}
		settings.DefaultInstanceId = selected.Id;
		return true;
	}

	public async Task ApplyUpdatedInstanceAsync(GameInstance instance)
	{
		await refreshInstancesGate.WaitAsync();
		try
		{
			ApplyUpdatedInstanceCore(instance);
		}
		finally
		{
			refreshInstancesGate.Release();
		}
	}

	private void ApplyUpdatedInstanceCore(GameInstance instance)
	{
		int num = FindInstanceIndex(instance.Id);
		bool num2 = string.Equals(SelectedInstance?.Id, instance.Id, StringComparison.OrdinalIgnoreCase);
		if (num >= 0)
		{
			GameInstance gameInstance = Instances[num];
			if (gameInstance != instance)
			{
				GameInstanceStateCopier.Copy(instance, gameInstance);
			}
			instance = gameInstance;
		}
		else
		{
			Instances.Add(instance);
		}
		if (num2 || SelectedInstance == null)
		{
			SelectedInstance = instance;
		}
		hasLoadedInstances = true;
		UpdateCatalogSnapshotAndRevision();
		logger.LogDebug("Game management instance updated locally. InstanceId={InstanceId} Count={InstanceCount} SelectedInstanceId={SelectedInstanceId} CatalogRevision={CatalogRevision}", instance.Id, Instances.Count, SelectedInstance?.Id, CatalogRevision);
	}

	private async Task<IReadOnlyList<GameInstance>> LoadInstanceSnapshotAsync(string requestedMinecraftDirectory)
	{
		if (backupService != null)
		{
			await backupService.RecoverPendingRestoresAsync(requestedMinecraftDirectory);
		}
		return await instanceService.GetInstancesAsync();
	}

	private void ApplyInstanceSnapshot(string requestedMinecraftDirectory, IReadOnlyList<GameInstance> loadedInstances, bool markAsFullyLoaded)
	{
		string previousSelectedId = SelectedInstance?.Id;
		bool forceChanged = lastRefreshedMinecraftDirectory == null || !PathsEqual(lastRefreshedMinecraftDirectory, requestedMinecraftDirectory);
		ReconcileInstances(loadedInstances);
		lastRefreshedMinecraftDirectory = requestedMinecraftDirectory;
		SelectedInstance = ResolveSelectedInstance(settings.DefaultInstanceId, previousSelectedId);
		if (!string.IsNullOrWhiteSpace(settings.DefaultInstanceId) && Instances.All((GameInstance instance) => !string.Equals(instance.Id, settings.DefaultInstanceId, StringComparison.OrdinalIgnoreCase)))
		{
			settings.DefaultInstanceId = SelectedInstance?.Id ?? string.Empty;
		}
		hasLoadedInstances = markAsFullyLoaded;
		UpdateCatalogSnapshotAndRevision(forceChanged);
		logger.LogDebug("Game management instances refreshed. Count={InstanceCount} SelectedInstanceId={SelectedInstanceId} CatalogRevision={CatalogRevision}", Instances.Count, SelectedInstance?.Id, CatalogRevision);
	}

	public async Task<bool> RemoveInstanceAsync(string instanceId)
	{
		await refreshInstancesGate.WaitAsync();
		try
		{
			return RemoveInstanceCore(instanceId);
		}
		finally
		{
			refreshInstancesGate.Release();
		}
	}

	private bool RemoveInstanceCore(string instanceId)
	{
		int num = FindInstanceIndex(instanceId);
		if (num < 0)
		{
			return false;
		}
		bool num2 = string.Equals(SelectedInstance?.Id, instanceId, StringComparison.OrdinalIgnoreCase);
		Instances.RemoveAt(num);
		if (num2)
		{
			SelectedInstance = ResolveSelectedInstance(settings.DefaultInstanceId, null);
			settings.DefaultInstanceId = SelectedInstance?.Id ?? string.Empty;
		}
		UpdateCatalogSnapshotAndRevision();
		logger.LogDebug("Game management instance removed locally. InstanceId={InstanceId} Count={InstanceCount} SelectedInstanceId={SelectedInstanceId} CatalogRevision={CatalogRevision}", instanceId, Instances.Count, SelectedInstance?.Id, CatalogRevision);
		return true;
	}

	private void ReconcileInstances(IReadOnlyList<GameInstance> loadedInstances)
	{
		Dictionary<string, GameInstance> dictionary = Instances.Where((GameInstance instance) => !string.IsNullOrWhiteSpace(instance.Id)).GroupBy<GameInstance, string>((GameInstance instance) => instance.Id, StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, GameInstance>, string, GameInstance>((IGrouping<string, GameInstance> group) => group.Key, (IGrouping<string, GameInstance> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		List<GameInstance> list = new List<GameInstance>(loadedInstances.Count);
		foreach (GameInstance loadedInstance in loadedInstances)
		{
			if (!string.IsNullOrWhiteSpace(loadedInstance.Id) && dictionary.TryGetValue(loadedInstance.Id, out var value))
			{
				if (value != loadedInstance)
				{
					GameInstanceStateCopier.Copy(loadedInstance, value);
				}
				list.Add(value);
			}
			else
			{
				list.Add(loadedInstance);
			}
		}
		for (int num = 0; num < list.Count; num++)
		{
			GameInstance gameInstance = list[num];
			if (num < Instances.Count && Instances[num] == gameInstance)
			{
				continue;
			}
			int num2 = -1;
			for (int num3 = num + 1; num3 < Instances.Count; num3++)
			{
				if (Instances[num3] == gameInstance)
				{
					num2 = num3;
					break;
				}
			}
			if (num2 >= 0)
			{
				Instances.Move(num2, num);
			}
			else
			{
				Instances.Insert(num, gameInstance);
			}
		}
		while (Instances.Count > list.Count)
		{
			Instances.RemoveAt(Instances.Count - 1);
		}
	}

	private void UpdateCatalogSnapshotAndRevision(bool forceChanged = false)
	{
		InstanceCatalogEntrySnapshot[] second = Instances.Select(InstanceCatalogEntrySnapshot.Create).ToArray();
		if (forceChanged || !catalogSnapshot.SequenceEqual(second))
		{
			catalogSnapshot = second;
			Interlocked.Increment(ref catalogRevision);
		}
	}

	private static bool PathsEqual(string first, string second)
	{
		return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)), StringComparison.OrdinalIgnoreCase);
	}

	private GameInstance? ResolveSelectedInstance(string? defaultInstanceId, string? previousSelectedId)
	{
		GameInstance gameInstance = ((!string.IsNullOrWhiteSpace(defaultInstanceId)) ? Instances.FirstOrDefault((GameInstance instance) => string.Equals(instance.Id, defaultInstanceId, StringComparison.OrdinalIgnoreCase)) : null);
		if (gameInstance == null)
		{
			gameInstance = ((!string.IsNullOrWhiteSpace(previousSelectedId)) ? Instances.FirstOrDefault((GameInstance instance) => string.Equals(instance.Id, previousSelectedId, StringComparison.OrdinalIgnoreCase)) : null);
		}
		if (gameInstance == null)
		{
			gameInstance = Instances.FirstOrDefault();
		}
		return gameInstance;
	}

	private int FindInstanceIndex(string instanceId)
	{
		for (int i = 0; i < Instances.Count; i++)
		{
			if (string.Equals(Instances[i].Id, instanceId, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedInstanceChanged(GameInstance? oldValue, GameInstance? newValue)
	{
		if (!string.Equals(oldValue?.Id, newValue?.Id, StringComparison.OrdinalIgnoreCase))
		{
			Interlocked.Increment(ref catalogRevision);
		}
	}
}

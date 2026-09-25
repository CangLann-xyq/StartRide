using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Shared;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class LocalModsViewModel : IDisposable
{
	private const string EnabledModExtension = ".jar";

	private const string DisabledModExtension = ".jar.disabled";

	private readonly IModService modService;

	private readonly ILocalModIconEnrichmentService? iconEnrichmentService;

	private readonly LocalResourceCategoryEnrichmentCoordinator<LocalMod> categoryEnrichmentCoordinator;

	private readonly IStatusService statusService;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<LocalModsViewModel> logger;

	private readonly InstanceContentRefreshWatcher contentWatcher;

	private CancellationTokenSource? refreshCancellationTokenSource;

	private CancellationTokenSource? iconEnrichmentCancellationTokenSource;

	private GameInstance? selectedInstance;

	private IReadOnlyList<LocalMod> currentMods = Array.Empty<LocalMod>();

	private int modRefreshVersion;

	private long revision;

	private long invalidationRevision;

	private long appliedInvalidationRevision;

	private bool observationArmed;

	private volatile bool isSectionActive;

	public ObservableCollection<LocalMod> Mods { get; } = new ObservableCollection<LocalMod>();

	public IReadOnlyList<LocalMod> CurrentMods => currentMods;

	public long Revision => revision;

	private bool IsIconEnrichmentActive => Volatile.Read(in iconEnrichmentCancellationTokenSource) != null;

	public event EventHandler? ModsChanged;

	public event EventHandler<LocalContentIconChangedEventArgs>? IconChanged;

	public LocalModsViewModel(IModService modService, IStatusService statusService, IInstanceDirectoryMonitor instanceDirectoryMonitor, IUiDispatcher? uiDispatcher = null, ILocalModIconEnrichmentService? iconEnrichmentService = null, ILogger<LocalModsViewModel>? logger = null, ILocalResourceCategoryEnrichmentService? categoryEnrichmentService = null)
	{
		this.modService = modService;
		this.iconEnrichmentService = iconEnrichmentService;
		this.statusService = statusService;
		this.uiDispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		this.logger = logger ?? NullLogger<LocalModsViewModel>.Instance;
		categoryEnrichmentCoordinator = new LocalResourceCategoryEnrichmentCoordinator<LocalMod>(categoryEnrichmentService, ResourceProjectKind.Mod, (LocalMod mod) => mod.FullPath, (LocalMod mod) => mod.Categories, delegate(LocalMod mod, IReadOnlyList<ResourceProjectCategory> categories)
		{
			mod.Categories = categories;
		}, () => currentMods, delegate
		{
			ModsChanged?.Invoke(this, EventArgs.Empty);
		}, this.uiDispatcher, this.logger, null, null, preferResolvedIconSource: false, (LocalMod mod) => mod.ProjectReference, delegate(LocalMod mod, ResourceProjectReference reference)
		{
			mod.ProjectReference = reference;
		});
		contentWatcher = new InstanceContentRefreshWatcher(instanceDirectoryMonitor, InstanceDirectoryKind.Mods, RefreshModsAsync, delegate
		{
			this.uiDispatcher.Post(delegate
			{
				ReportStatus(Strings.Status_LoadLocalModsFailed);
			});
		}, this.logger, ShouldRefreshForDirectoryChange, delegate
		{
			Interlocked.Increment(ref invalidationRevision);
		}, () => isSectionActive);
	}

	public void SetSelectedInstance(GameInstance? instance)
	{
		contentWatcher.SetEnabled(value: false);
		observationArmed = false;
		selectedInstance = instance;
		Interlocked.Exchange(ref invalidationRevision, 0L);
		Interlocked.Exchange(ref appliedInvalidationRevision, 0L);
		Interlocked.Increment(ref modRefreshVersion);
		CancelRefresh();
		CancelIconEnrichment();
		categoryEnrichmentCoordinator.Cancel();
		contentWatcher.SetInstance(instance);
		ClearMods();
		logger.LogDebug("Selected instance changed for local mods view. InstanceId={InstanceId}", instance?.Id ?? "<none>");
	}

	public void SetSectionActive(bool active)
	{
		isSectionActive = active;
		if (active && selectedInstance != null && !observationArmed)
		{
			observationArmed = true;
			contentWatcher.SetEnabled(value: true);
		}
		if (!active)
		{
			if (Volatile.Read(in refreshCancellationTokenSource) != null)
			{
				Interlocked.Increment(ref invalidationRevision);
			}
			Interlocked.Increment(ref modRefreshVersion);
			CancelRefresh();
		}
	}

	public Task<bool> RefreshIfInvalidatedAsync()
	{
		if (!isSectionActive || Interlocked.Read(in invalidationRevision) <= Interlocked.Read(in appliedInvalidationRevision))
		{
			return Task.FromResult(result: true);
		}
		return RefreshModsAsync();
	}

	public void InvalidateSnapshot()
	{
		Interlocked.Increment(ref invalidationRevision);
	}

	public void ReleaseObservation()
	{
		isSectionActive = false;
		if (observationArmed)
		{
			Interlocked.Increment(ref invalidationRevision);
		}
		observationArmed = false;
		contentWatcher.SetEnabled(value: false);
		Interlocked.Increment(ref modRefreshVersion);
		CancelRefresh();
		CancelIconEnrichment();
		categoryEnrichmentCoordinator.Cancel();
	}

	public void SuspendWatcherForInstanceRename()
	{
		contentWatcher.Suspend();
		CancelRefresh();
	}

	public void ResumeWatcherAfterInstanceRename(bool restart = true)
	{
		contentWatcher.Resume(restart);
	}

	public async Task<bool> RefreshModsAsync()
	{
		long invalidationAtStart = Interlocked.Read(in invalidationRevision);
		int refreshVersion = Interlocked.Increment(ref modRefreshVersion);
		CancellationTokenSource refreshCts = ReplaceRefreshCancellationTokenSource();
		GameInstance instance = selectedInstance;
		if (instance == null)
		{
			ClearMods();
			logger.LogDebug("Local mods view cleared because no instance is selected.");
			return true;
		}
		IReadOnlyList<LocalMod> loadedMods;
		IReadOnlyList<LocalMod> changedMods;
		bool snapshotChanged;
		bool cachedIconsChanged;
		try
		{
			loadedMods = await modService.GetModsAsync(instance, refreshCts.Token);
			changedMods = FindChangedMods(loadedMods);
			snapshotChanged = HasReferenceChanges(currentMods, loadedMods);
			LocalMod[] loadedMods2 = ((snapshotChanged || !IsIconEnrichmentActive) ? loadedMods.Where((LocalMod mod) => string.IsNullOrWhiteSpace(mod.IconSource)).ToArray() : Array.Empty<LocalMod>());
			if (IsRefreshCurrent(instance, refreshVersion))
			{
				cachedIconsChanged = await ApplyCachedIconSourcesAsync(instance, loadedMods2, refreshCts.Token);
			}
			else
			{
				cachedIconsChanged = false;
			}
		}
		catch (OperationCanceledException) when (refreshCts.IsCancellationRequested)
		{
			return false;
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to load local mods. InstanceId={InstanceId}", instance.Id);
			throw;
		}
		finally
		{
			ReleaseRefreshCancellationTokenSource(refreshCts);
		}
		if (!IsRefreshCurrent(instance, refreshVersion))
		{
			return false;
		}
		bool published = false;
		uiDispatcher.Invoke(delegate
		{
			if (IsRefreshCurrent(instance, refreshVersion))
			{
				if (!HasReferenceChanges(currentMods, loadedMods))
				{
					if (cachedIconsChanged)
					{
						ModsChanged?.Invoke(this, EventArgs.Empty);
					}
					published = true;
				}
				else
				{
					currentMods = loadedMods;
					Mods.SynchronizeByKey<LocalMod, string>(loadedMods, (LocalMod mod) => mod.FullPath, StringComparer.OrdinalIgnoreCase);
					revision++;
					ModsChanged?.Invoke(this, EventArgs.Empty);
					published = true;
				}
			}
		});
		if (!published)
		{
			return false;
		}
		AdvanceAppliedInvalidationRevision(invalidationAtStart);
		logger.LogDebug("Local mods view refreshed. InstanceId={InstanceId} Count={ModCount}", instance.Id, Mods.Count);
		if (snapshotChanged)
		{
			CancelIconEnrichment();
		}
		if (!IsIconEnrichmentActive)
		{
			QueueRemoteIconEnrichment(instance, loadedMods);
		}
		if (changedMods.Count > 0)
		{
			categoryEnrichmentCoordinator.Queue(changedMods);
		}
		return true;
	}

	private void AdvanceAppliedInvalidationRevision(long revisionToApply)
	{
		long num;
		do
		{
			num = Interlocked.Read(in appliedInvalidationRevision);
		}
		while (num < revisionToApply && Interlocked.CompareExchange(ref appliedInvalidationRevision, revisionToApply, num) != num);
	}

	private async Task<bool> ApplyCachedIconSourcesAsync(GameInstance instance, IReadOnlyList<LocalMod> loadedMods, CancellationToken cancellationToken)
	{
		if (iconEnrichmentService == null || loadedMods.Count == 0)
		{
			return false;
		}
		IReadOnlyDictionary<string, string> readOnlyDictionary;
		try
		{
			readOnlyDictionary = await iconEnrichmentService.ResolveCachedIconSourcesAsync(loadedMods, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to resolve cached local mod icons before publishing mods. InstanceId={InstanceId}", instance.Id);
			return false;
		}
		if (readOnlyDictionary.Count == 0)
		{
			return false;
		}
		int num = 0;
		foreach (LocalMod loadedMod in loadedMods)
		{
			if (string.IsNullOrWhiteSpace(loadedMod.IconSource) && readOnlyDictionary.TryGetValue(loadedMod.FullPath, out var value) && !string.IsNullOrWhiteSpace(value))
			{
				loadedMod.IconSource = value;
				num++;
			}
		}
		if (num > 0)
		{
			logger.LogDebug("Applied cached local mod icons before publishing mods. InstanceId={InstanceId} Count={Count}", instance.Id, num);
		}
		return num > 0;
	}

	public async Task ToggleModAsync(LocalMod mod)
	{
		ArgumentNullException.ThrowIfNull(mod, "mod");
		bool enabled = !mod.IsEnabled;
		string targetPath = GetPathForEnabledState(mod.FullPath, enabled);
		contentWatcher.Suspend();
		try
		{
			await modService.SetEnabledAsync(mod, enabled);
			uiDispatcher.Invoke(delegate
			{
				ApplyEnabledStateLocallyCore(mod, targetPath, enabled);
				revision++;
				ModsChanged?.Invoke(this, EventArgs.Empty);
			});
		}
		finally
		{
			contentWatcher.Resume();
		}
	}

	public async Task DeleteModAsync(LocalMod mod)
	{
		contentWatcher.Suspend();
		try
		{
			await modService.DeleteAsync(mod);
			await RefreshModsAsync();
		}
		finally
		{
			contentWatcher.Resume();
		}
	}

	public async Task<LocalModEnabledStateBatchResult> SetModsEnabledAsync(IEnumerable<LocalMod> mods, bool enabled)
	{
		ArgumentNullException.ThrowIfNull(mods, "mods");
		contentWatcher.Suspend();
		try
		{
			int failedCount = 0;
			string firstConflictTargetPath = null;
			List<(LocalMod Mod, string TargetPath)> appliedUpdates = new List<(LocalMod, string)>();
			foreach (LocalMod mod in mods.Where((LocalMod localMod) => localMod.IsEnabled != enabled).DistinctBy<LocalMod, string>((LocalMod localMod) => localMod.FullPath, StringComparer.OrdinalIgnoreCase))
			{
				string fullPath = mod.FullPath;
				string targetPath = GetPathForEnabledState(fullPath, enabled);
				try
				{
					await modService.SetEnabledAsync(mod, enabled);
					appliedUpdates.Add((mod, targetPath));
				}
				catch (ModEnabledStateConflictException ex)
				{
					failedCount++;
					if (firstConflictTargetPath == null)
					{
						firstConflictTargetPath = ex.TargetPath;
					}
					logger.LogWarning(ex, "Local mod enabled state target already exists. Path={Path} TargetPath={TargetPath} Enabled={Enabled}", mod.FullPath, ex.TargetPath, enabled);
				}
				catch (Exception exception)
				{
					failedCount++;
					logger.LogWarning(exception, "Failed to change local mod enabled state. Path={Path} Enabled={Enabled}", mod.FullPath, enabled);
				}
			}
			if (appliedUpdates.Count > 0)
			{
				uiDispatcher.Invoke(delegate
				{
					foreach (var (mod2, targetPath2) in appliedUpdates)
					{
						ApplyEnabledStateLocallyCore(mod2, targetPath2, enabled);
					}
					revision++;
					ModsChanged?.Invoke(this, EventArgs.Empty);
				});
			}
			return new LocalModEnabledStateBatchResult(failedCount, firstConflictTargetPath);
		}
		finally
		{
			contentWatcher.Resume();
		}
	}

	public async Task<int> DeleteModsAsync(IEnumerable<LocalMod> mods)
	{
		ArgumentNullException.ThrowIfNull(mods, "mods");
		contentWatcher.Suspend();
		try
		{
			int failedCount = 0;
			foreach (LocalMod mod in mods.DistinctBy<LocalMod, string>((LocalMod localMod) => localMod.FullPath, StringComparer.OrdinalIgnoreCase))
			{
				try
				{
					await modService.DeleteAsync(mod);
				}
				catch (Exception exception)
				{
					failedCount++;
					logger.LogWarning(exception, "Failed to delete local mod. Path={Path}", mod.FullPath);
				}
			}
			await RefreshModsAsync();
			return failedCount;
		}
		finally
		{
			contentWatcher.Resume();
		}
	}

	public async Task<bool> ImportModFromPathAsync(string path, bool overwriteExisting = false, bool reportStatus = true)
	{
		if (selectedInstance == null || string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		contentWatcher.Suspend();
		try
		{
			try
			{
				await modService.ImportAsync(selectedInstance, path, overwriteExisting);
			}
			catch (ModFileImportNotFoundException)
			{
				if (reportStatus)
				{
					ReportStatus(Strings.Status_LocalModImportFileNotFound);
				}
				return false;
			}
			await RefreshModsAsync();
			if (reportStatus)
			{
				ReportStatus(Strings.Status_LocalModImported);
			}
			return true;
		}
		finally
		{
			contentWatcher.Resume();
		}
	}

	public void Dispose()
	{
		contentWatcher.Dispose();
		CancelRefresh();
		CancelIconEnrichment();
		categoryEnrichmentCoordinator.Dispose();
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}

	private void ClearMods()
	{
		uiDispatcher.Invoke(delegate
		{
			if (currentMods.Count != 0 || Mods.Count != 0)
			{
				currentMods = Array.Empty<LocalMod>();
				Mods.Clear();
				revision++;
				ModsChanged?.Invoke(this, EventArgs.Empty);
			}
		});
	}

	private void CancelRefresh()
	{
		refreshCancellationTokenSource?.Cancel();
		refreshCancellationTokenSource?.Dispose();
		refreshCancellationTokenSource = null;
	}

	private CancellationTokenSource ReplaceRefreshCancellationTokenSource()
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource2 = Interlocked.Exchange(ref refreshCancellationTokenSource, cancellationTokenSource);
		cancellationTokenSource2?.Cancel();
		cancellationTokenSource2?.Dispose();
		return cancellationTokenSource;
	}

	private void ReleaseRefreshCancellationTokenSource(CancellationTokenSource refreshCts)
	{
		if (Interlocked.CompareExchange(ref refreshCancellationTokenSource, null, refreshCts) == refreshCts)
		{
			refreshCts.Dispose();
		}
	}

	private void QueueRemoteIconEnrichment(GameInstance instance, IReadOnlyList<LocalMod> loadedMods)
	{
		if (iconEnrichmentService == null)
		{
			return;
		}
		LocalMod[] missingIconMods = loadedMods.Where((LocalMod mod) => string.IsNullOrWhiteSpace(mod.IconSource)).ToArray();
		if (missingIconMods.Length == 0)
		{
			return;
		}
		CancellationTokenSource enrichmentCts = ReplaceIconEnrichmentCancellationTokenSource();
		Task.Run(async delegate
		{
			try
			{
				CallbackProgress<LocalContentIconResolution> progress = new CallbackProgress<LocalContentIconResolution>(delegate(LocalContentIconResolution resolution)
				{
					ApplyResolvedIcon(instance, resolution, enrichmentCts);
				});
				await iconEnrichmentService.ResolveMissingIconSourcesAsync(missingIconMods, enrichmentCts.Token, progress).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (enrichmentCts.IsCancellationRequested)
			{
			}
			catch (Exception exception)
			{
				logger.LogWarning(exception, "Failed to enrich local mod icons. InstanceId={InstanceId}", instance.Id);
			}
			finally
			{
				ReleaseIconEnrichmentCancellationTokenSource(enrichmentCts);
			}
		});
	}

	private void ApplyResolvedIcon(GameInstance instance, LocalContentIconResolution resolution, CancellationTokenSource enrichmentCts)
	{
		if (string.IsNullOrWhiteSpace(resolution.FullPath) || string.IsNullOrWhiteSpace(resolution.IconSource) || enrichmentCts.IsCancellationRequested || !IsSameInstancePath(instance, selectedInstance))
		{
			return;
		}
		uiDispatcher.Post(delegate
		{
			if (!enrichmentCts.IsCancellationRequested && IsSameInstancePath(instance, selectedInstance))
			{
				LocalMod localMod = currentMods.FirstOrDefault((LocalMod candidate) => string.Equals(candidate.FullPath, resolution.FullPath, StringComparison.OrdinalIgnoreCase));
				if (localMod != null && string.IsNullOrWhiteSpace(localMod.IconSource) && !string.IsNullOrWhiteSpace(resolution.IconSource))
				{
					localMod.IconSource = resolution.IconSource;
					IconChanged?.Invoke(this, new LocalContentIconChangedEventArgs(localMod.FullPath, resolution.IconSource));
				}
			}
		});
	}

	private bool IsRefreshCurrent(GameInstance instance, int refreshVersion)
	{
		if (refreshVersion == modRefreshVersion)
		{
			return IsSameInstancePath(instance, selectedInstance);
		}
		return false;
	}

	private IReadOnlyList<LocalMod> FindChangedMods(IReadOnlyList<LocalMod> loadedMods)
	{
		Dictionary<string, LocalMod> currentByPath = currentMods.ToDictionary<LocalMod, string>((LocalMod mod) => mod.FullPath, StringComparer.OrdinalIgnoreCase);
		return loadedMods.Where((LocalMod mod) => !currentByPath.TryGetValue(mod.FullPath, out var value) || value != mod).ToArray();
	}

	private static bool HasReferenceChanges(IReadOnlyList<LocalMod> current, IReadOnlyList<LocalMod> next)
	{
		if (current.Count != next.Count)
		{
			return true;
		}
		for (int i = 0; i < current.Count; i++)
		{
			if (current[i] != next[i])
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsSameInstancePath(GameInstance left, GameInstance? right)
	{
		if (right != null && string.Equals(left.Id, right.Id, StringComparison.Ordinal))
		{
			return string.Equals(left.InstanceDirectory, right.InstanceDirectory, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private void CancelIconEnrichment()
	{
		iconEnrichmentCancellationTokenSource?.Cancel();
		iconEnrichmentCancellationTokenSource?.Dispose();
		iconEnrichmentCancellationTokenSource = null;
	}

	private CancellationTokenSource ReplaceIconEnrichmentCancellationTokenSource()
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource2 = Interlocked.Exchange(ref iconEnrichmentCancellationTokenSource, cancellationTokenSource);
		cancellationTokenSource2?.Cancel();
		cancellationTokenSource2?.Dispose();
		return cancellationTokenSource;
	}

	private void ReleaseIconEnrichmentCancellationTokenSource(CancellationTokenSource enrichmentCts)
	{
		if (Interlocked.CompareExchange(ref iconEnrichmentCancellationTokenSource, null, enrichmentCts) == enrichmentCts)
		{
			enrichmentCts.Dispose();
		}
	}

	private static bool IsTrackedModPath(string? fullPath)
	{
		if (!string.IsNullOrWhiteSpace(fullPath))
		{
			if (!fullPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
			{
				return fullPath.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private bool ShouldRefreshForDirectoryChange(InstanceDirectoryChangedEventArgs change)
	{
		if (!IsTrackedModPath(change.FullPath))
		{
			return IsTrackedModPath(change.OldFullPath);
		}
		return true;
	}

	private static void ApplyEnabledStateLocallyCore(LocalMod mod, string targetPath, bool enabled)
	{
		mod.FullPath = targetPath;
		mod.FileName = Path.GetFileName(targetPath);
		mod.IsEnabled = enabled;
	}

	private static string GetPathForEnabledState(string path, bool enabled)
	{
		if (!enabled)
		{
			if (!path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
			{
				return path + ".disabled";
			}
			return path;
		}
		if (!path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}
		int length = ".disabled".Length;
		return path.Substring(0, path.Length - length);
	}
}

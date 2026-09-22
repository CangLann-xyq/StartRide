using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.GameSettings;

internal sealed class LocalResourceCategoryEnrichmentCoordinator<T> : IDisposable
{
	private const int EnrichmentBatchSize = 50;

	private readonly ILocalResourceCategoryEnrichmentService? service;

	private readonly ResourceProjectKind kind;

	private readonly Func<T, string> pathSelector;

	private readonly Func<T, IReadOnlyList<ResourceProjectCategory>> categoriesSelector;

	private readonly Action<T, IReadOnlyList<ResourceProjectCategory>> categoriesSetter;

	private readonly Func<T, string?>? iconSourceSelector;

	private readonly Action<T, string>? iconSourceSetter;

	private readonly bool preferResolvedIconSource;

	private readonly Action<T, string>? iconChanged;

	private readonly Func<T, ResourceProjectReference?>? projectReferenceSelector;

	private readonly Action<T, ResourceProjectReference>? projectReferenceSetter;

	private readonly Func<IReadOnlyList<T>> currentItemsProvider;

	private readonly Action changed;

	private readonly IUiDispatcher dispatcher;

	private readonly ILogger logger;

	private CancellationTokenSource? cancellationTokenSource;

	private long generation;

	public LocalResourceCategoryEnrichmentCoordinator(ILocalResourceCategoryEnrichmentService? service, ResourceProjectKind kind, Func<T, string> pathSelector, Func<T, IReadOnlyList<ResourceProjectCategory>> categoriesSelector, Action<T, IReadOnlyList<ResourceProjectCategory>> categoriesSetter, Func<IReadOnlyList<T>> currentItemsProvider, Action changed, IUiDispatcher dispatcher, ILogger logger, Func<T, string?>? iconSourceSelector = null, Action<T, string>? iconSourceSetter = null, bool preferResolvedIconSource = false, Func<T, ResourceProjectReference?>? projectReferenceSelector = null, Action<T, ResourceProjectReference>? projectReferenceSetter = null, Action<T, string>? iconChanged = null)
	{
		this.service = service;
		this.kind = kind;
		this.pathSelector = pathSelector;
		this.categoriesSelector = categoriesSelector;
		this.categoriesSetter = categoriesSetter;
		this.iconSourceSelector = iconSourceSelector;
		this.iconSourceSetter = iconSourceSetter;
		this.preferResolvedIconSource = preferResolvedIconSource;
		this.iconChanged = iconChanged;
		this.projectReferenceSelector = projectReferenceSelector;
		this.projectReferenceSetter = projectReferenceSetter;
		this.currentItemsProvider = currentItemsProvider;
		this.changed = changed;
		this.dispatcher = dispatcher;
		this.logger = logger;
	}

	public void Queue(IReadOnlyList<T> items)
	{
		if (service != null && items.Count != 0)
		{
			LocalResourceCategoryCandidate[] array = (from path in (from item in items
					select pathSelector(item) into path
					where !string.IsNullOrWhiteSpace(path)
					select path).Distinct<string>(StringComparer.OrdinalIgnoreCase)
				select new LocalResourceCategoryCandidate(path, kind)).ToArray();
			if (array.Length != 0)
			{
				Cancel();
				long expectedGeneration = generation;
				EnrichAsync(array, expectedGeneration, cancellationTokenSource = new CancellationTokenSource());
			}
		}
	}

	public void Cancel()
	{
		Interlocked.Increment(ref generation);
		CancellationTokenSource? obj = Interlocked.Exchange(ref cancellationTokenSource, null);
		obj?.Cancel();
		obj?.Dispose();
	}

	public void Dispose()
	{
		Cancel();
	}

	private async Task EnrichAsync(IReadOnlyList<LocalResourceCategoryCandidate> candidates, long expectedGeneration, CancellationTokenSource cts)
	{
		try
		{
			CallbackProgress<LocalContentIconResolution> iconProgress = new CallbackProgress<LocalContentIconResolution>(delegate(LocalContentIconResolution resolution)
			{
				dispatcher.Post(delegate
				{
					ApplyIcon(resolution, expectedGeneration, cts);
				});
			});
			foreach (LocalResourceCategoryCandidate[] batch in candidates.Chunk(50))
			{
				IReadOnlyDictionary<string, LocalResourceEnrichmentResult> cached = await service.ResolveCachedMetadataAsync(batch, cts.Token, iconProgress).ConfigureAwait(continueOnCapturedContext: false);
				if (!IsCurrent(expectedGeneration, cts))
				{
					return;
				}
				if (cached.Count > 0)
				{
					dispatcher.Post(delegate
					{
						Apply(cached, expectedGeneration, cts);
					});
				}
				IReadOnlyDictionary<string, LocalResourceEnrichmentResult> resolved = await service.ResolveMetadataAsync(batch, cts.Token, iconProgress).ConfigureAwait(continueOnCapturedContext: false);
				if (!IsCurrent(expectedGeneration, cts))
				{
					return;
				}
				if (resolved.Count > 0)
				{
					dispatcher.Post(delegate
					{
						Apply(resolved, expectedGeneration, cts);
					});
				}
			}
		}
		catch (OperationCanceledException) when (cts.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to enrich local resource categories. Kind={Kind}", kind);
		}
		finally
		{
			if (Interlocked.CompareExchange(ref cancellationTokenSource, null, cts) == cts)
			{
				cts.Dispose();
			}
		}
	}

	private void Apply(IReadOnlyDictionary<string, LocalResourceEnrichmentResult> resolved, long expectedGeneration, CancellationTokenSource cts)
	{
		if (!IsCurrent(expectedGeneration, cts))
		{
			return;
		}
		bool flag = false;
		foreach (T item in currentItemsProvider())
		{
			string key = pathSelector(item);
			if (!resolved.TryGetValue(key, out LocalResourceEnrichmentResult value))
			{
				continue;
			}
			IReadOnlyList<ResourceProjectCategory> first = categoriesSelector(item);
			if (value.Categories.Count > 0 && !first.SequenceEqual(value.Categories))
			{
				categoriesSetter(item, value.Categories);
				flag = true;
			}
			if (iconSourceSelector != null && iconSourceSetter != null && !string.IsNullOrWhiteSpace(value.IconSource))
			{
				string text = iconSourceSelector(item);
				if ((preferResolvedIconSource || string.IsNullOrWhiteSpace(text)) && !string.Equals(text, value.IconSource, StringComparison.Ordinal))
				{
					iconSourceSetter(item, value.IconSource);
					if (iconChanged == null)
					{
						flag = true;
					}
					else
					{
						iconChanged(item, value.IconSource);
					}
				}
			}
			if (projectReferenceSelector != null && projectReferenceSetter != null && (object)value.ProjectReference != null && projectReferenceSelector(item) != value.ProjectReference)
			{
				projectReferenceSetter(item, value.ProjectReference);
				flag = true;
			}
		}
		if (flag)
		{
			changed();
		}
	}

	private void ApplyIcon(LocalContentIconResolution resolution, long expectedGeneration, CancellationTokenSource cts)
	{
		if (!IsCurrent(expectedGeneration, cts) || iconSourceSelector == null || iconSourceSetter == null || string.IsNullOrWhiteSpace(resolution.FullPath) || string.IsNullOrWhiteSpace(resolution.IconSource))
		{
			return;
		}
		T val = currentItemsProvider().FirstOrDefault((T candidate) => string.Equals(pathSelector(candidate), resolution.FullPath, StringComparison.OrdinalIgnoreCase));
		if (val == null)
		{
			return;
		}
		string text = iconSourceSelector(val);
		if ((preferResolvedIconSource || string.IsNullOrWhiteSpace(text)) && !string.Equals(text, resolution.IconSource, StringComparison.Ordinal))
		{
			iconSourceSetter(val, resolution.IconSource);
			if (iconChanged == null)
			{
				changed();
			}
			else
			{
				iconChanged(val, resolution.IconSource);
			}
		}
	}

	private bool IsCurrent(long expectedGeneration, CancellationTokenSource cts)
	{
		if (!cts.IsCancellationRequested)
		{
			return expectedGeneration == Interlocked.Read(in generation);
		}
		return false;
	}
}

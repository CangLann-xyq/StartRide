using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using StartRide.App.Diagnostics;
using Serilog;

namespace StartRide.App.Services;

internal sealed class TransitionRenderCacheScope : IDisposable
{
	private sealed class CacheState(CacheMode? originalCacheMode, BitmapCache? installedCache)
	{
		internal CacheMode? OriginalCacheMode { get; } = originalCacheMode;

		internal BitmapCache? InstalledCache { get; } = installedCache;

		internal int ReferenceCount { get; set; }
	}

	private static readonly ConditionalWeakTable<FrameworkElement, CacheState> CacheStates = new ConditionalWeakTable<FrameworkElement, CacheState>();

	private static readonly object CacheStateGate = new object();

	private readonly FrameworkElement[] elements;

	private int isDisposed;

	internal bool IsActive
	{
		get
		{
			if (Volatile.Read(in isDisposed) == 0 && elements.Length != 0)
			{
				return FallbackReason == TransitionRenderCacheFallbackReason.None;
			}
			return false;
		}
	}

	internal long EstimatedBytes { get; }

	internal TransitionRenderCacheFallbackReason FallbackReason { get; }

	internal static int ActiveOwnedCacheCount { get; private set; }

	private TransitionRenderCacheScope(FrameworkElement[] elements, long estimatedBytes, TransitionRenderCacheFallbackReason fallbackReason)
	{
		this.elements = elements;
		EstimatedBytes = estimatedBytes;
		FallbackReason = fallbackReason;
		for (int i = 0; i < elements.Length; i++)
		{
			elements[i].Unloaded += Element_Unloaded;
		}
	}

	internal static bool RequiresContinuousRefreshFallback(TransitionRenderCacheFallbackReason reason)
	{
		bool flag = ((reason == TransitionRenderCacheFallbackReason.None || reason == TransitionRenderCacheFallbackReason.ElementNotReady) ? true : false);
		return !flag;
	}

	internal static TransitionRenderCacheScope TryAcquire(string transitionKind, IReadOnlyList<FrameworkElement> elements)
	{
		return TryAcquire(transitionKind, elements, TransitionRenderCacheCapabilities.Current);
	}

	internal static TransitionRenderCacheScope TryAcquire(string transitionKind, IReadOnlyList<FrameworkElement> elements, TransitionRenderCacheCapabilities capabilities)
	{
		TransitionRenderCacheScope transitionRenderCacheScope = TryAcquireCore(transitionKind, elements, capabilities);
		if (transitionRenderCacheScope.FallbackReason != TransitionRenderCacheFallbackReason.None)
		{
			UiPerformanceLog.LogTransitionRenderCacheFallback(transitionKind, transitionRenderCacheScope.FallbackReason.ToString(), transitionRenderCacheScope.EstimatedBytes);
		}
		return transitionRenderCacheScope;
	}

	private static TransitionRenderCacheScope TryAcquireCore(string transitionKind, IReadOnlyList<FrameworkElement> elements, TransitionRenderCacheCapabilities capabilities)
	{
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		FrameworkElement[] array = elements.Where((FrameworkElement element) => element != null).Distinct().ToArray();
		if (array.Length == 0)
		{
			return CreateFallback(TransitionRenderCacheFallbackReason.NoElements);
		}
		if (capabilities.RenderingTier < 2)
		{
			return CreateFallback(TransitionRenderCacheFallbackReason.RenderingTierTooLow);
		}
		long num = 0L;
		FrameworkElement[] array2 = array;
		int num2 = 0;
		while (num2 < array2.Length)
		{
			FrameworkElement frameworkElement = array2[num2];
			if (!frameworkElement.IsLoaded || !frameworkElement.IsVisible || frameworkElement.ActualWidth <= 0.0 || frameworkElement.ActualHeight <= 0.0)
			{
				return CreateFallback(TransitionRenderCacheFallbackReason.ElementNotReady);
			}
			DpiScale dpi = VisualTreeHelper.GetDpi(frameworkElement);
			long num3 = Math.Max(1L, (long)Math.Ceiling(frameworkElement.ActualWidth * dpi.DpiScaleX));
			long num4 = Math.Max(1L, (long)Math.Ceiling(frameworkElement.ActualHeight * dpi.DpiScaleY));
			Size maximumTextureSize = capabilities.MaximumTextureSize;
			if (!(maximumTextureSize.Width <= 0.0))
			{
				maximumTextureSize = capabilities.MaximumTextureSize;
				if (!(maximumTextureSize.Height <= 0.0))
				{
					double num5 = num3;
					maximumTextureSize = capabilities.MaximumTextureSize;
					if (!(num5 > maximumTextureSize.Width))
					{
						double num6 = num4;
						maximumTextureSize = capabilities.MaximumTextureSize;
						if (!(num6 > maximumTextureSize.Height))
						{
							if (num3 > long.MaxValue / num4 / 4)
							{
								return CreateFallback(TransitionRenderCacheFallbackReason.MemoryBudgetExceeded);
							}
							long num7 = num3 * num4 * 4;
							if (num > capabilities.MaximumEstimatedBytes - num7)
							{
								return CreateFallback(TransitionRenderCacheFallbackReason.MemoryBudgetExceeded);
							}
							num += num7;
							num2++;
							continue;
						}
					}
				}
			}
			return CreateFallback(TransitionRenderCacheFallbackReason.TextureTooLarge);
		}
		List<FrameworkElement> list = new List<FrameworkElement>(array.Length);
		try
		{
			array2 = array;
			foreach (FrameworkElement frameworkElement2 in array2)
			{
				AcquireElement(frameworkElement2);
				list.Add(frameworkElement2);
			}
		}
		catch (Exception exception)
		{
			for (int num8 = list.Count - 1; num8 >= 0; num8--)
			{
				ReleaseElement(list[num8]);
			}
			Log.Warning(exception, "Transition render cache initialization failed; continuing with live rendering. TransitionKind={TransitionKind}", transitionKind);
			return CreateFallback(TransitionRenderCacheFallbackReason.CacheCreationFailed);
		}
		return new TransitionRenderCacheScope(array, num, TransitionRenderCacheFallbackReason.None);
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) == 0)
		{
			FrameworkElement[] array = elements;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Unloaded -= Element_Unloaded;
			}
			for (int num = elements.Length - 1; num >= 0; num--)
			{
				ReleaseElement(elements[num]);
			}
		}
	}

	private static TransitionRenderCacheScope CreateFallback(TransitionRenderCacheFallbackReason reason)
	{
		return new TransitionRenderCacheScope(Array.Empty<FrameworkElement>(), 0L, reason);
	}

	private static void AcquireElement(FrameworkElement element)
	{
		lock (CacheStateGate)
		{
			if (CacheStates.TryGetValue(element, out CacheState value))
			{
				value.ReferenceCount++;
				return;
			}
			CacheMode cacheMode = element.CacheMode;
			BitmapCache bitmapCache = null;
			bool flag = false;
			try
			{
				if (cacheMode == null)
				{
					bitmapCache = (BitmapCache)(element.CacheMode = new BitmapCache
					{
						RenderAtScale = 1.0,
						EnableClearType = false,
						SnapsToDevicePixels = false
					});
					ActiveOwnedCacheCount++;
					flag = true;
				}
				CacheStates.Add(element, new CacheState(cacheMode, bitmapCache)
				{
					ReferenceCount = 1
				});
			}
			catch
			{
				if (bitmapCache != null)
				{
					if (element.CacheMode == bitmapCache)
					{
						element.CacheMode = cacheMode;
					}
					if (flag)
					{
						ActiveOwnedCacheCount--;
					}
				}
				throw;
			}
		}
	}

	private static void ReleaseElement(FrameworkElement element)
	{
		lock (CacheStateGate)
		{
			if (!CacheStates.TryGetValue(element, out CacheState value))
			{
				return;
			}
			value.ReferenceCount--;
			if (value.ReferenceCount > 0)
			{
				return;
			}
			CacheStates.Remove(element);
			if (value.InstalledCache != null)
			{
				if (element.CacheMode == value.InstalledCache)
				{
					element.CacheMode = value.OriginalCacheMode;
				}
				ActiveOwnedCacheCount--;
			}
		}
	}

	private void Element_Unloaded(object sender, RoutedEventArgs e)
	{
		Dispose();
	}
}

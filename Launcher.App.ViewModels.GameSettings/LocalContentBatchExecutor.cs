using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Launcher.App.ViewModels.GameSettings;

internal static class LocalContentBatchExecutor
{
	public static async Task<int> ExecuteAsync<T>(IEnumerable<T> items, Func<T, string> keySelector, Func<T, Task> operation, Action<T, Exception> reportFailure)
	{
		ArgumentNullException.ThrowIfNull(items, "items");
		int failedCount = 0;
		foreach (T item in items.DistinctBy<T, string>(keySelector, StringComparer.OrdinalIgnoreCase))
		{
			try
			{
				await operation(item).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception arg)
			{
				failedCount++;
				reportFailure(item, arg);
			}
		}
		return failedCount;
	}
}

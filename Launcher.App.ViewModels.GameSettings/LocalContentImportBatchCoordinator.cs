using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Launcher.App.ViewModels.GameSettings;

internal static class LocalContentImportBatchCoordinator
{
	public static async Task<LocalContentImportBatchResult<TResult>> ExecuteAsync<TResult>(IEnumerable<string> paths, Func<string, Task<TResult>> importAsync, Func<TResult, bool> isSuccess) where TResult : class
	{
		ArgumentNullException.ThrowIfNull(paths, "paths");
		ArgumentNullException.ThrowIfNull(importAsync, "importAsync");
		ArgumentNullException.ThrowIfNull(isSuccess, "isSuccess");
		int successCount = 0;
		foreach (string path in paths.Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			TResult val = await importAsync(path);
			if (isSuccess(val))
			{
				successCount++;
				continue;
			}
			return new LocalContentImportBatchResult<TResult>(successCount, path, val);
		}
		return new LocalContentImportBatchResult<TResult>(successCount, null, null);
	}
}

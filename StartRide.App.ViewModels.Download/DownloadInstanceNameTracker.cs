using System;
using System.Collections.Generic;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Download;

internal sealed class DownloadInstanceNameTracker
{
	private readonly object syncRoot = new object();

	private readonly HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> pendingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public void ReplaceExisting(IEnumerable<GameInstance> instances)
	{
		lock (syncRoot)
		{
			existingNames.Clear();
			foreach (GameInstance instance in instances)
			{
				AddNormalized(existingNames, instance.Name);
				AddNormalized(existingNames, instance.VersionName);
			}
		}
	}

	public void AddExisting(string? name)
	{
		string text = Normalize(name);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		lock (syncRoot)
		{
			existingNames.Add(text);
		}
	}

	public void AddPending(string? name)
	{
		string text = Normalize(name);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		lock (syncRoot)
		{
			pendingNames.Add(text);
		}
	}

	public void RemovePending(string? name)
	{
		string text = Normalize(name);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		lock (syncRoot)
		{
			pendingNames.Remove(text);
		}
	}

	public bool IsUnavailable(string? name)
	{
		string text = Normalize(name);
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		lock (syncRoot)
		{
			return existingNames.Contains(text) || pendingNames.Contains(text);
		}
	}

	private static void AddNormalized(ISet<string> names, string? name)
	{
		string text = Normalize(name);
		if (!string.IsNullOrWhiteSpace(text))
		{
			names.Add(text);
		}
	}

	private static string Normalize(string? name)
	{
		return name?.Trim() ?? string.Empty;
	}
}

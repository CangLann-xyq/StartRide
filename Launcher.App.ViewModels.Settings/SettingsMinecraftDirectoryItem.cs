using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.App.Resources;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsMinecraftDirectoryItem : ObservableObject
{
	private string displayName;

	private bool isAvailable;

	private bool canRemove;

	public string DisplayName
	{
		get
		{
			return displayName;
		}
		private set
		{
			SetProperty(ref displayName, value, "DisplayName");
		}
	}

	public string DirectoryPath { get; }

	public bool IsAvailable
	{
		get
		{
			return isAvailable;
		}
		private set
		{
			if (SetProperty(ref isAvailable, value, "IsAvailable"))
			{
				OnPropertyChanged("AvailabilityText");
			}
		}
	}

	public bool CanRemove
	{
		get
		{
			return canRemove;
		}
		private set
		{
			SetProperty(ref canRemove, value, "CanRemove");
		}
	}

	public string AvailabilityText
	{
		get
		{
			if (!IsAvailable)
			{
				return Strings.Settings_MinecraftDirectoryUnavailable;
			}
			return string.Empty;
		}
	}

	public SettingsMinecraftDirectoryItem(string displayName, string directoryPath, bool isAvailable, bool canRemove)
	{
		this.displayName = displayName;
		DirectoryPath = directoryPath;
		this.isAvailable = isAvailable;
		this.canRemove = canRemove;
	}

	public void Update(string newDisplayName, bool newIsAvailable, bool newCanRemove)
	{
		DisplayName = newDisplayName;
		IsAvailable = newIsAvailable;
		CanRemove = newCanRemove;
	}

	public void SetAvailability(bool newIsAvailable)
	{
		IsAvailable = newIsAvailable;
	}
}

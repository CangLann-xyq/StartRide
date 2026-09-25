using System;
using System.Threading.Tasks;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Settings;

public sealed class JavaSettingsViewModel : SettingsSectionViewModelBase
{
	public JavaSettingsEditorViewModel Editor { get; }

	public event EventHandler? LaunchDefaultsChanged;

	internal JavaSettingsViewModel(SettingsPersistenceCoordinator persistence, IJavaRuntimeDiscoveryService javaRuntimeDiscoveryService, IStatusService statusService, IFilePickerService filePickerService, IFloatingMessageService floatingMessageService, Func<string> minecraftDirectoryProvider)
		: base(persistence)
	{
		Editor = new JavaSettingsEditorViewModel(javaRuntimeDiscoveryService, statusService, filePickerService, floatingMessageService, minecraftDirectoryProvider);
		Editor.JavaSelectionChanged += Editor_JavaSelectionChanged;
	}

	public void Load(LauncherSettings settings)
	{
		LoadState(delegate
		{
			Editor.LoadSelection(settings.JavaSelectionMode, settings.SelectedJavaExecutablePath);
		});
		RefreshForDisplayAsync();
	}

	private async Task RefreshForDisplayAsync()
	{
		try
		{
			await Editor.RefreshJavaRuntimesForDisplayAsync();
		}
		catch (OperationCanceledException)
		{
		}
	}

	private void Editor_JavaSelectionChanged(object? sender, EventArgs e)
	{
		if (base.CanPersist)
		{
			Persist(delegate(LauncherSettings settings)
			{
				settings.JavaSelectionMode = Editor.SelectedMode;
				settings.SelectedJavaExecutablePath = ((Editor.SelectedMode == JavaSelectionMode.Manual && !string.IsNullOrWhiteSpace(Editor.SelectedExecutablePath)) ? Editor.SelectedExecutablePath : null);
			});
			LaunchDefaultsChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}

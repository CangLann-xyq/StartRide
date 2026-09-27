using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Settings;

public sealed class JavaSettingsEditorViewModel : ObservableObject
{
	private const string JavaSelectionAutoId = "auto";

	private const string JavaSelectionManualId = "manual";

	private readonly IJavaRuntimeDiscoveryService javaRuntimeDiscoveryService;

	private readonly IStatusService statusService;

	private readonly IFilePickerService filePickerService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly Func<string?> minecraftDirectoryProvider;

	private CancellationTokenSource? javaRuntimeScanCancellationTokenSource;

	private string? savedSelectedJavaExecutablePath;

	private bool suppressSelectionChanged;

	[ObservableProperty]
	private SettingsJavaSelectionOption? selectedJavaSelectionOption;

	[ObservableProperty]
	private SettingsJavaRuntimeItem? selectedJavaRuntime;

	[ObservableProperty]
	private bool isJavaRuntimeScanRunning;

	[ObservableProperty]
	private string javaRuntimeListMessage = Strings.Settings_JavaListEmpty;

	[ObservableProperty]
	private bool isEditorEnabled = true;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshJavaRuntimesCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? importJavaRuntimeCommand;

	public ObservableCollection<SettingsJavaSelectionOption> JavaSelectionOptions { get; } = new ObservableCollection<SettingsJavaSelectionOption>();

	public ObservableCollection<SettingsJavaRuntimeItem> JavaRuntimes { get; } = new ObservableCollection<SettingsJavaRuntimeItem>();

	public bool HasJavaRuntimeListMessage => !string.IsNullOrWhiteSpace(JavaRuntimeListMessage);

	public bool IsJavaManualSelection => SelectedMode == JavaSelectionMode.Manual;

	public JavaSelectionMode SelectedMode
	{
		get
		{
			if (!(SelectedJavaSelectionOption?.Id == "manual"))
			{
				return JavaSelectionMode.Auto;
			}
			return JavaSelectionMode.Manual;
		}
	}

	public string? SelectedExecutablePath
	{
		get
		{
			if (SelectedMode != JavaSelectionMode.Manual || SelectedJavaRuntime == null)
			{
				return savedSelectedJavaExecutablePath;
			}
			return SelectedJavaRuntime.ExecutablePath;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsJavaSelectionOption? SelectedJavaSelectionOption
	{
		get
		{
			return selectedJavaSelectionOption;
		}
		set
		{
			if (!EqualityComparer<SettingsJavaSelectionOption>.Default.Equals(selectedJavaSelectionOption, value))
			{
				SettingsJavaSelectionOption oldValue = selectedJavaSelectionOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedJavaSelectionOption);
				selectedJavaSelectionOption = value;
				OnSelectedJavaSelectionOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedJavaSelectionOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsJavaRuntimeItem? SelectedJavaRuntime
	{
		get
		{
			return selectedJavaRuntime;
		}
		set
		{
			if (!EqualityComparer<SettingsJavaRuntimeItem>.Default.Equals(selectedJavaRuntime, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedJavaRuntime);
				selectedJavaRuntime = value;
				OnSelectedJavaRuntimeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedJavaRuntime);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsJavaRuntimeScanRunning
	{
		get
		{
			return isJavaRuntimeScanRunning;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isJavaRuntimeScanRunning, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsJavaRuntimeScanRunning);
				isJavaRuntimeScanRunning = value;
				OnIsJavaRuntimeScanRunningChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsJavaRuntimeScanRunning);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string JavaRuntimeListMessage
	{
		get
		{
			return javaRuntimeListMessage;
		}
		[MemberNotNull("javaRuntimeListMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(javaRuntimeListMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.JavaRuntimeListMessage);
				javaRuntimeListMessage = value;
				OnJavaRuntimeListMessageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.JavaRuntimeListMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsEditorEnabled
	{
		get
		{
			return isEditorEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isEditorEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsEditorEnabled);
				isEditorEnabled = value;
				OnIsEditorEnabledChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsEditorEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshJavaRuntimesCommand => refreshJavaRuntimesCommand ?? (refreshJavaRuntimesCommand = new AsyncRelayCommand(RefreshJavaRuntimesAsync, CanRefreshJavaRuntimes));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ImportJavaRuntimeCommand => importJavaRuntimeCommand ?? (importJavaRuntimeCommand = new AsyncRelayCommand(ImportJavaRuntimeAsync, CanImportJavaRuntime));

	public event EventHandler? JavaSelectionChanged;

	public JavaSettingsEditorViewModel(IJavaRuntimeDiscoveryService javaRuntimeDiscoveryService, IStatusService statusService, IFilePickerService filePickerService, IFloatingMessageService floatingMessageService, Func<string?> minecraftDirectoryProvider)
	{
		this.javaRuntimeDiscoveryService = javaRuntimeDiscoveryService;
		this.statusService = statusService;
		this.filePickerService = filePickerService;
		this.floatingMessageService = floatingMessageService;
		this.minecraftDirectoryProvider = minecraftDirectoryProvider;
		JavaSelectionOptions.Add(new SettingsJavaSelectionOption("auto", Strings.Settings_JavaSelectionAuto));
		JavaSelectionOptions.Add(new SettingsJavaSelectionOption("manual", Strings.Settings_JavaSelectionManual));
		SelectedJavaSelectionOption = JavaSelectionOptions[0];
	}

	public void LoadSelection(JavaSelectionMode mode, string? selectedJavaExecutablePath)
	{
		suppressSelectionChanged = true;
		try
		{
			savedSelectedJavaExecutablePath = (string.IsNullOrWhiteSpace(selectedJavaExecutablePath) ? null : selectedJavaExecutablePath);
			SelectedJavaSelectionOption = GetJavaSelectionOption(mode);
			SelectedJavaRuntime = null;
			UpdateJavaRuntimeSelectionAfterListChanged();
		}
		finally
		{
			suppressSelectionChanged = false;
		}
	}

	[RelayCommand(CanExecute = "CanRefreshJavaRuntimes")]
	public async Task RefreshJavaRuntimesAsync()
	{
		await RefreshJavaRuntimesCoreAsync(allowWhenDisabled: false);
	}

	public async Task RefreshJavaRuntimesForDisplayAsync()
	{
		await RefreshJavaRuntimesCoreAsync(allowWhenDisabled: true);
	}

	private async Task RefreshJavaRuntimesCoreAsync(bool allowWhenDisabled)
	{
		if (IsJavaRuntimeScanRunning || (!allowWhenDisabled && !IsEditorEnabled))
		{
			return;
		}
		javaRuntimeScanCancellationTokenSource?.Cancel();
		javaRuntimeScanCancellationTokenSource?.Dispose();
		CancellationTokenSource cancellationTokenSource = (javaRuntimeScanCancellationTokenSource = new CancellationTokenSource());
		IsJavaRuntimeScanRunning = true;
		JavaRuntimeListMessage = Strings.Settings_JavaListLoading;
		try
		{
			IReadOnlyList<JavaRuntimeInfo> obj = await javaRuntimeDiscoveryService.DiscoverAsync(minecraftDirectoryProvider(), cancellationTokenSource.Token);
			JavaRuntimes.Clear();
			foreach (JavaRuntimeInfo item in obj)
			{
				JavaRuntimes.Add(new SettingsJavaRuntimeItem(item));
			}
			await EnsureSavedSelectedJavaRuntimePresentAsync(cancellationTokenSource.Token);
			UpdateJavaRuntimeSelectionAfterListChanged();
			JavaRuntimeListMessage = ((JavaRuntimes.Count == 0) ? Strings.Settings_JavaListEmpty : string.Empty);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
			JavaRuntimes.Clear();
			JavaRuntimeListMessage = Strings.Settings_JavaListEmpty;
			statusService.Report(Strings.Status_JavaScanFailed);
		}
		finally
		{
			if (javaRuntimeScanCancellationTokenSource == cancellationTokenSource)
			{
				IsJavaRuntimeScanRunning = false;
				cancellationTokenSource.Dispose();
				javaRuntimeScanCancellationTokenSource = null;
			}
		}
	}

	[RelayCommand(CanExecute = "CanImportJavaRuntime")]
	public async Task ImportJavaRuntimeAsync()
	{
		if (!IsEditorEnabled)
		{
			return;
		}
		string text = filePickerService.PickJavaExecutable();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		try
		{
			JavaRuntimeInfo runtime = await javaRuntimeDiscoveryService.ImportExecutableAsync(text);
			bool flag = !AddJavaRuntime(runtime);
			SettingsJavaRuntimeItem settingsJavaRuntimeItem = JavaRuntimes.First((SettingsJavaRuntimeItem item) => IsSameJavaRuntime(item, runtime));
			if (IsJavaManualSelection)
			{
				suppressSelectionChanged = true;
				try
				{
					savedSelectedJavaExecutablePath = runtime.ExecutablePath;
					SelectedJavaRuntime = settingsJavaRuntimeItem;
				}
				finally
				{
					suppressSelectionChanged = false;
				}
				RaiseJavaSelectionChanged();
			}
			if (flag)
			{
				floatingMessageService.Show(Strings.Status_JavaAlreadyExists);
			}
			JavaRuntimeListMessage = ((JavaRuntimes.Count == 0) ? Strings.Settings_JavaListEmpty : string.Empty);
			if (!flag)
			{
				statusService.Report(Strings.Status_JavaImported);
			}
		}
		catch (Exception)
		{
			statusService.Report(Strings.Status_JavaImportFailed);
		}
	}

	private bool CanRefreshJavaRuntimes()
	{
		if (IsEditorEnabled)
		{
			return !IsJavaRuntimeScanRunning;
		}
		return false;
	}

	private bool CanImportJavaRuntime()
	{
		return IsEditorEnabled;
	}

	private bool AddJavaRuntime(JavaRuntimeInfo runtime)
	{
		if (JavaRuntimes.Any((SettingsJavaRuntimeItem item) => IsSameJavaRuntime(item, runtime)))
		{
			return false;
		}
		SettingsJavaRuntimeItem settingsJavaRuntimeItem = new SettingsJavaRuntimeItem(runtime);
		int num;
		for (num = 0; num < JavaRuntimes.Count && JavaRuntimes[num].MajorVersion.GetValueOrDefault() > settingsJavaRuntimeItem.MajorVersion.GetValueOrDefault(); num++)
		{
		}
		JavaRuntimes.Insert(num, settingsJavaRuntimeItem);
		return true;
	}

	private async Task EnsureSavedSelectedJavaRuntimePresentAsync(CancellationToken cancellationToken)
	{
		if (!IsJavaManualSelection || string.IsNullOrWhiteSpace(savedSelectedJavaExecutablePath) || JavaRuntimes.Any((SettingsJavaRuntimeItem item) => IsSameExecutablePath(item.ExecutablePath, savedSelectedJavaExecutablePath)))
		{
			return;
		}
		try
		{
			AddJavaRuntime(await javaRuntimeDiscoveryService.DiscoverExecutableAsync(savedSelectedJavaExecutablePath, cancellationToken));
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
		}
	}

	private void UpdateJavaRuntimeSelectionAfterListChanged()
	{
		if (!IsJavaManualSelection)
		{
			SelectedJavaRuntime = null;
			return;
		}
		SettingsJavaRuntimeItem settingsJavaRuntimeItem = (string.IsNullOrWhiteSpace(savedSelectedJavaExecutablePath) ? null : JavaRuntimes.FirstOrDefault((SettingsJavaRuntimeItem item) => IsSameExecutablePath(item.ExecutablePath, savedSelectedJavaExecutablePath)));
		SettingsJavaRuntimeItem settingsJavaRuntimeItem2 = ((SelectedJavaRuntime == null) ? null : JavaRuntimes.FirstOrDefault((SettingsJavaRuntimeItem item) => IsSameExecutablePath(item.ExecutablePath, SelectedJavaRuntime.ExecutablePath)));
		SelectedJavaRuntime = settingsJavaRuntimeItem ?? settingsJavaRuntimeItem2 ?? JavaRuntimes.FirstOrDefault();
	}

	private SettingsJavaSelectionOption GetJavaSelectionOption(JavaSelectionMode mode)
	{
		string targetId = ((mode == JavaSelectionMode.Manual) ? "manual" : "auto");
		return JavaSelectionOptions.FirstOrDefault((SettingsJavaSelectionOption option) => option.Id == targetId) ?? JavaSelectionOptions[0];
	}

	private void RaiseJavaSelectionChanged()
	{
		if (!suppressSelectionChanged)
		{
			OnPropertyChanged("SelectedExecutablePath");
			JavaSelectionChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private static bool IsSameJavaRuntime(SettingsJavaRuntimeItem item, JavaRuntimeInfo runtime)
	{
		if (IsSameExecutablePath(item.ExecutablePath, runtime.ExecutablePath))
		{
			return true;
		}
		if (string.IsNullOrWhiteSpace(runtime.Version))
		{
			return false;
		}
		if (string.Equals(item.InstallationDirectory, runtime.InstallationDirectory, StringComparison.OrdinalIgnoreCase) && string.Equals(item.VersionText, runtime.Version, StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(item.Architecture, runtime.Architecture, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool IsSameExecutablePath(string? left, string? right)
	{
		return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedJavaSelectionOptionChanged(SettingsJavaSelectionOption? oldValue, SettingsJavaSelectionOption? newValue)
	{
		if (newValue == null)
		{
			bool flag = suppressSelectionChanged;
			suppressSelectionChanged = true;
			try
			{
				SelectedJavaSelectionOption = oldValue ?? JavaSelectionOptions[0];
				return;
			}
			finally
			{
				suppressSelectionChanged = flag;
			}
		}
		OnPropertyChanged("IsJavaManualSelection");
		OnPropertyChanged("SelectedMode");
		if (IsJavaManualSelection)
		{
			UpdateJavaRuntimeSelectionAfterListChanged();
		}
		else
		{
			suppressSelectionChanged = true;
			try
			{
				SelectedJavaRuntime = null;
			}
			finally
			{
				suppressSelectionChanged = false;
			}
		}
		RaiseJavaSelectionChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedJavaRuntimeChanged(SettingsJavaRuntimeItem? value)
	{
		if (suppressSelectionChanged)
		{
			return;
		}
		if (!IsJavaManualSelection)
		{
			if (value != null)
			{
				suppressSelectionChanged = true;
				try
				{
					SelectedJavaRuntime = null;
				}
				finally
				{
					suppressSelectionChanged = false;
				}
			}
		}
		else
		{
			if (value != null)
			{
				savedSelectedJavaExecutablePath = value.ExecutablePath;
			}
			OnPropertyChanged("SelectedExecutablePath");
			RaiseJavaSelectionChanged();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsJavaRuntimeScanRunningChanged(bool value)
	{
		RefreshJavaRuntimesCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnJavaRuntimeListMessageChanged(string value)
	{
		OnPropertyChanged("HasJavaRuntimeListMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsEditorEnabledChanged(bool value)
	{
		RefreshJavaRuntimesCommand.NotifyCanExecuteChanged();
		ImportJavaRuntimeCommand.NotifyCanExecuteChanged();
	}
}

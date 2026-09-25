using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceExportSettingsViewModel : GameSettingsDetailsSectionViewModelBase
{
	private const string DefaultExportVersion = "1.0.0";

	private readonly IModpackExportService? modpackExportService;

	private readonly IFilePickerService filePickerService;

	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	[ObservableProperty]
	private InstanceExportTypeOption? selectedExportTypeOption;

	[ObservableProperty]
	private string exportModpackName = string.Empty;

	[ObservableProperty]
	private string exportAuthor = string.Empty;

	[ObservableProperty]
	private string exportVersion = string.Empty;

	[ObservableProperty]
	private bool packMods = true;

	[ObservableProperty]
	private bool packDisabledMods;

	[ObservableProperty]
	private bool packResourcePacks = true;

	[ObservableProperty]
	private bool packShaderPacks = true;

	[ObservableProperty]
	private bool packSaves = true;

	[ObservableProperty]
	private bool isExporting;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? exportCommand;

	public override bool UsesFullViewportLayout => true;

	public bool CanExport
	{
		get
		{
			if (!IsExporting && modpackExportService != null && base.Parent.SelectedInstance != null)
			{
				return !IsExportModpackNameEmpty;
			}
			return false;
		}
	}

	public bool CanPackDisabledMods => PackMods;

	public bool IsExportModpackNameEmpty => string.IsNullOrWhiteSpace(ExportModpackName);

	public ObservableCollection<InstanceExportTypeOption> ExportTypeOptions { get; } = new ObservableCollection<InstanceExportTypeOption>();

	private ModpackExportKind SelectedExportKind
	{
		get
		{
			if (!string.Equals(SelectedExportTypeOption?.Id, "modrinth", StringComparison.OrdinalIgnoreCase))
			{
				return ModpackExportKind.CurseForge;
			}
			return ModpackExportKind.Modrinth;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public InstanceExportTypeOption? SelectedExportTypeOption
	{
		get
		{
			return selectedExportTypeOption;
		}
		set
		{
			if (!EqualityComparer<InstanceExportTypeOption>.Default.Equals(selectedExportTypeOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedExportTypeOption);
				selectedExportTypeOption = value;
				OnSelectedExportTypeOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedExportTypeOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ExportModpackName
	{
		get
		{
			return exportModpackName;
		}
		[MemberNotNull("exportModpackName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(exportModpackName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ExportModpackName);
				exportModpackName = value;
				OnExportModpackNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ExportModpackName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ExportAuthor
	{
		get
		{
			return exportAuthor;
		}
		[MemberNotNull("exportAuthor")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(exportAuthor, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ExportAuthor);
				exportAuthor = value;
				OnExportAuthorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ExportAuthor);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ExportVersion
	{
		get
		{
			return exportVersion;
		}
		[MemberNotNull("exportVersion")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(exportVersion, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ExportVersion);
				exportVersion = value;
				OnExportVersionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ExportVersion);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool PackMods
	{
		get
		{
			return packMods;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(packMods, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PackMods);
				packMods = value;
				OnPackModsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PackMods);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool PackDisabledMods
	{
		get
		{
			return packDisabledMods;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(packDisabledMods, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PackDisabledMods);
				packDisabledMods = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PackDisabledMods);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool PackResourcePacks
	{
		get
		{
			return packResourcePacks;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(packResourcePacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PackResourcePacks);
				packResourcePacks = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PackResourcePacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool PackShaderPacks
	{
		get
		{
			return packShaderPacks;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(packShaderPacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PackShaderPacks);
				packShaderPacks = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PackShaderPacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool PackSaves
	{
		get
		{
			return packSaves;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(packSaves, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PackSaves);
				packSaves = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PackSaves);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsExporting
	{
		get
		{
			return isExporting;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isExporting, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsExporting);
				isExporting = value;
				OnIsExportingChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsExporting);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ExportCommand => exportCommand ?? (exportCommand = new AsyncRelayCommand(ExportAsync, () => CanExport));

	public InstanceExportSettingsViewModel(GameSettingsDetailsViewModel parent, IFilePickerService filePickerService, IStatusService statusService, IFloatingMessageService floatingMessageService, IModpackExportService? modpackExportService = null)
		: base(parent)
	{
		this.filePickerService = filePickerService;
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.modpackExportService = modpackExportService;
		ExportTypeOptions.Add(new InstanceExportTypeOption("curseforge", Strings.GameSettings_ExportTypeCurseForge));
		ExportTypeOptions.Add(new InstanceExportTypeOption("modrinth", Strings.GameSettings_ExportTypeModrinth));
		SelectedExportTypeOption = ExportTypeOptions[0];
	}

	public override void OnSelectedInstanceChanged(GameInstance? instance)
	{
		OnPropertyChanged("CanExport");
		ExportCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = "CanExport")]
	private async Task ExportAsync()
	{
		GameInstance gameInstance = base.Parent.SelectedInstance?.Instance;
		if (gameInstance == null || modpackExportService == null)
		{
			return;
		}
		ModpackExportKind selectedExportKind = SelectedExportKind;
		string text = filePickerService.PickModpackExportArchive(CreateDefaultArchiveFileName(selectedExportKind), selectedExportKind);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		IsExporting = true;
		floatingMessageService.Show(Strings.Status_ModpackExporting);
		try
		{
			ModpackExportResult modpackExportResult = await modpackExportService.ExportAsync(new ModpackExportRequest(gameInstance, selectedExportKind, ExportModpackName, ExportAuthor, ResolveExportVersion(), text, PackMods, PackDisabledMods, PackResourcePacks, PackShaderPacks, PackSaves));
			if (modpackExportResult.IsSuccess)
			{
				floatingMessageService.Show(Strings.Status_ModpackExported);
				statusService.Report(string.Format(Strings.Status_ModpackExportedFormat, modpackExportResult.OutputArchivePath));
			}
			else
			{
				statusService.Report(ResolveFailureMessage(modpackExportResult.FailureReason));
			}
		}
		finally
		{
			IsExporting = false;
		}
	}

	private void NotifyCanExportChanged()
	{
		OnPropertyChanged("CanExport");
		OnPropertyChanged("IsExportModpackNameEmpty");
		ExportCommand.NotifyCanExecuteChanged();
	}

	private string CreateDefaultArchiveFileName(ModpackExportKind kind)
	{
		string text = (string.IsNullOrWhiteSpace(ExportModpackName) ? "modpack" : ExportModpackName.Trim());
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		string text2 = ((kind == ModpackExportKind.Modrinth) ? ".mrpack" : ".zip");
		if (!text.EndsWith(text2, StringComparison.OrdinalIgnoreCase))
		{
			return text + text2;
		}
		return text;
	}

	private string ResolveExportVersion()
	{
		if (!string.IsNullOrWhiteSpace(ExportVersion))
		{
			return ExportVersion;
		}
		return "1.0.0";
	}

	private static string ResolveFailureMessage(ModpackExportFailureReason reason)
	{
		return reason switch
		{
			ModpackExportFailureReason.MissingCurseForgeApiKey => Strings.Status_ModpackExportMissingCurseForgeApiKey, 
			ModpackExportFailureReason.MissingLoaderVersion => Strings.Status_ModpackExportMissingLoaderVersion, 
			ModpackExportFailureReason.UnsupportedType => Strings.Status_ModrinthExportUnsupported, 
			ModpackExportFailureReason.CurseForgeApiFailed => Strings.Status_ModpackExportCurseForgeApiFailed, 
			ModpackExportFailureReason.ModrinthApiFailed => Strings.Status_ModpackExportModrinthApiFailed, 
			ModpackExportFailureReason.InvalidRequest => Strings.Status_ModpackExportInvalidRequest, 
			ModpackExportFailureReason.FileSystemError => Strings.Status_ModpackExportFileSystemFailed, 
			_ => Strings.Status_ModpackExportFailed, 
		};
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedExportTypeOptionChanged(InstanceExportTypeOption? value)
	{
		NotifyCanExportChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnExportModpackNameChanged(string value)
	{
		NotifyCanExportChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnExportAuthorChanged(string value)
	{
		NotifyCanExportChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnExportVersionChanged(string value)
	{
		NotifyCanExportChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnPackModsChanged(bool value)
	{
		if (!value)
		{
			PackDisabledMods = false;
		}
		OnPropertyChanged("CanPackDisabledMods");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsExportingChanged(bool value)
	{
		NotifyCanExportChanged();
	}
}

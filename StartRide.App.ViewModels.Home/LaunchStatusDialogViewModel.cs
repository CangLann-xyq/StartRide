using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.Home;

public sealed class LaunchStatusDialogViewModel : ObservableObject
{
	private const int MaxVisibleAnalysisDetails = 5;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFilePickerService filePickerService;

	private readonly ILaunchDiagnosticExportService diagnosticExportService;

	private readonly IStatusService statusService;

	private readonly ILogger<LaunchStatusDialogViewModel> logger;

	private IReadOnlyList<LaunchDiagnosticReference> diagnosticCandidates = Array.Empty<LaunchDiagnosticReference>();

	private IReadOnlyList<string> exportSensitiveValues = Array.Empty<string>();

	private string instanceName = string.Empty;

	private string versionName = string.Empty;

	[ObservableProperty]
	private bool isOpen;

	[ObservableProperty]
	private string title = string.Empty;

	[ObservableProperty]
	private string message = string.Empty;

	[ObservableProperty]
	private string diagnosticHint = string.Empty;

	[ObservableProperty]
	private bool hasAnalysis;

	[ObservableProperty]
	private string analysisReasonTitle = string.Empty;

	[ObservableProperty]
	private string analysisReasonDetail = string.Empty;

	[ObservableProperty]
	private string analysisRecommendation = string.Empty;

	[ObservableProperty]
	private bool isExporting;

	[ObservableProperty]
	private IReadOnlyList<LaunchAnalysisDetailItem> analysisDetails = Array.Empty<LaunchAnalysisDetailItem>();

	[ObservableProperty]
	private bool hasAnalysisDetails;

	[ObservableProperty]
	private string analysisAdditionalDetailsHint = string.Empty;

	[ObservableProperty]
	private bool hasAdditionalAnalysisDetails;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? viewReportCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? exportReportCommand;

	public bool CanViewReport
	{
		get
		{
			if (IsOpen)
			{
				return diagnosticCandidates.Count > 0;
			}
			return false;
		}
	}

	public bool CanExportReport
	{
		get
		{
			if (IsOpen && !IsExporting)
			{
				return diagnosticCandidates.Count > 0;
			}
			return false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsOpen
	{
		get
		{
			return isOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOpen);
				isOpen = value;
				OnIsOpenChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Title
	{
		get
		{
			return title;
		}
		[MemberNotNull("title")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(title, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Title);
				title = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Title);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Message
	{
		get
		{
			return message;
		}
		[MemberNotNull("message")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(message, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Message);
				message = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Message);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DiagnosticHint
	{
		get
		{
			return diagnosticHint;
		}
		[MemberNotNull("diagnosticHint")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(diagnosticHint, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DiagnosticHint);
				diagnosticHint = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DiagnosticHint);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasAnalysis
	{
		get
		{
			return hasAnalysis;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasAnalysis, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasAnalysis);
				hasAnalysis = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasAnalysis);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AnalysisReasonTitle
	{
		get
		{
			return analysisReasonTitle;
		}
		[MemberNotNull("analysisReasonTitle")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(analysisReasonTitle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AnalysisReasonTitle);
				analysisReasonTitle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AnalysisReasonTitle);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AnalysisReasonDetail
	{
		get
		{
			return analysisReasonDetail;
		}
		[MemberNotNull("analysisReasonDetail")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(analysisReasonDetail, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AnalysisReasonDetail);
				analysisReasonDetail = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AnalysisReasonDetail);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AnalysisRecommendation
	{
		get
		{
			return analysisRecommendation;
		}
		[MemberNotNull("analysisRecommendation")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(analysisRecommendation, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AnalysisRecommendation);
				analysisRecommendation = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AnalysisRecommendation);
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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<LaunchAnalysisDetailItem> AnalysisDetails
	{
		get
		{
			return analysisDetails;
		}
		[MemberNotNull("analysisDetails")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<LaunchAnalysisDetailItem>>.Default.Equals(analysisDetails, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AnalysisDetails);
				analysisDetails = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AnalysisDetails);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasAnalysisDetails
	{
		get
		{
			return hasAnalysisDetails;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasAnalysisDetails, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasAnalysisDetails);
				hasAnalysisDetails = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasAnalysisDetails);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AnalysisAdditionalDetailsHint
	{
		get
		{
			return analysisAdditionalDetailsHint;
		}
		[MemberNotNull("analysisAdditionalDetailsHint")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(analysisAdditionalDetailsHint, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AnalysisAdditionalDetailsHint);
				analysisAdditionalDetailsHint = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AnalysisAdditionalDetailsHint);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasAdditionalAnalysisDetails
	{
		get
		{
			return hasAdditionalAnalysisDetails;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasAdditionalAnalysisDetails, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasAdditionalAnalysisDetails);
				hasAdditionalAnalysisDetails = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasAdditionalAnalysisDetails);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseCommand => closeCommand ?? (closeCommand = new RelayCommand(Close));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ViewReportCommand => viewReportCommand ?? (viewReportCommand = new RelayCommand(ViewReport, () => CanViewReport));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ExportReportCommand => exportReportCommand ?? (exportReportCommand = new AsyncRelayCommand(ExportReportAsync, () => CanExportReport));

	public LaunchStatusDialogViewModel(IInstanceFolderService instanceFolderService, IFilePickerService filePickerService, ILaunchDiagnosticExportService diagnosticExportService, IStatusService statusService, ILogger<LaunchStatusDialogViewModel>? logger = null)
	{
		this.instanceFolderService = instanceFolderService;
		this.filePickerService = filePickerService;
		this.diagnosticExportService = diagnosticExportService;
		this.statusService = statusService;
		this.logger = logger ?? NullLogger<LaunchStatusDialogViewModel>.Instance;
	}

	public void Show(LaunchFailureReport report)
	{
		instanceName = (string.IsNullOrWhiteSpace(report.InstanceName) ? report.VersionName : report.InstanceName);
		versionName = report.VersionName;
		IReadOnlyList<LaunchDiagnosticReference> readOnlyList2;
		if (report.DiagnosticCandidates.Count <= 0)
		{
			LaunchDiagnosticReference primaryDiagnostic = report.PrimaryDiagnostic;
			if ((object)primaryDiagnostic == null)
			{
				IReadOnlyList<LaunchDiagnosticReference> readOnlyList = Array.Empty<LaunchDiagnosticReference>();
				readOnlyList2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<LaunchDiagnosticReference> readOnlyList = new global::_003C_003Ez__ReadOnlySingleElementList<LaunchDiagnosticReference>(primaryDiagnostic);
				readOnlyList2 = readOnlyList;
			}
		}
		else
		{
			readOnlyList2 = report.DiagnosticCandidates;
		}
		diagnosticCandidates = readOnlyList2;
		exportSensitiveValues = report.ExportSensitiveValues.ToArray();
		Title = GetTitle(report.Kind);
		ApplyAnalysis(report.Analysis);
		Message = string.Format(Strings.Dialog_LaunchStatusMessageFormat, string.IsNullOrWhiteSpace(report.InstanceName) ? report.VersionName : report.InstanceName, HasAnalysis ? AnalysisReasonDetail : GetDescription(report));
		DiagnosticHint = (string.IsNullOrWhiteSpace(report.DiagnosticPath) ? Strings.Dialog_LaunchStatusDiagnosticDirectoryHint : Strings.Dialog_LaunchStatusDiagnosticFileHint);
		IsOpen = true;
		ViewReportCommand.NotifyCanExecuteChanged();
		ExportReportCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	private void Close()
	{
		IsOpen = false;
		ViewReportCommand.NotifyCanExecuteChanged();
		ExportReportCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = "CanViewReport")]
	private void ViewReport()
	{
		foreach (LaunchDiagnosticReference diagnosticCandidate in diagnosticCandidates)
		{
			if (instanceFolderService.TryOpenFile(diagnosticCandidate.Path))
			{
				return;
			}
		}
		statusService.Report(Strings.Status_OpenLaunchReportFailed);
	}

	[RelayCommand(CanExecute = "CanExportReport")]
	private async Task ExportReportAsync()
	{
		string text = filePickerService.PickLaunchDiagnosticExportArchive(instanceName);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		IsExporting = true;
		try
		{
			LaunchDiagnosticExportResult launchDiagnosticExportResult = await diagnosticExportService.ExportAsync(new LaunchDiagnosticExportRequest(text, instanceName, versionName, diagnosticCandidates)
			{
				SensitiveValues = exportSensitiveValues
			});
			if (launchDiagnosticExportResult.IsSuccess)
			{
				statusService.Report((launchDiagnosticExportResult.SkippedFileCount > 0) ? string.Format(Strings.Status_LaunchReportExportPartialFormat, launchDiagnosticExportResult.ExportedFileCount, launchDiagnosticExportResult.SkippedFileCount) : string.Format(Strings.Status_LaunchReportExportSucceededFormat, launchDiagnosticExportResult.ExportedFileCount));
			}
			else
			{
				statusService.Report((launchDiagnosticExportResult.FailureReason == LaunchDiagnosticExportFailureReason.NoReadableDiagnostics) ? Strings.Status_LaunchReportExportNoReadableFiles : Strings.Status_LaunchReportExportFailed);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Launch diagnostic export command failed. InstanceName={InstanceName} VersionName={VersionName}", instanceName, versionName);
			statusService.Report(Strings.Status_LaunchReportExportFailed);
		}
		finally
		{
			IsExporting = false;
		}
	}

	private static string GetTitle(LaunchFailureKind kind)
	{
		return kind switch
		{
			LaunchFailureKind.StartupProcessExited => Strings.Dialog_LaunchStatusExitedTitle, 
			LaunchFailureKind.StartupAbnormalExit => Strings.Dialog_LaunchStatusInitializationFailedTitle, 
			LaunchFailureKind.RuntimeAbnormalExit => Strings.Dialog_LaunchStatusRuntimeFailedTitle, 
			_ => Strings.Dialog_LaunchStatusFailedTitle, 
		};
	}

	private static string GetDescription(LaunchFailureReport report)
	{
		return report.Kind switch
		{
			LaunchFailureKind.StartupProcessExited => Strings.Dialog_LaunchStatusStartupExitedMessage, 
			LaunchFailureKind.RuntimeAbnormalExit => FormatExitCode(Strings.Dialog_LaunchStatusRuntimeAbnormalMessage, report.ExitCode), 
			LaunchFailureKind.StartupAbnormalExit => FormatExitCode(Strings.Dialog_LaunchStatusStartupAbnormalMessage, report.ExitCode), 
			_ => Strings.Dialog_LaunchStatusStartupFailedMessage, 
		};
	}

	private static string FormatExitCode(string format, int? exitCode)
	{
		return string.Format(format, (!exitCode.HasValue) ? Strings.Dialog_LaunchStatusUnknownExitCode : exitCode.GetValueOrDefault().ToString());
	}

	private void ApplyAnalysis(LaunchFailureAnalysis? analysis)
	{
		if ((object)analysis == null)
		{
			HasAnalysis = false;
			AnalysisReasonTitle = string.Empty;
			AnalysisReasonDetail = string.Empty;
			AnalysisRecommendation = string.Empty;
			AnalysisDetails = Array.Empty<LaunchAnalysisDetailItem>();
			HasAnalysisDetails = false;
			AnalysisAdditionalDetailsHint = string.Empty;
			HasAdditionalAnalysisDetails = false;
		}
		else
		{
			HasAnalysis = true;
			AnalysisReasonTitle = GetAnalysisReasonTitle(analysis);
			AnalysisReasonDetail = GetAnalysisReasonDetail(analysis);
			AnalysisRecommendation = GetAnalysisRecommendation(analysis);
			AnalysisDetails = BuildAnalysisDetails(analysis);
			HasAnalysisDetails = AnalysisDetails.Count > 0;
			int num = ((analysis.Details.Count > 0) ? Math.Max(0, analysis.Details.Count - 5) : Math.Max(0, analysis.Evidence.Count - 5));
			AnalysisAdditionalDetailsHint = ((num > 0) ? string.Format(Strings.Dialog_LaunchAnalysisAdditionalDetailsFormat, num) : string.Empty);
			HasAdditionalAnalysisDetails = num > 0;
		}
	}

	private static IReadOnlyList<LaunchAnalysisDetailItem> BuildAnalysisDetails(LaunchFailureAnalysis analysis)
	{
		if (analysis.Details.Count > 0)
		{
			return (from detail in analysis.Details.Take(5)
				select new LaunchAnalysisDetailItem(GetAnalysisDetailSummary(detail), detail.OriginalReason ?? string.Empty, detail.OriginalSuggestion ?? string.Empty)).ToArray();
		}
		if (analysis.Evidence.Count == 0)
		{
			return Array.Empty<LaunchAnalysisDetailItem>();
		}
		LaunchFailureEvidence[] source = analysis.Evidence.Take(5).ToArray();
		IEnumerable<string> values = from item in source
			where item.Kind == LaunchFailureEvidenceKind.Reason
			select item.Text;
		IEnumerable<string> values2 = from item in source
			where item.Kind == LaunchFailureEvidenceKind.Suggestion
			select item.Text;
		return new global::_003C_003Ez__ReadOnlySingleElementList<LaunchAnalysisDetailItem>(new LaunchAnalysisDetailItem(GetAnalysisReasonDetail(analysis), string.Join(Environment.NewLine, values), string.Join(Environment.NewLine, values2)));
	}

	private static string GetAnalysisDetailSummary(LaunchFailureDetail detail)
	{
		string arg = FormatNameAndVersion(detail.ModName, detail.ModVersion);
		string arg2 = FormatNameAndVersion(detail.DependencyName, detail.RequiredVersion);
		switch (detail.Kind)
		{
		case LaunchFailureDetailKind.MissingDependency:
			return string.Format(Strings.Dialog_LaunchAnalysisDetailMissingDependencyFormat, arg, arg2);
		case LaunchFailureDetailKind.IncompatibleDependencyVersion:
		case LaunchFailureDetailKind.IncompatibleMinecraftVersion:
		case LaunchFailureDetailKind.IncompatibleLoaderVersion:
			return string.Format(Strings.Dialog_LaunchAnalysisDetailWrongVersionFormat, arg, arg2, string.IsNullOrWhiteSpace(detail.CurrentVersion) ? Strings.Dialog_LaunchStatusUnknownExitCode : detail.CurrentVersion);
		default:
			return string.Format(Strings.Dialog_LaunchAnalysisDetailConflictFormat, arg);
		}
	}

	private static string FormatNameAndVersion(string? name, string? version)
	{
		string text = (string.IsNullOrWhiteSpace(name) ? Strings.Dialog_LaunchAnalysisCurrentInstance : name);
		if (!string.IsNullOrWhiteSpace(version))
		{
			return text + " " + version;
		}
		return text;
	}

	private static string GetAnalysisReasonTitle(LaunchFailureAnalysis analysis)
	{
		return analysis.Category switch
		{
			LaunchFailureCategory.JavaVersionMismatch => Strings.Dialog_LaunchAnalysisJavaVersionTitle, 
			LaunchFailureCategory.ModDependencyMissing => Strings.Dialog_LaunchAnalysisModDependencyTitle, 
			LaunchFailureCategory.ModVersionIncompatible => Strings.Dialog_LaunchAnalysisModVersionTitle, 
			LaunchFailureCategory.MissingGameFiles => Strings.Dialog_LaunchAnalysisMissingFilesTitle, 
			LaunchFailureCategory.GameFileIntegrity => Strings.Dialog_LaunchAnalysisGameFileIntegrityTitle, 
			LaunchFailureCategory.OutOfMemory => Strings.Dialog_LaunchAnalysisOutOfMemoryTitle, 
			_ => Strings.Dialog_LaunchAnalysisUnknownTitle, 
		};
	}

	private static string GetAnalysisReasonDetail(LaunchFailureAnalysis analysis)
	{
		switch (analysis.Category)
		{
		case LaunchFailureCategory.JavaVersionMismatch:
			return string.Format(Strings.Dialog_LaunchAnalysisJavaVersionDetailFormat, string.IsNullOrWhiteSpace(analysis.ModName) ? Strings.Dialog_LaunchAnalysisCurrentInstance : analysis.ModName, analysis.RequiredJavaMajorVersion?.ToString() ?? Strings.Dialog_LaunchStatusUnknownExitCode, analysis.CurrentJavaMajorVersion?.ToString() ?? Strings.Dialog_LaunchStatusUnknownExitCode);
		case LaunchFailureCategory.ModDependencyMissing:
			if (!string.IsNullOrWhiteSpace(analysis.DependencyName))
			{
				return string.Format(Strings.Dialog_LaunchAnalysisModDependencyDetailFormat, string.IsNullOrWhiteSpace(analysis.ModName) ? Strings.Dialog_LaunchAnalysisCurrentInstance : analysis.ModName, analysis.DependencyName);
			}
			return Strings.Dialog_LaunchAnalysisModDependencyDetail;
		case LaunchFailureCategory.ModVersionIncompatible:
			return Strings.Dialog_LaunchAnalysisModVersionDetail;
		case LaunchFailureCategory.MissingGameFiles:
			if (string.Equals(analysis.ReasonDetail, "missing_client_jar", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(analysis.MissingPath))
			{
				return string.Format(Strings.Dialog_LaunchAnalysisMissingClientJarDetailFormat, analysis.MissingPath);
			}
			if (!string.IsNullOrWhiteSpace(analysis.MissingPath))
			{
				return string.Format(Strings.Dialog_LaunchAnalysisMissingClasspathEntryDetailFormat, analysis.MissingPath);
			}
			return Strings.Dialog_LaunchAnalysisMissingFilesDetail;
		case LaunchFailureCategory.GameFileIntegrity:
			return GetGameFileIntegrityDetail(analysis);
		case LaunchFailureCategory.OutOfMemory:
			return Strings.Dialog_LaunchAnalysisOutOfMemoryDetail;
		default:
			return Strings.Dialog_LaunchAnalysisUnknownDetail;
		}
	}

	private static string GetAnalysisRecommendation(LaunchFailureAnalysis analysis)
	{
		return analysis.Category switch
		{
			LaunchFailureCategory.JavaVersionMismatch => string.Format(Strings.Dialog_LaunchAnalysisJavaVersionRecommendationFormat, analysis.RequiredJavaMajorVersion?.ToString() ?? Strings.Dialog_LaunchStatusUnknownExitCode), 
			LaunchFailureCategory.ModDependencyMissing => Strings.Dialog_LaunchAnalysisModDependencyRecommendation, 
			LaunchFailureCategory.ModVersionIncompatible => Strings.Dialog_LaunchAnalysisModVersionRecommendation, 
			LaunchFailureCategory.MissingGameFiles => Strings.Dialog_LaunchAnalysisMissingFilesRecommendation, 
			LaunchFailureCategory.GameFileIntegrity => GetGameFileIntegrityRecommendation(analysis), 
			LaunchFailureCategory.OutOfMemory => Strings.Dialog_LaunchAnalysisOutOfMemoryRecommendation, 
			_ => Strings.Dialog_LaunchAnalysisUnknownRecommendation, 
		};
	}

	private static string GetGameFileIntegrityDetail(LaunchFailureAnalysis analysis)
	{
		string affectedPath = analysis.AffectedPath;
		switch (analysis.GameFileFailureReason)
		{
		case GameFileRepairFailureReason.Missing:
			if (!string.IsNullOrWhiteSpace(affectedPath))
			{
				return string.Format(Strings.Dialog_LaunchAnalysisGameFileIntegrityMissingDetailFormat, affectedPath);
			}
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityMissingDetail;
		case GameFileRepairFailureReason.Corrupted:
			if (!string.IsNullOrWhiteSpace(affectedPath))
			{
				return string.Format(Strings.Dialog_LaunchAnalysisGameFileIntegrityCorruptedDetailFormat, affectedPath);
			}
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityCorruptedDetail;
		case GameFileRepairFailureReason.MetadataIncomplete:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityMetadataIncompleteDetail;
		case GameFileRepairFailureReason.DownloadFailed:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityDownloadFailedDetail;
		case GameFileRepairFailureReason.ProcessorRegenerationFailed:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityProcessorRegenerationFailedDetail;
		case GameFileRepairFailureReason.PublicationFailed:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityPublicationFailedDetail;
		case GameFileRepairFailureReason.FinalLaunchPlanInvalid:
			if (!string.IsNullOrWhiteSpace(affectedPath))
			{
				return string.Format(Strings.Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidDetailFormat, affectedPath);
			}
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidDetail;
		default:
			return Strings.Dialog_LaunchAnalysisUnknownDetail;
		}
	}

	private static string GetGameFileIntegrityRecommendation(LaunchFailureAnalysis analysis)
	{
		switch (analysis.GameFileFailureReason)
		{
		case GameFileRepairFailureReason.Missing:
		case GameFileRepairFailureReason.Corrupted:
			if ((!analysis.AutoRepairEnabled) ?? false)
			{
				return Strings.Dialog_LaunchAnalysisGameFileIntegrityEnableAutoRepairRecommendation;
			}
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityRepairFailedRecommendation;
		case GameFileRepairFailureReason.MetadataIncomplete:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityMetadataIncompleteRecommendation;
		case GameFileRepairFailureReason.DownloadFailed:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityDownloadFailedRecommendation;
		case GameFileRepairFailureReason.ProcessorRegenerationFailed:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityProcessorRegenerationFailedRecommendation;
		case GameFileRepairFailureReason.PublicationFailed:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityPublicationFailedRecommendation;
		case GameFileRepairFailureReason.FinalLaunchPlanInvalid:
			return Strings.Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidRecommendation;
		default:
			return Strings.Dialog_LaunchAnalysisUnknownRecommendation;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsOpenChanged(bool value)
	{
		if (!value)
		{
			exportSensitiveValues = Array.Empty<string>();
		}
		ViewReportCommand.NotifyCanExecuteChanged();
		ExportReportCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsExportingChanged(bool value)
	{
		ExportReportCommand.NotifyCanExecuteChanged();
	}
}

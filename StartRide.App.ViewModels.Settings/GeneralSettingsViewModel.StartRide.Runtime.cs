using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace StartRide.App.ViewModels.Settings;

public sealed partial class GeneralSettingsViewModel
{
	private bool isGameRunning;

	private string gameRuntimeStatusText = string.Empty;

	private string playtimeSummaryText = string.Empty;

	private string diagnosticsStatusText = string.Empty;

	private bool isDiagnosticsBusy;

	private bool endGameArmed;

	private bool runtimeInitialized;

	private RelayCommand? refreshGameRuntimeCommand;

	private RelayCommand? endGameCommand;

	private RelayCommand? openDiagnosticsFolderCommand;

	private AsyncRelayCommand? exportDiagnosticsCommand;

	public bool IsGameRunning
	{
		get => isGameRunning;
		private set
		{
			if (isGameRunning != value)
			{
				isGameRunning = value;
				OnPropertyChanged("IsGameRunning");
			}
		}
	}

	public string GameRuntimeStatusText
	{
		get => gameRuntimeStatusText;
		private set
		{
			if (gameRuntimeStatusText != value)
			{
				gameRuntimeStatusText = value;
				OnPropertyChanged("GameRuntimeStatusText");
			}
		}
	}

	public string PlaytimeSummaryText
	{
		get => playtimeSummaryText;
		private set
		{
			if (playtimeSummaryText != value)
			{
				playtimeSummaryText = value;
				OnPropertyChanged("PlaytimeSummaryText");
			}
		}
	}

	public string DiagnosticsStatusText
	{
		get => diagnosticsStatusText;
		private set
		{
			if (diagnosticsStatusText != value)
			{
				diagnosticsStatusText = value;
				OnPropertyChanged("DiagnosticsStatusText");
			}
		}
	}

	public bool IsDiagnosticsBusy
	{
		get => isDiagnosticsBusy;
		private set
		{
			if (isDiagnosticsBusy != value)
			{
				isDiagnosticsBusy = value;
				OnPropertyChanged("IsDiagnosticsBusy");
			}
		}
	}

	public string EndGameButtonText =>
		endGameArmed ? Strings.Settings_RuntimeEndGameConfirmButton : Strings.Settings_RuntimeEndGameButton;

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand RefreshGameRuntimeCommand =>
		refreshGameRuntimeCommand ?? (refreshGameRuntimeCommand = new RelayCommand(RefreshGameRuntimeState));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand EndGameCommand =>
		endGameCommand ?? (endGameCommand = new RelayCommand(EndGame));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand OpenDiagnosticsFolderCommand =>
		openDiagnosticsFolderCommand ?? (openDiagnosticsFolderCommand = new RelayCommand(OpenDiagnosticsFolder));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand ExportDiagnosticsCommand =>
		exportDiagnosticsCommand ?? (exportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync));

	private void InitializeRuntimeSection()
	{
		runtimeInitialized = true;
		RefreshGameRuntimeState();
	}

	public void RefreshGameRuntimeState()
	{
		try
		{
			var app = AppSettings.Current;
			bool running = GameRuntimeService.IsRunning();
			IsGameRunning = running;

			if (running)
			{
				DateTimeOffset? start = PlaytimeTracker.TryGetSessionStart(app) ?? GameRuntimeService.TryGetStartTime();
				var length = start.HasValue ? DateTimeOffset.Now - start.Value : (TimeSpan?)null;
				GameRuntimeStatusText = length.HasValue && length.Value >= TimeSpan.Zero
					? string.Format(Strings.Settings_RuntimeRunningFormat, PlaytimeTracker.FormatDuration(length.Value))
					: Strings.Settings_RuntimeNotRunning;
				endGameArmed = false;
			}
			else
			{
				GameRuntimeStatusText = Strings.Settings_RuntimeNotRunning;
			}
			OnPropertyChanged("EndGameButtonText");

			PlaytimeSummaryText = app.LaunchCount > 0
				? string.Format(
					Strings.Settings_RuntimePlaytimeFormat,
					PlaytimeTracker.FormatDuration(PlaytimeTracker.GetTotalPlaytime(app)),
					app.LaunchCount,
					PlaytimeTracker.FormatLastLaunchAt(app))
				: Strings.Settings_RuntimePlaytimeNone;

			string last = app.LastDiagnosticsBundlePath;
			DiagnosticsStatusText = (!string.IsNullOrWhiteSpace(last) && File.Exists(last))
				? string.Format(Strings.Settings_DiagnosticsLastExportFormat, Path.GetFileName(last))
				: Strings.Settings_DiagnosticsNotExportedYet;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Refresh game runtime state failed.");
		}
	}

	private void EndGame()
	{
		bool running;
		try
		{
			running = GameRuntimeService.IsRunning();
		}
		catch
		{
			running = false;
		}

		if (running && !endGameArmed)
		{
			endGameArmed = true;
			OnPropertyChanged("EndGameButtonText");
			statusService.Report(Strings.Settings_RuntimeEndGameWarning);
			return;
		}

		endGameArmed = false;
		OnPropertyChanged("EndGameButtonText");

		try
		{
			int ended = GameRuntimeService.EndGame();
			PlaytimeTracker.EndSession(AppSettings.Current);
			statusService.Report(ended > 0
				? string.Format(Strings.Settings_RuntimeEndedFormat, ended)
				: Strings.Settings_RuntimeNothingToEnd);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "End game process failed.");
			statusService.Report(string.Format(Strings.Settings_RuntimeEndFailedFormat, ex.Message));
		}
		finally
		{
			RefreshGameRuntimeState();
		}
	}

	private void OpenDiagnosticsFolder()
	{
		try
		{
			string dir = DiagnosticsBundleService.DiagnosticsDirectory;
			Directory.CreateDirectory(dir);
			instanceFolderService.TryOpen(dir);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open diagnostics folder failed.");
			statusService.Report(string.Format(Strings.Settings_DiagnosticsFailedFormat, ex.Message));
		}
	}

	private async Task ExportDiagnosticsAsync()
	{
		if (IsDiagnosticsBusy)
		{
			return;
		}

		IsDiagnosticsBusy = true;
		DiagnosticsStatusText = Strings.Settings_DiagnosticsExporting;
		try
		{
			var app = AppSettings.Current;
			string logDirectory = LauncherLogDirectory;
			string path = await Task.Run(() => DiagnosticsBundleService.Export(app, logDirectory)).ConfigureAwait(true);

			DiagnosticsStatusText = string.Format(
				Strings.Settings_DiagnosticsExportedFormat,
				FileSizeFormatter.Format(new FileInfo(path).Length));			statusService.Report(DiagnosticsStatusText);

			try
			{
				instanceFolderService.TryOpen(Path.GetDirectoryName(path) ?? DiagnosticsBundleService.DiagnosticsDirectory);
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Export diagnostics bundle failed.");
			DiagnosticsStatusText = string.Format(Strings.Settings_DiagnosticsFailedFormat, ex.Message);
			statusService.Report(DiagnosticsStatusText);
		}
		finally
		{
			IsDiagnosticsBusy = false;
		}
	}
}

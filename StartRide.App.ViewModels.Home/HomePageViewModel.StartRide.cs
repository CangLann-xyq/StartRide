using System;
using System.Threading;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.ViewModels.Home;

public sealed partial class HomePageViewModel
{
	private static int orphanRecoveryDone;

	private Timer? gameRuntimeTimer;

	private bool isGameRunning;

	private string gameRuntimeSummary = string.Empty;

	private string playtimeSummary = string.Empty;

	private bool showGameRuntimeLine;

	private bool endGameArmed;

	private RelayCommand? endGameCommand;

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

	public string GameRuntimeSummary
	{
		get => gameRuntimeSummary;
		private set
		{
			if (gameRuntimeSummary != value)
			{
				gameRuntimeSummary = value;
				OnPropertyChanged("GameRuntimeSummary");
			}
		}
	}

	public string PlaytimeSummary
	{
		get => playtimeSummary;
		private set
		{
			if (playtimeSummary != value)
			{
				playtimeSummary = value;
				OnPropertyChanged("PlaytimeSummary");
			}
		}
	}

	public bool ShowGameRuntimeLine
	{
		get => showGameRuntimeLine;
		private set
		{
			if (showGameRuntimeLine != value)
			{
				showGameRuntimeLine = value;
				OnPropertyChanged("ShowGameRuntimeLine");
			}
		}
	}

	public string EndGameButtonText => endGameArmed ? Strings.Home_EndGameConfirmButton : Strings.Home_EndGameButton;

	[System.CodeDom.Compiler.GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand EndGameCommand =>
		endGameCommand ?? (endGameCommand = new RelayCommand(EndGame));

	private void InitializeGameRuntime()
	{
		if (Interlocked.Exchange(ref orphanRecoveryDone, 1) == 0)
		{
			try
			{
				PlaytimeTracker.RecoverOrphanSession(AppSettings.Current);
			}
			catch
			{
			}
		}

		RefreshGameRuntimeState();
		try
		{
			gameRuntimeTimer = new Timer(OnGameRuntimeTick, null, 4000, 4000);
		}
		catch
		{
		}
	}

	private void OnGameRuntimeTick(object? state)
	{
		try
		{
			bool running = GameRuntimeService.IsRunning();
			if (!running && !isGameRunning && !endGameArmed)
			{
				return;
			}
			if (uiDispatcher.HasAccess)
			{
				RefreshGameRuntimeState();
			}
			else
			{
				uiDispatcher.Post(RefreshGameRuntimeState);
			}
		}
		catch
		{
		}
	}

	public void RefreshGameRuntimeState()
	{
		try
		{
			var app = AppSettings.Current;
			bool processRunning = GameRuntimeService.IsRunning();
			DateTimeOffset? start = PlaytimeTracker.TryGetSessionStart(app) ?? GameRuntimeService.TryGetStartTime();

			if (processRunning)
			{
				IsGameRunning = true;
				var length = start.HasValue ? DateTimeOffset.Now - start.Value : (TimeSpan?)null;
				GameRuntimeSummary = length.HasValue && length.Value >= TimeSpan.Zero
					? string.Format(Strings.Home_GameRunningFormat, PlaytimeTracker.FormatDuration(length.Value))
					: Strings.Home_GameRunningOnly;
			}
			else
			{
				IsGameRunning = false;
				GameRuntimeSummary = string.Empty;
				if (endGameArmed)
				{
					endGameArmed = false;
					OnPropertyChanged("EndGameButtonText");
				}
			}

			PlaytimeSummary = app.LaunchCount > 0
				? string.Format(
					Strings.Home_PlaytimeSummaryFormat,
					PlaytimeTracker.FormatDuration(PlaytimeTracker.GetTotalPlaytime(app)),
					app.LaunchCount)
				: string.Empty;

			ShowGameRuntimeLine = IsGameRunning || PlaytimeSummary.Length > 0;

			UpdateTrayTooltip();
		}
		catch
		{
		}
	}

	private void UpdateTrayTooltip()
	{
		try
		{
			if (System.Windows.Application.Current?.MainWindow is StartRide.App.Views.Shell.MainWindow window)
			{
				window.StartRideTrayUpdateTooltip(IsGameRunning
					? Strings.Home_TrayTooltipRunning
					: Strings.Home_TrayTooltipIdle);
			}
		}
		catch
		{
		}
	}

	private void EndGame()
	{
		bool running = false;
		try
		{
			running = GameRuntimeService.IsRunning();
		}
		catch
		{
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
			statusService.Report(string.Format(Strings.Settings_RuntimeEndFailedFormat, ex.Message));
		}
		finally
		{
			RefreshGameRuntimeState();
		}
	}
}

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

public sealed class MainWindowPlacementService(ISettingsService settingsService)
{
	internal void Restore(Window window, LauncherSettings settings)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		ArgumentNullException.ThrowIfNull(window, "window");
		ArgumentNullException.ThrowIfNull(settings, "settings");
		Restore(window, settings, SystemParameters.WorkArea);
	}

	internal static void Restore(Window window, LauncherSettings settings, Rect workArea)
	{
		window.Width = NormalizeRestoredDimension(settings.MainWindowWidth, window.Width, window.MinWidth, workArea.Width);
		window.Height = NormalizeRestoredDimension(settings.MainWindowHeight, window.Height, window.MinHeight, workArea.Height);
		window.WindowState = (settings.MainWindowWasMaximized ? WindowState.Maximized : WindowState.Normal);
	}

	internal MainWindowPlacementSnapshot Capture(Window window)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		ArgumentNullException.ThrowIfNull(window, "window");
		return CreateSnapshot(window.WindowState, new Size(window.ActualWidth, window.ActualHeight), window.RestoreBounds, new Size(window.Width, window.Height));
	}

	internal async Task SaveAsync(MainWindowPlacementSnapshot snapshot, CancellationToken cancellationToken = default(CancellationToken))
	{
		await settingsService.UpdateAsync(delegate(LauncherSettings settings)
		{
			settings.MainWindowWidth = snapshot.Width;
			settings.MainWindowHeight = snapshot.Height;
			settings.MainWindowWasMaximized = snapshot.WasMaximized;
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal static MainWindowPlacementSnapshot CreateSnapshot(WindowState windowState, Size actualSize, Rect restoreBounds, Size fallbackSize)
	{
		double value = ((windowState == WindowState.Normal) ? actualSize.Width : restoreBounds.Width);
		return new MainWindowPlacementSnapshot(Height: NormalizeCapturedDimension((windowState == WindowState.Normal) ? actualSize.Height : restoreBounds.Height, fallbackSize.Height, 700.0), Width: NormalizeCapturedDimension(value, fallbackSize.Width, 1000.0), WasMaximized: windowState == WindowState.Maximized);
	}

	private static double NormalizeRestoredDimension(double value, double fallbackValue, double minimumValue, double availableValue)
	{
		double num = (IsValidDimension(minimumValue) ? minimumValue : 0.0);
		double num2 = (IsValidDimension(fallbackValue) ? Math.Max(fallbackValue, num) : num);
		double value2 = (IsValidDimension(value) ? value : num2);
		double max = (IsValidDimension(availableValue) ? Math.Max(availableValue, num) : double.PositiveInfinity);
		return Math.Clamp(value2, num, max);
	}

	private static double NormalizeCapturedDimension(double value, double fallbackValue, double defaultValue)
	{
		if (IsValidDimension(value))
		{
			return value;
		}
		if (!IsValidDimension(fallbackValue))
		{
			return defaultValue;
		}
		return fallbackValue;
	}

	private static bool IsValidDimension(double value)
	{
		if (double.IsFinite(value))
		{
			return value > 0.0;
		}
		return false;
	}
}

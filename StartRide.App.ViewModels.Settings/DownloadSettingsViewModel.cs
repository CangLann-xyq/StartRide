using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Settings;

public sealed class DownloadSettingsViewModel : SettingsSectionViewModelBase
{
	[ObservableProperty]
	private SettingsDownloadSourceOption? selectedDownloadSourceOption;

	[ObservableProperty]
	private int maximumDownloadConcurrency = 8;

	[ObservableProperty]
	private string downloadSpeedLimitMbPerSecondText = string.Empty;

	public ObservableCollection<SettingsDownloadSourceOption> DownloadSourceOptions { get; }

	public CustomFileDownloadViewModel CustomFileDownload { get; }

	public int MinimumDownloadConcurrency => 1;

	public int MaximumAllowedDownloadConcurrency => 16;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsDownloadSourceOption? SelectedDownloadSourceOption
	{
		get
		{
			return selectedDownloadSourceOption;
		}
		set
		{
			if (!EqualityComparer<SettingsDownloadSourceOption>.Default.Equals(selectedDownloadSourceOption, value))
			{
				SettingsDownloadSourceOption oldValue = selectedDownloadSourceOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedDownloadSourceOption);
				selectedDownloadSourceOption = value;
				OnSelectedDownloadSourceOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedDownloadSourceOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int MaximumDownloadConcurrency
	{
		get
		{
			return maximumDownloadConcurrency;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(maximumDownloadConcurrency, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MaximumDownloadConcurrency);
				maximumDownloadConcurrency = value;
				OnMaximumDownloadConcurrencyChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MaximumDownloadConcurrency);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DownloadSpeedLimitMbPerSecondText
	{
		get
		{
			return downloadSpeedLimitMbPerSecondText;
		}
		[MemberNotNull("downloadSpeedLimitMbPerSecondText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(downloadSpeedLimitMbPerSecondText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DownloadSpeedLimitMbPerSecondText);
				downloadSpeedLimitMbPerSecondText = value;
				OnDownloadSpeedLimitMbPerSecondTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DownloadSpeedLimitMbPerSecondText);
			}
		}
	}

	public event EventHandler<SettingsDownloadSourceChangedEventArgs>? DownloadSourceChanged;

	public event EventHandler<SettingsMaximumDownloadConcurrencyChangedEventArgs>? MaximumDownloadConcurrencyChanged;

	public event EventHandler<SettingsDownloadSpeedLimitChangedEventArgs>? DownloadSpeedLimitChanged;

	internal DownloadSettingsViewModel(SettingsPersistenceCoordinator persistence, CustomFileDownloadViewModel customFileDownload)
		: base(persistence)
	{
		CustomFileDownload = customFileDownload;

		DownloadSourceOptions = new ObservableCollection<SettingsDownloadSourceOption>
		{
			new SettingsDownloadSourceOption(DownloadSourcePreference.Official, Strings.Settings_DownloadSourceOfficial)
		};
		selectedDownloadSourceOption = DownloadSourceOptions[0];
	}

	public void Load(LauncherSettings settings)
	{
		LoadState(delegate
		{
			SelectedDownloadSourceOption = DownloadSourceOptions.FirstOrDefault((SettingsDownloadSourceOption option) => option.Preference == settings.DownloadSourcePreference) ?? DownloadSourceOptions[0];

			int threads = StartRide.Core.AppSettings.Current.DownloadThreads;
			MaximumDownloadConcurrency = Math.Clamp(threads, MinimumDownloadConcurrency, MaximumAllowedDownloadConcurrency);

			int kbps = StartRide.Core.AppSettings.Current.DownloadSpeedLimitKbps;
			DownloadSpeedLimitMbPerSecondText = FormatDownloadSpeedLimit(kbps > 0 ? kbps / 1024 : 0);

			settings.MaximumDownloadConcurrency = MaximumDownloadConcurrency;
		});
	}

	private static int NormalizeDownloadSpeedLimit(string? value)
	{
		if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return 0;
		}
		return Math.Max(result, 0);
	}

	private static string FormatDownloadSpeedLimit(int value)
	{
		if (value <= 0)
		{
			return string.Empty;
		}
		return value.ToString(CultureInfo.InvariantCulture);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedDownloadSourceOptionChanged(SettingsDownloadSourceOption? oldValue, SettingsDownloadSourceOption? newValue)
	{
		if (newValue == null)
		{
			LoadState(delegate
			{
				SelectedDownloadSourceOption = oldValue ?? DownloadSourceOptions[0];
			});
		}
		else if (base.CanPersist)
		{
			DownloadSourcePreference preference = newValue.Preference;
			Persist(delegate(LauncherSettings settings)
			{
				settings.DownloadSourcePreference = preference;
			});
			DownloadSourceChanged?.Invoke(this, new SettingsDownloadSourceChangedEventArgs(preference));
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnMaximumDownloadConcurrencyChanged(int value)
	{
		int normalized = Math.Clamp(value, MinimumDownloadConcurrency, MaximumAllowedDownloadConcurrency);
		if (value != normalized)
		{
			MaximumDownloadConcurrency = normalized;
		}
		else if (base.CanPersist)
		{
			Persist(delegate(LauncherSettings settings)
			{
				settings.MaximumDownloadConcurrency = normalized;
			});

			try
			{
				var app = StartRide.Core.AppSettings.Current;
				if (app.DownloadThreads != normalized)
				{
					app.DownloadThreads = normalized;
					app.Save();
				}
			}
			catch { }

			MaximumDownloadConcurrencyChanged?.Invoke(this, new SettingsMaximumDownloadConcurrencyChangedEventArgs(normalized));
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDownloadSpeedLimitMbPerSecondTextChanged(string value)
	{
		if (base.CanPersist)
		{
			int limit = NormalizeDownloadSpeedLimit(value);
			Persist(delegate(LauncherSettings settings)
			{
				settings.DownloadSpeedLimitMbPerSecond = limit;
			});

			try
			{
				var app = StartRide.Core.AppSettings.Current;
				int kbps = limit * 1024;
				if (app.DownloadSpeedLimitKbps != kbps)
				{
					app.DownloadSpeedLimitKbps = kbps;
					app.Save();
				}
			}
			catch { }

			DownloadSpeedLimitChanged?.Invoke(this, new SettingsDownloadSpeedLimitChangedEventArgs(limit));
		}
	}
}

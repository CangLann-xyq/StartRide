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

	/// <summary>
	/// 上限 16。以前是 128，但真正下载的 StartRide.DownloadManager 最多只认 16 个并发，
	/// 滑到 100 也不会有任何变化 —— 界面范围和引擎能力必须一致，否则又是"调了没反应"。
	/// </summary>
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
		// StartRide：下载源只有 BeamNG 官方源。BMCLAPI 是 Minecraft 专用镜像，
		// 对 BeamNG 没有意义，不再出现在选项里。
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

			// 以 StartRide 自己的配置为准：真正下载的是 StartRide.DownloadManager，
			// 它只读 AppSettings.DownloadThreads / DownloadSpeedLimitKbps。
			// 两边曾经各存一套、互不相通，所以这里统一到 AppSettings 上。
			int threads = StartRide.Core.AppSettings.Current.DownloadThreads;
			MaximumDownloadConcurrency = Math.Clamp(threads, MinimumDownloadConcurrency, MaximumAllowedDownloadConcurrency);

			int kbps = StartRide.Core.AppSettings.Current.DownloadSpeedLimitKbps;
			DownloadSpeedLimitMbPerSecondText = FormatDownloadSpeedLimit(kbps > 0 ? kbps / 1024 : 0);

			// 顺手把 LauncherSettings 侧也写一致，避免下次从另一个入口读出来还是旧值
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

			// 关键：写进真正被下载引擎读取的那份配置，滑杆才真的改变并发行为
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

			// 下载引擎读的是 Kbps，这里做单位换算后落库
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

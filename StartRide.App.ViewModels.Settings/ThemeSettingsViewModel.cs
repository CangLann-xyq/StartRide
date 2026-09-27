using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Shell;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Settings;

public sealed class ThemeSettingsViewModel : SettingsSectionViewModelBase
{
	private readonly IThemeService themeService;

	private readonly LauncherBackgroundViewModel? launcherBackground;

	[ObservableProperty]
	private SettingsThemeOption? selectedThemeOption;

	[ObservableProperty]
	private SettingsAccentColorOption? selectedAccentColorOption;

	[ObservableProperty]
	private SettingsBackgroundEffectOption? selectedBackgroundEffectOption;

	[ObservableProperty]
	private bool followSystemTheme = true;

	[ObservableProperty]
	private int launcherBackgroundOpacityPercent = 85;

	[ObservableProperty]
	private bool enableImageBackgroundControlBlur = true;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openLauncherBackgroundImageFolderCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? refreshLauncherBackgroundImageCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? clearLauncherBackgroundImagesCommand;

	public ObservableCollection<SettingsThemeOption> ThemeOptions { get; }

	public ObservableCollection<SettingsAccentColorOption> AccentColorOptions { get; }

	public ObservableCollection<SettingsBackgroundEffectOption> BackgroundEffectOptions { get; }

	public bool IsThemeSelectionVisible => !FollowSystemTheme;

	public bool IsBackgroundImageSelectionVisible => SelectedBackgroundEffectOption?.IsImageSelected ?? false;

	public bool IsBackgroundOpacityVisible => SelectedBackgroundEffectOption?.IsAcrylicEnabled ?? true;

	public string LauncherBackgroundOpacityText => $"{LauncherBackgroundOpacityPercent}%";

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsThemeOption? SelectedThemeOption
	{
		get
		{
			return selectedThemeOption;
		}
		set
		{
			if (!EqualityComparer<SettingsThemeOption>.Default.Equals(selectedThemeOption, value))
			{
				SettingsThemeOption oldValue = selectedThemeOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedThemeOption);
				selectedThemeOption = value;
				OnSelectedThemeOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedThemeOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsAccentColorOption? SelectedAccentColorOption
	{
		get
		{
			return selectedAccentColorOption;
		}
		set
		{
			if (!EqualityComparer<SettingsAccentColorOption>.Default.Equals(selectedAccentColorOption, value))
			{
				SettingsAccentColorOption oldValue = selectedAccentColorOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedAccentColorOption);
				selectedAccentColorOption = value;
				OnSelectedAccentColorOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedAccentColorOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsBackgroundEffectOption? SelectedBackgroundEffectOption
	{
		get
		{
			return selectedBackgroundEffectOption;
		}
		set
		{
			if (!EqualityComparer<SettingsBackgroundEffectOption>.Default.Equals(selectedBackgroundEffectOption, value))
			{
				SettingsBackgroundEffectOption oldValue = selectedBackgroundEffectOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedBackgroundEffectOption);
				selectedBackgroundEffectOption = value;
				OnSelectedBackgroundEffectOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedBackgroundEffectOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool FollowSystemTheme
	{
		get
		{
			return followSystemTheme;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(followSystemTheme, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FollowSystemTheme);
				followSystemTheme = value;
				OnFollowSystemThemeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FollowSystemTheme);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int LauncherBackgroundOpacityPercent
	{
		get
		{
			return launcherBackgroundOpacityPercent;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(launcherBackgroundOpacityPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LauncherBackgroundOpacityPercent);
				launcherBackgroundOpacityPercent = value;
				OnLauncherBackgroundOpacityPercentChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LauncherBackgroundOpacityPercent);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool EnableImageBackgroundControlBlur
	{
		get
		{
			return enableImageBackgroundControlBlur;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(enableImageBackgroundControlBlur, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableImageBackgroundControlBlur);
				enableImageBackgroundControlBlur = value;
				OnEnableImageBackgroundControlBlurChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableImageBackgroundControlBlur);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenLauncherBackgroundImageFolderCommand => openLauncherBackgroundImageFolderCommand ?? (openLauncherBackgroundImageFolderCommand = new RelayCommand(OpenLauncherBackgroundImageFolder));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RefreshLauncherBackgroundImageCommand => refreshLauncherBackgroundImageCommand ?? (refreshLauncherBackgroundImageCommand = new RelayCommand(RefreshLauncherBackgroundImage));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ClearLauncherBackgroundImagesCommand => clearLauncherBackgroundImagesCommand ?? (clearLauncherBackgroundImagesCommand = new RelayCommand(ClearLauncherBackgroundImages));

	internal ThemeSettingsViewModel(SettingsPersistenceCoordinator persistence, IThemeService themeService, LauncherBackgroundViewModel? launcherBackground)
		: base(persistence)
	{
		this.themeService = themeService;
		this.launcherBackground = launcherBackground;
		ThemeOptions = new ObservableCollection<SettingsThemeOption>
		{
			new SettingsThemeOption("Dark", Strings.Settings_ThemeDarkTitle),
			new SettingsThemeOption("Light", Strings.Settings_ThemeLightTitle)
		};
		AccentColorOptions = new ObservableCollection<SettingsAccentColorOption>
		{
			new SettingsAccentColorOption("Blue", Strings.Settings_AccentColorBlueTitle),
			new SettingsAccentColorOption("Cyan", Strings.Settings_AccentColorCyanTitle),
			new SettingsAccentColorOption("Green", Strings.Settings_AccentColorGreenTitle),
			new SettingsAccentColorOption("Emerald", Strings.Settings_AccentColorEmeraldTitle),
			new SettingsAccentColorOption("Purple", Strings.Settings_AccentColorPurpleTitle),
			new SettingsAccentColorOption("Pink", Strings.Settings_AccentColorPinkTitle),
			new SettingsAccentColorOption("Orange", Strings.Settings_AccentColorOrangeTitle),
			new SettingsAccentColorOption("Amber", Strings.Settings_AccentColorAmberTitle)
		};
		BackgroundEffectOptions = new ObservableCollection<SettingsBackgroundEffectOption>
		{
			new SettingsBackgroundEffectOption("None", Strings.Settings_BackgroundEffectNoneTitle),
			new SettingsBackgroundEffectOption("Acrylic", Strings.Settings_BackgroundEffectAcrylicTitle),
			new SettingsBackgroundEffectOption("Image", Strings.Settings_BackgroundEffectImageTitle)
		};
		selectedThemeOption = ThemeOptions[0];
		selectedAccentColorOption = AccentColorOptions[0];
		selectedBackgroundEffectOption = BackgroundEffectOptions[1];
	}

	public void Load(LauncherSettings settings)
	{
		LoadState(delegate
		{
			FollowSystemTheme = settings.ThemeFollowSystem;
			SelectedThemeOption = ThemeOptions.FirstOrDefault((SettingsThemeOption option) => string.Equals(option.Id, settings.Theme, StringComparison.OrdinalIgnoreCase)) ?? ThemeOptions[0];
			SelectedAccentColorOption = AccentColorOptions.FirstOrDefault((SettingsAccentColorOption option) => string.Equals(option.Id, settings.AccentColor, StringComparison.OrdinalIgnoreCase)) ?? AccentColorOptions[0];
			string backgroundEffect = LauncherBackgroundEffects.Normalize(settings.LauncherBackgroundEffect);
			SelectedBackgroundEffectOption = BackgroundEffectOptions.First((SettingsBackgroundEffectOption option) => string.Equals(option.Id, backgroundEffect, StringComparison.Ordinal));
			LauncherBackgroundOpacityPercent = Math.Clamp(settings.LauncherBackgroundOpacityPercent, 0, 100);
			EnableImageBackgroundControlBlur = settings.EnableImageBackgroundControlBlur;
		});
	}

	[RelayCommand]
	private void OpenLauncherBackgroundImageFolder()
	{
		launcherBackground?.TryOpenDirectory();
	}

	[RelayCommand]
	private void RefreshLauncherBackgroundImage()
	{
		launcherBackground?.Refresh();
	}

	[RelayCommand]
	private void ClearLauncherBackgroundImages()
	{
		launcherBackground?.ClearImages();
	}

	private void ApplyPreferenceAndPersist()
	{
		if (base.CanPersist && SelectedThemeOption != null)
		{
			string theme = SelectedThemeOption.Id;
			themeService.ApplyPreference(theme, FollowSystemTheme, LauncherBackgroundOpacityPercent);
			Persist(delegate(LauncherSettings settings)
			{
				settings.Theme = theme;
				settings.ThemeFollowSystem = FollowSystemTheme;
			});
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedThemeOptionChanged(SettingsThemeOption? oldValue, SettingsThemeOption? newValue)
	{
		if (newValue == null)
		{
			LoadState(delegate
			{
				SelectedThemeOption = oldValue ?? ThemeOptions[0];
			});
		}
		else
		{
			ApplyPreferenceAndPersist();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedAccentColorOptionChanged(SettingsAccentColorOption? oldValue, SettingsAccentColorOption? newValue)
	{
		if (newValue == null)
		{
			LoadState(delegate
			{
				SelectedAccentColorOption = oldValue ?? AccentColorOptions[0];
			});
		}
		else if (base.CanPersist)
		{
			string accent = newValue.Id;
			themeService.ApplyAccent(accent);
			Persist(delegate(LauncherSettings settings)
			{
				settings.AccentColor = accent;
			});
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedBackgroundEffectOptionChanged(SettingsBackgroundEffectOption? oldValue, SettingsBackgroundEffectOption? newValue)
	{
		if (newValue == null)
		{
			LoadState(delegate
			{
				SelectedBackgroundEffectOption = oldValue ?? BackgroundEffectOptions[1];
			});
			return;
		}
		OnPropertyChanged("IsBackgroundImageSelectionVisible");
		OnPropertyChanged("IsBackgroundOpacityVisible");
		if (base.CanPersist)
		{
			themeService.ApplyBackgroundEffect(newValue.Id, EnableImageBackgroundControlBlur);
			launcherBackground?.ApplyEffect(newValue.Id, newValue.IsImageSelected);
			Persist(delegate(LauncherSettings settings)
			{
				settings.LauncherBackgroundEffect = newValue.Id;
			});
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnFollowSystemThemeChanged(bool value)
	{
		OnPropertyChanged("IsThemeSelectionVisible");
		ApplyPreferenceAndPersist();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLauncherBackgroundOpacityPercentChanged(int value)
	{
		int normalized = Math.Clamp(value, 0, 100);
		if (value != normalized)
		{
			LauncherBackgroundOpacityPercent = normalized;
			return;
		}
		OnPropertyChanged("LauncherBackgroundOpacityText");
		if (base.CanPersist)
		{
			themeService.ApplyBackgroundOpacity(normalized);
			Persist(delegate(LauncherSettings settings)
			{
				settings.LauncherBackgroundOpacityPercent = normalized;
			});
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnEnableImageBackgroundControlBlurChanged(bool value)
	{
		if (base.CanPersist)
		{
			string backgroundEffect = SelectedBackgroundEffectOption?.Id ?? "Acrylic";
			themeService.ApplyBackgroundEffect(backgroundEffect, value);
			Persist(delegate(LauncherSettings settings)
			{
				settings.EnableImageBackgroundControlBlur = value;
			});
		}
	}
}

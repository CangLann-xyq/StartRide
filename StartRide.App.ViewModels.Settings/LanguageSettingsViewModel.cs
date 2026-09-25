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

public sealed class LanguageSettingsViewModel : SettingsSectionViewModelBase
{
	[ObservableProperty]
	private SettingsLanguageOption? selectedLanguageOption;

	[ObservableProperty]
	private bool autoSetGameLanguageToLauncherLanguage;

	public ObservableCollection<SettingsLanguageOption> LanguageOptions { get; }

	public string SelectedLanguageId => LauncherLanguages.Normalize(SelectedLanguageOption?.Id);

	public bool IsLanguageRestartNoticeVisible => !string.Equals(SelectedLanguageId, CurrentLanguageId, StringComparison.OrdinalIgnoreCase);

	public string LanguageRestartNoticeText
	{
		get
		{
			if (!IsLanguageRestartNoticeVisible)
			{
				return string.Empty;
			}
			return BuildLanguageRestartNoticeText(CurrentLanguageId, SelectedLanguageId);
		}
	}

	private static string CurrentLanguageId => LauncherLanguages.Normalize(CultureInfo.CurrentUICulture.Name);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsLanguageOption? SelectedLanguageOption
	{
		get
		{
			return selectedLanguageOption;
		}
		set
		{
			if (!EqualityComparer<SettingsLanguageOption>.Default.Equals(selectedLanguageOption, value))
			{
				SettingsLanguageOption oldValue = selectedLanguageOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedLanguageOption);
				selectedLanguageOption = value;
				OnSelectedLanguageOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedLanguageOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool AutoSetGameLanguageToLauncherLanguage
	{
		get
		{
			return autoSetGameLanguageToLauncherLanguage;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(autoSetGameLanguageToLauncherLanguage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AutoSetGameLanguageToLauncherLanguage);
				autoSetGameLanguageToLauncherLanguage = value;
				OnAutoSetGameLanguageToLauncherLanguageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AutoSetGameLanguageToLauncherLanguage);
			}
		}
	}

	internal LanguageSettingsViewModel(SettingsPersistenceCoordinator persistence)
		: base(persistence)
	{
		LanguageOptions = new ObservableCollection<SettingsLanguageOption>
		{
			new SettingsLanguageOption("zh-Hans", Strings.Settings_LanguageSimplifiedChinese),
			new SettingsLanguageOption("zh-Hant", Strings.Settings_LanguageTraditionalChinese),
			new SettingsLanguageOption("ja-JP", Strings.Settings_LanguageJapanese),
			new SettingsLanguageOption("en", Strings.Settings_LanguageEnglish)
		};
		selectedLanguageOption = LanguageOptions[0];
	}

	public void Load(LauncherSettings settings)
	{
		LoadState(delegate
		{
			string normalized = LauncherLanguages.Normalize(settings.LauncherLanguage);
			SelectedLanguageOption = LanguageOptions.FirstOrDefault((SettingsLanguageOption option) => string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase)) ?? LanguageOptions[0];
			AutoSetGameLanguageToLauncherLanguage = settings.AutoSetGameLanguageToLauncherLanguage;
		});
	}

	private void PersistLanguage()
	{
		if ((object)SelectedLanguageOption != null)
		{
			Persist(delegate(LauncherSettings settings)
			{
				settings.LauncherLanguage = SelectedLanguageId;
				settings.AutoSetGameLanguageToLauncherLanguage = AutoSetGameLanguageToLauncherLanguage;
			});
		}
	}

	private static string BuildLanguageRestartNoticeText(string currentLanguageId, string targetLanguageId)
	{
		string languageRestartNotice = Strings.GetLanguageRestartNotice(CultureInfo.GetCultureInfo(currentLanguageId));
		string languageRestartNotice2 = Strings.GetLanguageRestartNotice(CultureInfo.GetCultureInfo(targetLanguageId));
		if (!string.Equals(languageRestartNotice, languageRestartNotice2, StringComparison.Ordinal))
		{
			return languageRestartNotice + Environment.NewLine + languageRestartNotice2;
		}
		return languageRestartNotice;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedLanguageOptionChanged(SettingsLanguageOption? oldValue, SettingsLanguageOption? newValue)
	{
		if ((object)newValue == null)
		{
			LoadState(delegate
			{
				SelectedLanguageOption = oldValue ?? LanguageOptions[0];
			});
			return;
		}
		OnPropertyChanged("SelectedLanguageId");
		OnPropertyChanged("IsLanguageRestartNoticeVisible");
		OnPropertyChanged("LanguageRestartNoticeText");
		PersistLanguage();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAutoSetGameLanguageToLauncherLanguageChanged(bool value)
	{
		PersistLanguage();
	}
}

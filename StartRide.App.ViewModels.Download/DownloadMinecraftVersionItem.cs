using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Utilities;
using StartRide.App.ViewModels.Shared;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Download;

public sealed class DownloadMinecraftVersionItem : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public MinecraftVersionInfo Version { get; }

	public string Name => Version.Name;

	public string Type => Version.Type;

	public string VersionType => MinecraftVersionIconResolver.NormalizeVersionType(Type);

	public string TypeLabel => MinecraftVersionTypeDisplayProvider.GetLabel(VersionType, Version.Type);

	public string ReleaseDateText
	{
		get
		{
			DateTimeOffset? releaseTime = Version.ReleaseTime;
			if (!releaseTime.HasValue)
			{
				return string.Empty;
			}
			return releaseTime.GetValueOrDefault().ToLocalTime().ToString("yyyy-MM-dd");
		}
	}

	public bool IsRelease => VersionType.Equals("release", StringComparison.OrdinalIgnoreCase);

	public bool IsSnapshot => VersionType.Equals("snapshot", StringComparison.OrdinalIgnoreCase);

	public bool IsAprilFools => MinecraftAprilFoolsVersionClassifier.IsAprilFoolsVersion(Name);

	public bool IsBeta => VersionType.Equals("old_beta", StringComparison.OrdinalIgnoreCase);

	public bool IsAlpha => VersionType.Equals("old_alpha", StringComparison.OrdinalIgnoreCase);

	public string IconSource => MinecraftVersionIconResolver.Resolve(VersionType, Name);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isSelected, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSelected);
				isSelected = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSelected);
			}
		}
	}

	public DownloadMinecraftVersionItem(MinecraftVersionInfo version)
	{
		Version = version;
	}
}

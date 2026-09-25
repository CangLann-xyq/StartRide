using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceBackupItemViewModel : ObservableObject
{
	[ObservableProperty]
	private string title = string.Empty;

	[ObservableProperty]
	private string fileName = string.Empty;

	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private long sizeBytes;

	[ObservableProperty]
	private DateTimeOffset createdAt;

	[ObservableProperty]
	private bool isSelected;

	public string TrailingText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

	public string Subtitle => string.Format(Strings.GameSettings_BackupItemSubtitleFormat, FileName, (double)SizeBytes / 1024.0 / 1024.0);

	public string IconKey => "instance_setting_page/backup";

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
	public string FileName
	{
		get
		{
			return fileName;
		}
		[MemberNotNull("fileName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(fileName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FileName);
				fileName = value;
				OnFileNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FileName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string FullPath
	{
		get
		{
			return fullPath;
		}
		[MemberNotNull("fullPath")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(fullPath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FullPath);
				fullPath = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FullPath);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public long SizeBytes
	{
		get
		{
			return sizeBytes;
		}
		set
		{
			if (!EqualityComparer<long>.Default.Equals(sizeBytes, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SizeBytes);
				sizeBytes = value;
				OnSizeBytesChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SizeBytes);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DateTimeOffset CreatedAt
	{
		get
		{
			return createdAt;
		}
		set
		{
			if (!EqualityComparer<DateTimeOffset>.Default.Equals(createdAt, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CreatedAt);
				createdAt = value;
				OnCreatedAtChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CreatedAt);
			}
		}
	}

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

	public InstanceBackupItemViewModel(InstanceBackupRecord backup)
	{
		Title = backup.Name;
		FileName = backup.FileName;
		FullPath = backup.FullPath;
		SizeBytes = backup.SizeBytes;
		CreatedAt = backup.CreatedAt;
	}

	public bool Matches(string query)
	{
		if (!Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
		{
			return FileName.Contains(query, StringComparison.CurrentCultureIgnoreCase);
		}
		return true;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnFileNameChanged(string value)
	{
		OnPropertyChanged("Subtitle");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSizeBytesChanged(long value)
	{
		OnPropertyChanged("Subtitle");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCreatedAtChanged(DateTimeOffset value)
	{
		OnPropertyChanged("TrailingText");
	}
}

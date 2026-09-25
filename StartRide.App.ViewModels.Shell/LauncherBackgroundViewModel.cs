using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.Shell;

public sealed class LauncherBackgroundViewModel : ObservableObject
{
	private readonly ILauncherBackgroundImageCatalog catalog;

	private readonly ILauncherBackgroundImageLoader imageLoader;

	private readonly IInstanceFolderService folderService;

	private readonly IStatusService statusService;

	private readonly ILogger<LauncherBackgroundViewModel> logger;

	private readonly Func<int, int> nextRandomIndex;

	private string? currentImagePath;

	[ObservableProperty]
	private ImageSource? imageSource;

	[ObservableProperty]
	private bool isActive;

	internal string? CurrentImagePath => currentImagePath;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ImageSource? ImageSource
	{
		get
		{
			return imageSource;
		}
		set
		{
			if (!EqualityComparer<System.Windows.Media.ImageSource>.Default.Equals(imageSource, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ImageSource);
				imageSource = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ImageSource);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsActive
	{
		get
		{
			return isActive;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isActive, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsActive);
				isActive = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsActive);
			}
		}
	}

	public LauncherBackgroundViewModel(ILauncherBackgroundImageCatalog catalog, ILauncherBackgroundImageLoader imageLoader, IInstanceFolderService folderService, IStatusService statusService, ILogger<LauncherBackgroundViewModel>? logger = null)
		: this(catalog, imageLoader, folderService, statusService, logger, Random.Shared.Next)
	{
	}

	internal LauncherBackgroundViewModel(ILauncherBackgroundImageCatalog catalog, ILauncherBackgroundImageLoader imageLoader, IInstanceFolderService folderService, IStatusService statusService, ILogger<LauncherBackgroundViewModel>? logger, Func<int, int> nextRandomIndex)
	{
		this.catalog = catalog ?? throw new ArgumentNullException("catalog");
		this.imageLoader = imageLoader ?? throw new ArgumentNullException("imageLoader");
		this.folderService = folderService ?? throw new ArgumentNullException("folderService");
		this.statusService = statusService ?? throw new ArgumentNullException("statusService");
		this.logger = logger ?? NullLogger<LauncherBackgroundViewModel>.Instance;
		this.nextRandomIndex = nextRandomIndex ?? throw new ArgumentNullException("nextRandomIndex");
	}

	public void ApplyEffect(string? backgroundEffect, bool reportFailure)
	{
		if (!LauncherBackgroundEffects.IsImage(backgroundEffect))
		{
			Deactivate();
		}
		else
		{
			Refresh(avoidCurrentImage: false, reportFailure);
		}
	}

	public bool Refresh(bool avoidCurrentImage = true, bool reportFailure = true)
	{
		IReadOnlyList<string> candidates;
		try
		{
			candidates = catalog.GetCandidatePaths();
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			logger.LogWarning(ex, "Failed to enumerate launcher background images. Stage=Enumerate");
			Deactivate();
			if (reportFailure)
			{
				statusService.Report(Strings.Status_ReadLauncherBackgroundImagesFailed);
			}
			return false;
		}
		logger.LogDebug("Launcher background image candidates enumerated. CandidateCount={CandidateCount}", candidates.Count);
		if (candidates.Count == 0)
		{
			Deactivate();
			if (reportFailure)
			{
				statusService.Report(Strings.Status_NoLauncherBackgroundImages);
			}
			return false;
		}
		string previousPath = currentImagePath;
		bool flag = ImageSource != null && previousPath != null && candidates.Contains<string>(previousPath, StringComparer.OrdinalIgnoreCase);
		List<string> list = candidates.Where((string path) => !avoidCurrentImage || candidates.Count == 1 || !string.Equals(path, previousPath, StringComparison.OrdinalIgnoreCase)).ToList();
		Shuffle(list);
		for (int num = 0; num < list.Count; num++)
		{
			try
			{
				ImageSource imageSource = imageLoader.Load(list[num]);
				currentImagePath = list[num];
				ImageSource = imageSource;
				IsActive = true;
				logger.LogInformation("Launcher background image loaded. CandidateCount={CandidateCount} Attempt={Attempt}", candidates.Count, num + 1);
				return true;
			}
			catch (Exception ex2) when (((ex2 is IOException || ex2 is FileFormatException || ex2 is UnauthorizedAccessException || ex2 is NotSupportedException || ex2 is ArgumentException || ex2 is InvalidOperationException || ex2 is COMException) ? 1 : 0) != 0)
			{
				logger.LogDebug(ex2, "Failed to decode launcher background image. Stage=Decode Attempt={Attempt} CandidateCount={CandidateCount}", num + 1, candidates.Count);
			}
		}
		if (!flag)
		{
			Deactivate();
		}
		if (reportFailure)
		{
			statusService.Report(flag ? Strings.Status_NoOtherLauncherBackgroundImages : Strings.Status_NoLauncherBackgroundImages);
		}
		return false;
	}

	public bool TryOpenDirectory()
	{
		try
		{
			string folderPath = catalog.EnsureDirectoryExists();
			if (folderService.TryOpen(folderPath))
			{
				return true;
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			logger.LogWarning(ex, "Failed to prepare launcher background image directory. Stage=OpenDirectory");
		}
		statusService.Report(Strings.Status_OpenLauncherBackgroundImageFolderFailed);
		return false;
	}

	public bool ClearImages()
	{
		try
		{
			catalog.ClearImages();
			Deactivate();
			logger.LogInformation("Launcher background images cleared.");
			return true;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			Deactivate();
			logger.LogWarning(ex, "Failed to clear launcher background images. Stage=Clear");
			statusService.Report(Strings.Status_ClearLauncherBackgroundImagesFailed);
			return false;
		}
	}

	public void Deactivate()
	{
		IsActive = false;
		ImageSource = null;
		currentImagePath = null;
	}

	private void Shuffle(IList<string> paths)
	{
		for (int num = paths.Count - 1; num > 0; num--)
		{
			int num2 = nextRandomIndex(num + 1);
			if (num2 < 0 || num2 > num)
			{
				throw new InvalidOperationException("The background image random index was outside the requested range.");
			}
			int index = num;
			int index2 = num2;
			string value = paths[num2];
			string value2 = paths[num];
			paths[index] = value;
			paths[index2] = value2;
		}
	}
}

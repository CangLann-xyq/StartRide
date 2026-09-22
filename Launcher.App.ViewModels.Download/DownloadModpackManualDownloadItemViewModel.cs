namespace Launcher.App.ViewModels.Download;

public sealed class DownloadModpackManualDownloadItemViewModel
{
	public string Title { get; }

	public string FileName { get; }

	public string FailureSummary { get; }

	public DownloadModpackManualDownloadItemViewModel(string title, string fileName, string failureSummary)
	{
		Title = title;
		FileName = fileName;
		FailureSummary = failureSummary;
	}
}

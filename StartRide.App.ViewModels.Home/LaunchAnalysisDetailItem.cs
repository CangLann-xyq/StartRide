namespace StartRide.App.ViewModels.Home;

public sealed record LaunchAnalysisDetailItem(string Summary, string OriginalReason, string OriginalSuggestion)
{
	public bool HasOriginalReason => !string.IsNullOrWhiteSpace(OriginalReason);

	public bool HasOriginalSuggestion => !string.IsNullOrWhiteSpace(OriginalSuggestion);
}

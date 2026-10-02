using System;

namespace StartRide.App.ViewModels.Settings;

public sealed class FeedbackChannelItem
{
	public string IconKey { get; }

	public string Title { get; }

	public string Description { get; }

	public string Target { get; }

	public FeedbackChannelItem(string iconKey, string title, string description, string target)
	{
		IconKey = iconKey;
		Title = title;
		Description = description;
		Target = target;
	}
}

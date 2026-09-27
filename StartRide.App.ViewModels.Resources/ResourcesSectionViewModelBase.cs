using CommunityToolkit.Mvvm.ComponentModel;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Resources;

public abstract class ResourcesSectionViewModelBase : ObservableObject
{
	public ResourcesPageViewModel Parent { get; }

	public string Title { get; }

	public string PlaceholderMessage => string.Format(Strings.Resources_SelectedPlaceholderMessageFormat, Title);

	protected ResourcesSectionViewModelBase(ResourcesPageViewModel parent, string title)
	{
		Parent = parent;
		Title = title;
	}
}

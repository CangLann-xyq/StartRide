namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceExportTypeOption
{
	public string Id { get; }

	public string Title { get; }

	public InstanceExportTypeOption(string id, string title)
	{
		Id = id;
		Title = title;
	}

	public override string ToString()
	{
		return Title;
	}
}

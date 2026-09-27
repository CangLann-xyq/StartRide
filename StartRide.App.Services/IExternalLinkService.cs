namespace StartRide.App.Services;

public interface IExternalLinkService
{
	bool TryOpen(string url);
}

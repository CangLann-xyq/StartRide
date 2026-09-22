using System;
using System.Diagnostics;

namespace Launcher.App.Services;

public sealed class ExternalLinkService : IExternalLinkService
{
	public bool TryOpen(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps))
		{
			return false;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = result.AbsoluteUri,
				UseShellExecute = true
			});
			return true;
		}
		catch
		{
			return false;
		}
	}
}

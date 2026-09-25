using System.Globalization;
using System.Net;
using StartRide.App.Resources;
using Launcher.Application.Accounts;

namespace StartRide.App.Services;

internal sealed class MicrosoftLoginBrowserPageProvider : IMicrosoftLoginBrowserPageProvider
{
	public string GetAuthorizationCompletedHtml()
	{
		CultureInfo currentUICulture = CultureInfo.CurrentUICulture;
		string value = WebUtility.HtmlEncode(currentUICulture.Name);
		string value2 = WebUtility.HtmlEncode(Strings.MicrosoftLogin_BrowserCompletionHeading);
		string value3 = WebUtility.HtmlEncode(string.Format(currentUICulture, Strings.MicrosoftLogin_BrowserCompletionMessageFormat, Strings.App_Title));
		return $"<!doctype html>\r\n<html lang=\"{value}\">\r\n<head>\r\n  <meta charset=\"utf-8\">\r\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n  <title>{value2}</title>\r\n</head>\r\n<body>\r\n  <h2>{value2}</h2>\r\n  <p>{value3}</p>\r\n</body>\r\n</html>";
	}
}

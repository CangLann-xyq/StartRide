using System;
using Launcher.App.Resources;
using Launcher.Application.Accounts;

namespace Launcher.App.ViewModels.Account;

internal static class AccountErrorCodeMessageFormatter
{
	public static string Format(Exception exception)
	{
		string text;
		if (exception is MicrosoftAccountNameChangeException ex)
		{
			string errorCode = ex.ErrorCode;
			if (errorCode != null && errorCode.Length > 0)
			{
				text = errorCode;
				goto IL_0078;
			}
		}
		else if (exception is MicrosoftAccountSkinUpdateException ex2)
		{
			string errorCode2 = ex2.ErrorCode;
			if (errorCode2 != null && errorCode2.Length > 0)
			{
				text = errorCode2;
				goto IL_0078;
			}
		}
		else if (exception is MicrosoftAccountProfileRefreshException ex3)
		{
			string errorCode3 = ex3.ErrorCode;
			if (errorCode3 != null && errorCode3.Length > 0)
			{
				text = errorCode3;
				goto IL_0078;
			}
		}
		text = null;
		goto IL_0078;
		IL_0078:
		string text2 = text;
		if (!string.IsNullOrWhiteSpace(text2))
		{
			return string.Format(Strings.Status_ErrorCodeFormat, text2);
		}
		return string.Empty;
	}
}

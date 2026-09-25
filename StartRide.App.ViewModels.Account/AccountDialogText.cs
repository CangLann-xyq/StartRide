using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Account;

internal static class AccountDialogText
{
	public static string GetRenameTitle(string step, bool isSuccessful)
	{
		if (!(step == "Status"))
		{
			if (step == "Result")
			{
				return isSuccessful ? Strings.Dialog_RenameAccountSuccessTitle : Strings.Dialog_RenameAccountFailedTitle;
			}
			return Strings.Dialog_RenameAccountTitle;
		}
		return Strings.Dialog_RenameAccountBusyTitle;
	}

	public static string GetRenameSubtitle(string step, bool isMicrosoftAccount)
	{
		if (!(step == "Status"))
		{
			if (step == "Result")
			{
				return Strings.Dialog_RenameAccountResultSubtitle;
			}
			return isMicrosoftAccount ? Strings.Dialog_RenameMicrosoftAccountSubtitle : Strings.Dialog_RenameOfflineAccountSubtitle;
		}
		return Strings.Dialog_RenameAccountBusySubtitle;
	}

	public static string GetAddTitle(string step, bool isMicrosoftAccountAlreadyAdded, bool isMicrosoftLoginSuccessful)
	{
		return step switch
		{
			"OfflineName" => Strings.Dialog_AddOfflineAccountTitle, 
			"ThirdPartyCredentials" => Strings.Dialog_AddThirdPartyAccountTitle, 
			"MicrosoftLogin" => Strings.Dialog_AddMicrosoftAccountTitle, 
			"MicrosoftResult" => isMicrosoftAccountAlreadyAdded ? Strings.Dialog_AddAccountAlreadyExistsTitle : (isMicrosoftLoginSuccessful ? Strings.Dialog_LoginSuccessTitle : Strings.Dialog_LoginIncompleteTitle), 
			_ => Strings.Dialog_AddAccountTitle, 
		};
	}

	public static string GetAddSubtitle(string step)
	{
		return step switch
		{
			"OfflineName" => Strings.Dialog_AddOfflineAccountSubtitle, 
			"ThirdPartyCredentials" => Strings.Dialog_AddThirdPartyAccountSubtitle, 
			"MicrosoftLogin" => Strings.Dialog_AddMicrosoftAccountSubtitle, 
			"MicrosoftResult" => Strings.Dialog_AddAccountResultSubtitle, 
			_ => Strings.Dialog_AddAccountSubtitle, 
		};
	}
}

using System.Collections.Generic;
using StartRide.App.Resources;
using Launcher.Application.Services;

namespace StartRide.App.ViewModels.GameSettings;

internal static class LocalContentImportPathEvaluator
{
	public static bool TryValidate(IInstanceContentImportPathValidator validator, IReadOnlyList<string> paths, InstanceContentImportKind kind, string invalidTypeMessage, out string failureMessage)
	{
		InstanceContentImportPathValidation instanceContentImportPathValidation = validator.Validate(paths, kind);
		failureMessage = ((instanceContentImportPathValidation.Failure == InstanceContentImportPathFailure.DirectoryNotSupported) ? Strings.GameSettings_DropFoldersUnsupportedMessage : (instanceContentImportPathValidation.IsValid ? string.Empty : invalidTypeMessage));
		return instanceContentImportPathValidation.IsValid;
	}
}

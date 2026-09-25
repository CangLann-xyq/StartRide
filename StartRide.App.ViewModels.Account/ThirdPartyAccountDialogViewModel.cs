using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using Launcher.Application.Accounts;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Account;

public sealed class ThirdPartyAccountDialogViewModel : ObservableObject
{
	private readonly AccountListViewModel accountList;

	private readonly IThirdPartyAccountService accountService;

	private readonly ILogger logger;

	[ObservableProperty]
	private string authenticationServer = string.Empty;

	[ObservableProperty]
	private string usernameOrEmail = string.Empty;

	[ObservableProperty]
	private bool hasPassword;

	[ObservableProperty]
	private string authenticationServerError = string.Empty;

	[ObservableProperty]
	private string usernameError = string.Empty;

	[ObservableProperty]
	private string passwordError = string.Empty;

	public bool CanConfirm
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(AuthenticationServer) && !string.IsNullOrWhiteSpace(UsernameOrEmail))
			{
				return HasPassword;
			}
			return false;
		}
	}

	public bool HasAuthenticationServerError => !string.IsNullOrWhiteSpace(AuthenticationServerError);

	public bool HasUsernameError => !string.IsNullOrWhiteSpace(UsernameError);

	public bool HasPasswordError => !string.IsNullOrWhiteSpace(PasswordError);

	public ObservableCollection<ThirdPartyProfileOptionViewModel> Profiles { get; } = new ObservableCollection<ThirdPartyProfileOptionViewModel>();

	public string? EmailAttemptId { get; private set; }

	public bool IsEmailIdentifier => UsernameOrEmail.Contains('@', StringComparison.Ordinal);

	public bool HasSelectedProfiles => Profiles.Any((ThirdPartyProfileOptionViewModel profile) => profile.IsSelected);

	public bool CanSelectAllProfiles
	{
		get
		{
			if (Profiles.Count > 0)
			{
				return Profiles.Any((ThirdPartyProfileOptionViewModel profile) => !profile.IsSelected);
			}
			return false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AuthenticationServer
	{
		get
		{
			return authenticationServer;
		}
		[MemberNotNull("authenticationServer")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(authenticationServer, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AuthenticationServer);
				authenticationServer = value;
				OnAuthenticationServerChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AuthenticationServer);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string UsernameOrEmail
	{
		get
		{
			return usernameOrEmail;
		}
		[MemberNotNull("usernameOrEmail")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(usernameOrEmail, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UsernameOrEmail);
				usernameOrEmail = value;
				OnUsernameOrEmailChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UsernameOrEmail);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasPassword
	{
		get
		{
			return hasPassword;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasPassword, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasPassword);
				hasPassword = value;
				OnHasPasswordChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasPassword);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AuthenticationServerError
	{
		get
		{
			return authenticationServerError;
		}
		[MemberNotNull("authenticationServerError")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(authenticationServerError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AuthenticationServerError);
				authenticationServerError = value;
				OnAuthenticationServerErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AuthenticationServerError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string UsernameError
	{
		get
		{
			return usernameError;
		}
		[MemberNotNull("usernameError")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(usernameError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UsernameError);
				usernameError = value;
				OnUsernameErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UsernameError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string PasswordError
	{
		get
		{
			return passwordError;
		}
		[MemberNotNull("passwordError")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(passwordError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PasswordError);
				passwordError = value;
				OnPasswordErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PasswordError);
			}
		}
	}

	public ThirdPartyAccountDialogViewModel(AccountListViewModel accountList, IThirdPartyAccountService accountService, ILogger logger)
	{
		this.accountList = accountList;
		this.accountService = accountService;
		this.logger = logger;
	}

	public void Reset()
	{
		AuthenticationServer = string.Empty;
		ResetCredentials();
	}

	public void ResetCredentials()
	{
		UsernameOrEmail = string.Empty;
		HasPassword = false;
		ClearErrors();
		Profiles.Clear();
		EmailAttemptId = null;
	}

	public void PrepareReauthentication(LauncherAccount account)
	{
		AuthenticationServer = account.AuthenticationServerUrl ?? string.Empty;
		UsernameOrEmail = account.ThirdPartyLoginUsername ?? string.Empty;
		HasPassword = false;
		ClearErrors();
	}

	public void UpdatePasswordState(bool hasPassword)
	{
		HasPassword = hasPassword;
	}

	public async Task<bool> BeginEmailLoginAsync(string password)
	{
		ClearErrors();
		if (!Validate(password))
		{
			return false;
		}
		try
		{
			ThirdPartyEmailLoginSession thirdPartyEmailLoginSession = await accountService.BeginEmailLoginAsync(AuthenticationServer.Trim(), UsernameOrEmail.Trim(), password);
			EmailAttemptId = thirdPartyEmailLoginSession.AttemptId;
			Profiles.Clear();
			foreach (ThirdPartyProfileOption profile in thirdPartyEmailLoginSession.Profiles)
			{
				ThirdPartyProfileOptionViewModel thirdPartyProfileOptionViewModel = new ThirdPartyProfileOptionViewModel(profile);
				thirdPartyProfileOptionViewModel.PropertyChanged += delegate(object? _, PropertyChangedEventArgs e)
				{
					if (e.PropertyName == "IsSelected")
					{
						OnPropertyChanged("HasSelectedProfiles");
						OnPropertyChanged("CanSelectAllProfiles");
					}
				};
				Profiles.Add(thirdPartyProfileOptionViewModel);
			}
			OnPropertyChanged("HasSelectedProfiles");
			OnPropertyChanged("CanSelectAllProfiles");
			return true;
		}
		catch (ThirdPartyAccountLoginException ex)
		{
			ApplyLoginError(ex.Reason);
			return false;
		}
		catch (Exception ex2) when (!(ex2 is OperationCanceledException))
		{
			logger.LogError(ex2, "Third-party email login failed.");
			AuthenticationServerError = Strings.Account_ThirdPartyServerInvalidResponse;
			return false;
		}
	}

	public void SelectAllProfiles()
	{
		foreach (ThirdPartyProfileOptionViewModel profile in Profiles)
		{
			profile.IsSelected = true;
		}
	}

	public async Task<LauncherAccount?> ImportEmailProfileAsync(ThirdPartyProfileOptionViewModel profile, string password, CancellationToken cancellationToken)
	{
		if (EmailAttemptId == null)
		{
			return null;
		}
		LauncherAccount importedAccount = null;
		bool isNewAccount = false;
		try
		{
			importedAccount = await accountService.ImportEmailProfileAsync(EmailAttemptId, profile.Uuid, password, cancellationToken);
			AccountItemViewModel accountItemViewModel = accountList.Accounts.FirstOrDefault((AccountItemViewModel item) => string.Equals(item.Id, importedAccount.Id, StringComparison.Ordinal));
			if (accountItemViewModel != null)
			{
				await accountList.ReplaceSelectedAccountAndPersistAsync(accountItemViewModel.Account, importedAccount);
			}
			else
			{
				isNewAccount = true;
				await accountList.AddAndSelectAsync(importedAccount);
			}
			return importedAccount;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Third-party email profile import failed. ProfileUuid={ProfileUuid}", profile.Uuid);
			if (isNewAccount && importedAccount != null)
			{
				await TryRollbackCredentialsAsync(importedAccount.Id);
			}
			return null;
		}
	}

	public async Task CancelEmailLoginAsync()
	{
		string emailAttemptId = EmailAttemptId;
		EmailAttemptId = null;
		if (emailAttemptId == null)
		{
			return;
		}
		try
		{
			await accountService.CancelEmailLoginAsync(emailAttemptId);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			logger.LogDebug(ex, "Unable to clean up third-party email login attempt.");
		}
	}

	public async Task<bool> LoginAsync(string password)
	{
		ClearErrors();
		if (!Validate(password))
		{
			return false;
		}
		LauncherAccount authenticatedAccount = null;
		bool isNewAccount = false;
		try
		{
			authenticatedAccount = await accountService.LoginWithUsernameAsync(AuthenticationServer.Trim(), UsernameOrEmail.Trim(), password);
			AccountItemViewModel accountItemViewModel = accountList.Accounts.FirstOrDefault((AccountItemViewModel item) => string.Equals(item.Id, authenticatedAccount.Id, StringComparison.Ordinal));
			if (accountItemViewModel == null)
			{
				isNewAccount = true;
				await accountList.AddAndSelectAsync(authenticatedAccount);
			}
			else
			{
				accountList.SelectItem(accountItemViewModel, persistSelection: false);
				await accountList.ReplaceSelectedAccountAndPersistAsync(accountItemViewModel.Account, authenticatedAccount);
			}
			return true;
		}
		catch (ThirdPartyAccountLoginException ex)
		{
			ApplyLoginError(ex.Reason);
			return false;
		}
		catch (Exception ex2) when (!(ex2 is OperationCanceledException))
		{
			logger.LogError(ex2, "Third-party account login or persistence failed.");
			PasswordError = Strings.Account_ThirdPartyLoginFailed;
			if (isNewAccount && authenticatedAccount != null)
			{
				await TryRollbackCredentialsAsync(authenticatedAccount.Id);
			}
			return false;
		}
	}

	public async Task<LauncherAccount?> ReauthenticateAsync(LauncherAccount account, string password)
	{
		ClearErrors();
		if (string.IsNullOrEmpty(password))
		{
			PasswordError = Strings.Account_ThirdPartyPasswordRequired;
			return null;
		}
		try
		{
			return await accountService.ReauthenticateAsync(account, password);
		}
		catch (ThirdPartyAccountLoginException ex)
		{
			ApplyLoginError(ex.Reason);
			return null;
		}
		catch (Exception ex2) when (!(ex2 is OperationCanceledException))
		{
			logger.LogError(ex2, "Third-party account reauthentication failed. AccountId={AccountId}", account.Id);
			PasswordError = Strings.Account_ThirdPartyLoginFailed;
			return null;
		}
	}

	private bool Validate(string password)
	{
		if (string.IsNullOrWhiteSpace(AuthenticationServer))
		{
			AuthenticationServerError = Strings.Account_ThirdPartyServerRequired;
		}
		if (string.IsNullOrWhiteSpace(UsernameOrEmail))
		{
			UsernameError = Strings.Account_ThirdPartyUsernameRequired;
		}
		if (string.IsNullOrEmpty(password))
		{
			PasswordError = Strings.Account_ThirdPartyPasswordRequired;
		}
		if (string.IsNullOrEmpty(AuthenticationServerError) && string.IsNullOrEmpty(UsernameError))
		{
			return string.IsNullOrEmpty(PasswordError);
		}
		return false;
	}

	private void ApplyLoginError(ThirdPartyAccountLoginFailureReason reason)
	{
		switch (reason)
		{
		case ThirdPartyAccountLoginFailureReason.InvalidServerAddress:
			AuthenticationServerError = Strings.Account_ThirdPartyServerInvalid;
			break;
		case ThirdPartyAccountLoginFailureReason.InsecureServerAddress:
			AuthenticationServerError = Strings.Account_ThirdPartyServerHttpsRequired;
			break;
		case ThirdPartyAccountLoginFailureReason.ServerUnavailable:
			AuthenticationServerError = Strings.Account_ThirdPartyServerUnavailable;
			break;
		case ThirdPartyAccountLoginFailureReason.UsernameLoginUnsupported:
			UsernameError = Strings.Account_ThirdPartyUsernameUnsupported;
			break;
		case ThirdPartyAccountLoginFailureReason.ProfileMissing:
		case ThirdPartyAccountLoginFailureReason.AccountMismatch:
			UsernameError = Strings.Account_ThirdPartyProfileMissing;
			break;
		case ThirdPartyAccountLoginFailureReason.InvalidCredentials:
			PasswordError = Strings.Account_ThirdPartyInvalidCredentials;
			break;
		case ThirdPartyAccountLoginFailureReason.CredentialStorageFailed:
			PasswordError = Strings.Account_ThirdPartyCredentialStorageFailed;
			break;
		default:
			AuthenticationServerError = Strings.Account_ThirdPartyServerInvalidResponse;
			break;
		}
	}

	private void ClearErrors()
	{
		AuthenticationServerError = string.Empty;
		UsernameError = string.Empty;
		PasswordError = string.Empty;
	}

	private async Task TryRollbackCredentialsAsync(string accountId)
	{
		try
		{
			await accountService.DeleteCredentialsAsync(accountId);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Third-party credential rollback failed. AccountId={AccountId}", accountId);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAuthenticationServerChanged(string value)
	{
		AuthenticationServerError = string.Empty;
		OnPropertyChanged("CanConfirm");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnUsernameOrEmailChanged(string value)
	{
		UsernameError = string.Empty;
		PasswordError = string.Empty;
		OnPropertyChanged("CanConfirm");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnHasPasswordChanged(bool value)
	{
		PasswordError = string.Empty;
		OnPropertyChanged("CanConfirm");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAuthenticationServerErrorChanged(string value)
	{
		OnPropertyChanged("HasAuthenticationServerError");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnUsernameErrorChanged(string value)
	{
		OnPropertyChanged("HasUsernameError");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnPasswordErrorChanged(string value)
	{
		OnPropertyChanged("HasPasswordError");
	}
}

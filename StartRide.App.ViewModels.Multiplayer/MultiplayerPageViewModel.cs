using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Account;
using Launcher.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StartRide.Core;

namespace StartRide.App.ViewModels.Multiplayer;

public sealed class MultiplayerPageViewModel : ObservableObject
{
	private readonly AccountPageViewModel? accountPage;

	private readonly IMultiplayerLobbyService lobbyService;

	private readonly IClipboardService clipboardService;

	private readonly IUiDispatcher uiDispatcher;

	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly IExternalLinkService? externalLinkService;

	private readonly ILogger<MultiplayerPageViewModel> logger;

	private readonly ApiService api = new();

	[ObservableProperty]
	private MultiplayerSectionItem? selectedSection;

	[ObservableProperty]
	private MultiplayerCreateLobbyStep createLobbyStep;

	[ObservableProperty]
	private string lobbyOwnerName = Strings.Multiplayer_LobbyOwnerPlaceholder;

	[ObservableProperty]
	private string roomCode = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor("JoinLobbyCommand")]
	private string joinRoomCode = string.Empty;

	[ObservableProperty]
	private bool isLobbyHost;

	[ObservableProperty]
	private bool isLeaveLobbyDialogOpen;

	[ObservableProperty]
	private bool isLobbySectionSwitchBlockedDialogOpen;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor("CreateLobbyCommand")]
	private bool isCreatingLobby;

	[ObservableProperty]
	private bool isLanWorldDetectionDialogOpen;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor("PasteRoomCodeCommand")]
	[NotifyCanExecuteChangedFor("JoinLobbyCommand")]
	private bool isJoiningLobby;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor("RequestLeaveLobbyCommand")]
	[NotifyCanExecuteChangedFor("ConfirmLeaveLobbyCommand")]
	private bool isStoppingLobby;

	[ObservableProperty]
	private string joinLobbyStatus = string.Empty;

	private bool isLoadingRooms;

	private string roomsLoadStatus = string.Empty;

	public bool IsLoadingRooms
	{
		get => isLoadingRooms;
		private set
		{
			if (isLoadingRooms != value)
			{
				isLoadingRooms = value;
				OnPropertyChanged(nameof(IsLoadingRooms));
				RefreshRoomsCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public string RoomsLoadStatus
	{
		get => roomsLoadStatus;
		private set
		{
			if (!string.Equals(roomsLoadStatus, value, StringComparison.Ordinal))
			{
				roomsLoadStatus = value;
				OnPropertyChanged(nameof(RoomsLoadStatus));
				OnPropertyChanged(nameof(HasRoomsLoadStatus));
			}
		}
	}

	public bool HasRoomsLoadStatus => !string.IsNullOrWhiteSpace(RoomsLoadStatus);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<MultiplayerSectionItem?>? selectSectionCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeLobbySectionSwitchBlockedDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? createLobbyCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelLobbyDetectionCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? pasteRoomCodeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? joinLobbyCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestLeaveLobbyCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelLeaveLobbyCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmLeaveLobbyCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? copyRoomCodeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openTerracottaProjectCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshRoomsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<PublicRoomItem?>? joinRoomCommand;

	public ObservableCollection<MultiplayerSectionItem> Sections { get; }

	public ObservableCollection<PublicRoomItem> PublicRooms { get; } = new ObservableCollection<PublicRoomItem>();

	public ObservableCollection<MultiplayerLobbyPlayerItem> LobbyPlayers { get; } = new ObservableCollection<MultiplayerLobbyPlayerItem>();

	public string SectionTitle
	{
		get
		{
			object obj;
			if (!IsLobbyStep)
			{
				obj = SelectedSection?.Title;
				if (obj == null)
				{
					return Strings.Multiplayer_SectionCreateLobby;
				}
			}
			else
			{
				obj = LobbyTitle;
			}
			return (string)obj;
		}
	}

	public bool IsCreateLobbySection
	{
		get
		{
			MultiplayerPageSection? multiplayerPageSection = SelectedSection?.Section;
			if (multiplayerPageSection.HasValue)
			{
				return multiplayerPageSection.GetValueOrDefault() == MultiplayerPageSection.CreateLobby;
			}
			return false;
		}
	}

	public bool IsJoinLobbySection
	{
		get
		{
			MultiplayerPageSection? multiplayerPageSection = SelectedSection?.Section;
			if (multiplayerPageSection.HasValue)
			{
				return multiplayerPageSection == MultiplayerPageSection.JoinLobby;
			}
			return false;
		}
	}

	public bool IsLobbyStep => CreateLobbyStep == MultiplayerCreateLobbyStep.Lobby;

	public string LobbyTitle => string.Format(Strings.Multiplayer_LobbyTitleFormat, LobbyOwnerName);

	public string JoinLobbyButtonText
	{
		get
		{
			if (!IsJoiningLobby)
			{
				return Strings.Multiplayer_SectionJoinLobby;
			}
			return Strings.Multiplayer_Join_Joining;
		}
	}

	public bool HasJoinLobbyStatus => !string.IsNullOrWhiteSpace(JoinLobbyStatus);

	public string LeaveLobbyButtonText
	{
		get
		{
			if (!IsLobbyHost)
			{
				return Strings.Multiplayer_LobbyLeaveButton;
			}
			return Strings.Multiplayer_LobbyLeaveAndDisbandButton;
		}
	}

	public string LeaveLobbyDialogTitle
	{
		get
		{
			if (!IsLobbyHost)
			{
				return Strings.Dialog_MultiplayerLeaveJoinedLobbyTitle;
			}
			return Strings.Dialog_MultiplayerLeaveLobbyTitle;
		}
	}

	public string LeaveLobbyDialogMessage
	{
		get
		{
			if (!IsLobbyHost)
			{
				return Strings.Dialog_MultiplayerLeaveJoinedLobbyMessage;
			}
			return Strings.Dialog_MultiplayerLeaveLobbyMessage;
		}
	}

	public string LeaveLobbyConfirmButtonText
	{
		get
		{
			if (!IsLobbyHost)
			{
				return Strings.Dialog_MultiplayerLeaveJoinedLobbyConfirmButton;
			}
			return Strings.Dialog_MultiplayerLeaveLobbyConfirmButton;
		}
	}

	private bool CanCreateLobby => !IsCreatingLobby;

	private bool CanPasteRoomCode => !IsJoiningLobby;

	private bool CanJoinLobby
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(JoinRoomCode))
			{
				return !IsJoiningLobby;
			}
			return false;
		}
	}

	private bool CanRequestLeaveLobby
	{
		get
		{
			if (IsLobbyStep)
			{
				return !IsStoppingLobby;
			}
			return false;
		}
	}

	private bool CanConfirmLeaveLobby
	{
		get
		{
			if (IsLobbyStep)
			{
				return !IsStoppingLobby;
			}
			return false;
		}
	}

	private bool CanCopyRoomCode => !string.IsNullOrWhiteSpace(RoomCode);

	private bool CanCancelLobbyDetection => IsCreatingLobby;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public MultiplayerSectionItem? SelectedSection
	{
		get
		{
			return selectedSection;
		}
		set
		{
			if (!EqualityComparer<MultiplayerSectionItem>.Default.Equals(selectedSection, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSection);
				selectedSection = value;
				OnSelectedSectionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSection);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public MultiplayerCreateLobbyStep CreateLobbyStep
	{
		get
		{
			return createLobbyStep;
		}
		set
		{
			if (!EqualityComparer<MultiplayerCreateLobbyStep>.Default.Equals(createLobbyStep, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CreateLobbyStep);
				createLobbyStep = value;
				OnCreateLobbyStepChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CreateLobbyStep);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LobbyOwnerName
	{
		get
		{
			return lobbyOwnerName;
		}
		[MemberNotNull("lobbyOwnerName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(lobbyOwnerName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LobbyOwnerName);
				lobbyOwnerName = value;
				OnLobbyOwnerNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LobbyOwnerName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string RoomCode
	{
		get
		{
			return roomCode;
		}
		[MemberNotNull("roomCode")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(roomCode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RoomCode);
				roomCode = value;
				OnRoomCodeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RoomCode);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string JoinRoomCode
	{
		get
		{
			return joinRoomCode;
		}
		[MemberNotNull("joinRoomCode")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(joinRoomCode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.JoinRoomCode);
				joinRoomCode = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.JoinRoomCode);
				JoinLobbyCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLobbyHost
	{
		get
		{
			return isLobbyHost;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLobbyHost, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLobbyHost);
				isLobbyHost = value;
				OnIsLobbyHostChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLobbyHost);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLeaveLobbyDialogOpen
	{
		get
		{
			return isLeaveLobbyDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLeaveLobbyDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLeaveLobbyDialogOpen);
				isLeaveLobbyDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLeaveLobbyDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLobbySectionSwitchBlockedDialogOpen
	{
		get
		{
			return isLobbySectionSwitchBlockedDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLobbySectionSwitchBlockedDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLobbySectionSwitchBlockedDialogOpen);
				isLobbySectionSwitchBlockedDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLobbySectionSwitchBlockedDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsCreatingLobby
	{
		get
		{
			return isCreatingLobby;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isCreatingLobby, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsCreatingLobby);
				isCreatingLobby = value;
				OnIsCreatingLobbyChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsCreatingLobby);
				CreateLobbyCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLanWorldDetectionDialogOpen
	{
		get
		{
			return isLanWorldDetectionDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLanWorldDetectionDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLanWorldDetectionDialogOpen);
				isLanWorldDetectionDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLanWorldDetectionDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsJoiningLobby
	{
		get
		{
			return isJoiningLobby;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isJoiningLobby, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsJoiningLobby);
				isJoiningLobby = value;
				OnIsJoiningLobbyChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsJoiningLobby);
				PasteRoomCodeCommand.NotifyCanExecuteChanged();
				JoinLobbyCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsStoppingLobby
	{
		get
		{
			return isStoppingLobby;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isStoppingLobby, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsStoppingLobby);
				isStoppingLobby = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsStoppingLobby);
				RequestLeaveLobbyCommand.NotifyCanExecuteChanged();
				ConfirmLeaveLobbyCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string JoinLobbyStatus
	{
		get
		{
			return joinLobbyStatus;
		}
		[MemberNotNull("joinLobbyStatus")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(joinLobbyStatus, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.JoinLobbyStatus);
				joinLobbyStatus = value;
				OnJoinLobbyStatusChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.JoinLobbyStatus);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<MultiplayerSectionItem?> SelectSectionCommand => selectSectionCommand ?? (selectSectionCommand = new RelayCommand<MultiplayerSectionItem>(SelectSection));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseLobbySectionSwitchBlockedDialogCommand => closeLobbySectionSwitchBlockedDialogCommand ?? (closeLobbySectionSwitchBlockedDialogCommand = new RelayCommand(CloseLobbySectionSwitchBlockedDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand CreateLobbyCommand => createLobbyCommand ?? (createLobbyCommand = new AsyncRelayCommand(CreateLobbyAsync, () => CanCreateLobby));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelLobbyDetectionCommand => cancelLobbyDetectionCommand ?? (cancelLobbyDetectionCommand = new RelayCommand(CancelLobbyDetection, () => CanCancelLobbyDetection));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand PasteRoomCodeCommand => pasteRoomCodeCommand ?? (pasteRoomCodeCommand = new AsyncRelayCommand(PasteRoomCodeAsync, () => CanPasteRoomCode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand JoinLobbyCommand => joinLobbyCommand ?? (joinLobbyCommand = new AsyncRelayCommand(JoinLobbyAsync, () => CanJoinLobby));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestLeaveLobbyCommand => requestLeaveLobbyCommand ?? (requestLeaveLobbyCommand = new RelayCommand(RequestLeaveLobby, () => CanRequestLeaveLobby));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelLeaveLobbyCommand => cancelLeaveLobbyCommand ?? (cancelLeaveLobbyCommand = new RelayCommand(CancelLeaveLobby));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmLeaveLobbyCommand => confirmLeaveLobbyCommand ?? (confirmLeaveLobbyCommand = new AsyncRelayCommand(ConfirmLeaveLobbyAsync, () => CanConfirmLeaveLobby));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand CopyRoomCodeCommand => copyRoomCodeCommand ?? (copyRoomCodeCommand = new AsyncRelayCommand(CopyRoomCodeAsync, () => CanCopyRoomCode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenTerracottaProjectCommand => openTerracottaProjectCommand ?? (openTerracottaProjectCommand = new RelayCommand(OpenTerracottaProject));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshRoomsCommand => refreshRoomsCommand ?? (refreshRoomsCommand = new AsyncRelayCommand(RefreshRoomsAsync, () => !IsLoadingRooms));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<PublicRoomItem?> JoinRoomCommand => joinRoomCommand ?? (joinRoomCommand = new AsyncRelayCommand<PublicRoomItem>(JoinRoomAsync, _ => !IsJoiningLobby));

	public MultiplayerPageViewModel(IMultiplayerLobbyService lobbyService, IClipboardService clipboardService, IUiDispatcher uiDispatcher, IStatusService statusService, IFloatingMessageService floatingMessageService, AccountPageViewModel? accountPage = null, IExternalLinkService? externalLinkService = null, ILogger<MultiplayerPageViewModel>? logger = null)
	{
		this.lobbyService = lobbyService;
		this.clipboardService = clipboardService;
		this.uiDispatcher = uiDispatcher;
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.accountPage = accountPage;
		this.externalLinkService = externalLinkService;
		this.logger = logger ?? NullLogger<MultiplayerPageViewModel>.Instance;
		Sections = new ObservableCollection<MultiplayerSectionItem>
		{
			new MultiplayerSectionItem(MultiplayerPageSection.CreateLobby, Strings.Multiplayer_SectionCreateLobby, "multiple_player/multi_create"),
			new MultiplayerSectionItem(MultiplayerPageSection.JoinLobby, Strings.Multiplayer_SectionJoinLobby, "multiple_player/multi_enter")
		};
		SelectedSection = Sections[0];
		lobbyService.SnapshotChanged += OnLobbySnapshotChanged;
		lobbyService.Stopped += OnLobbyStopped;
		// 首次进入联机页即拉取公开房间列表。
		_ = RefreshRoomsAsync(CancellationToken.None);
	}

	[RelayCommand]
	private async Task RefreshRoomsAsync(CancellationToken cancellationToken)
	{
		IsLoadingRooms = true;
		RoomsLoadStatus = string.Empty;
		try
		{
			List<Room> rooms = await api.GetRoomsAsync().ConfigureAwait(true);
			PublicRooms.Clear();
			// 后端只返回没过期的房间（90 秒无心跳即被清理），不再按 live 二次过滤，
			// 否则后端字段缺失时列表会永远是空的。
			foreach (Room room in rooms)
			{
				PublicRooms.Add(new PublicRoomItem(room));
			}
			if (PublicRooms.Count == 0)
			{
				RoomsLoadStatus = Strings.Multiplayer_Join_NoPublicRooms;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to load public multiplayer rooms.");
			RoomsLoadStatus = Strings.Multiplayer_Join_RoomsLoadFailed;
		}
		finally
		{
			IsLoadingRooms = false;
		}
	}

	[RelayCommand]
	private async Task JoinRoomAsync(PublicRoomItem? room, CancellationToken cancellationToken)
	{
		if (room == null)
		{
			return;
		}
		IsJoiningLobby = true;
		JoinLobbyStatus = string.Empty;
		try
		{
			string playerName = accountPage?.SelectedAccount?.DisplayName ?? Strings.Multiplayer_LobbyOwnerPlaceholder;
			MultiplayerLobbySnapshot multiplayerLobbySnapshot = await lobbyService.JoinAsync(room.RoomCode, playerName, cancellationToken);
			IsLobbyHost = false;
			JoinRoomCode = multiplayerLobbySnapshot.RoomCode;
			ApplyLobbySnapshot(multiplayerLobbySnapshot);
			CreateLobbyStep = MultiplayerCreateLobbyStep.Lobby;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (MultiplayerLobbyCreationException ex2)
		{
			logger.LogWarning(ex2, "Failed to join multiplayer lobby. Failure={Failure}", ex2.Failure);
			ReportJoinFailure(MapJoinFailure(ex2.Failure));
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to join multiplayer lobby.");
			ReportJoinFailure(Strings.Multiplayer_Join_Failed);
		}
		finally
		{
			IsJoiningLobby = false;
		}
	}

	[RelayCommand]
	private void SelectSection(MultiplayerSectionItem? section)
	{
		if (section != null && section.Section != SelectedSection?.Section)
		{
			if (IsLobbyStep)
			{
				IsLobbySectionSwitchBlockedDialogOpen = true;
			}
			else
			{
				SelectedSection = section;
			}
		}
	}

	[RelayCommand]
	private void CloseLobbySectionSwitchBlockedDialog()
	{
		IsLobbySectionSwitchBlockedDialogOpen = false;
	}

	[RelayCommand(CanExecute = "CanCreateLobby")]
	private async Task CreateLobbyAsync(CancellationToken cancellationToken)
	{
		IsCreatingLobby = true;
		IsLanWorldDetectionDialogOpen = true;
		try
		{
			string hostName = accountPage?.SelectedAccount?.DisplayName ?? Strings.Multiplayer_LobbyOwnerPlaceholder;
			MultiplayerLobbySnapshot snapshot = await lobbyService.CreateHostAsync(hostName, cancellationToken);
			IsLobbyHost = true;
			ApplyLobbySnapshot(snapshot);
			CreateLobbyStep = MultiplayerCreateLobbyStep.Lobby;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (MultiplayerLobbyCreationException ex2)
		{
			logger.LogWarning(ex2, "Failed to create multiplayer lobby. Failure={Failure}", ex2.Failure);
			ReportFailure(MapCreationFailure(ex2.Failure));
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to create multiplayer lobby.");
			ReportFailure(Strings.Multiplayer_Create_LobbyFailed);
		}
		finally
		{
			IsLanWorldDetectionDialogOpen = false;
			IsCreatingLobby = false;
		}
	}

	[RelayCommand(CanExecute = "CanCancelLobbyDetection")]
	private void CancelLobbyDetection()
	{
		IsLanWorldDetectionDialogOpen = false;
		CreateLobbyCommand.Cancel();
	}

	[RelayCommand(CanExecute = "CanPasteRoomCode")]
	private async Task PasteRoomCodeAsync(CancellationToken cancellationToken)
	{
		string text;
		try
		{
			text = await clipboardService.GetTextAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to read a Terracotta room code from the clipboard.");
			ReportJoinFailure(Strings.Multiplayer_Join_ClipboardEmpty);
			return;
		}
		string text2 = text?.Trim() ?? string.Empty;
		if (text2.Length == 0)
		{
			ReportJoinFailure(Strings.Multiplayer_Join_ClipboardEmpty);
			return;
		}
		JoinRoomCode = text2;
		JoinLobbyStatus = string.Empty;
	}

	[RelayCommand(CanExecute = "CanJoinLobby")]
	private async Task JoinLobbyAsync(CancellationToken cancellationToken)
	{
		string text = JoinRoomCode.Trim();
		if (text.Length == 0)
		{
			return;
		}
		IsJoiningLobby = true;
		JoinLobbyStatus = string.Empty;
		try
		{
			string playerName = accountPage?.SelectedAccount?.DisplayName ?? Strings.Multiplayer_LobbyOwnerPlaceholder;
			MultiplayerLobbySnapshot multiplayerLobbySnapshot = await lobbyService.JoinAsync(text, playerName, cancellationToken);
			IsLobbyHost = false;
			JoinRoomCode = multiplayerLobbySnapshot.RoomCode;
			ApplyLobbySnapshot(multiplayerLobbySnapshot);
			CreateLobbyStep = MultiplayerCreateLobbyStep.Lobby;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (MultiplayerLobbyCreationException ex2)
		{
			logger.LogWarning(ex2, "Failed to join multiplayer lobby. Failure={Failure}", ex2.Failure);
			ReportJoinFailure(MapJoinFailure(ex2.Failure));
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to join multiplayer lobby.");
			ReportJoinFailure(Strings.Multiplayer_Join_Failed);
		}
		finally
		{
			IsJoiningLobby = false;
		}
	}

	[RelayCommand(CanExecute = "CanRequestLeaveLobby")]
	private void RequestLeaveLobby()
	{
		IsLeaveLobbyDialogOpen = true;
	}

	[RelayCommand]
	private void CancelLeaveLobby()
	{
		IsLeaveLobbyDialogOpen = false;
	}

	[RelayCommand(CanExecute = "CanConfirmLeaveLobby")]
	private async Task ConfirmLeaveLobbyAsync(CancellationToken cancellationToken)
	{
		IsLeaveLobbyDialogOpen = false;
		IsStoppingLobby = true;
		try
		{
			await lobbyService.StopAsync(cancellationToken);
			ResetLobbyView();
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to stop multiplayer lobby cleanly.");
			ReportFailure(IsLobbyHost ? Strings.Multiplayer_LobbyDisbandFailed : Strings.Multiplayer_LobbyLeaveFailed);
		}
		finally
		{
			IsStoppingLobby = false;
		}
	}

	[RelayCommand(CanExecute = "CanCopyRoomCode")]
	private async Task CopyRoomCodeAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (await clipboardService.CopyTextAsync(RoomCode, cancellationToken))
			{
				statusService.Report(Strings.Multiplayer_LobbyRoomCodeCopied);
				floatingMessageService.Show(Strings.Multiplayer_LobbyRoomCodeCopied);
				return;
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to copy the multiplayer room code.");
		}
		statusService.Report(Strings.Multiplayer_LobbyRoomCodeCopyFailed);
		floatingMessageService.Show(Strings.Multiplayer_LobbyRoomCodeCopyFailed);
	}

	[RelayCommand]
	private void OpenTerracottaProject()
	{
		try
		{
			// 归属行写的是「StartRide 中继」，链接也必须指自己的项目（原先硬编码指向
			// 上游那套 Minecraft 穿透方案的仓库，改文案时漏掉了）。
			if (externalLinkService?.TryOpen(StartRide.Core.SiteLinks.RelayProjectUrl) ?? false)
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open the relay project page from the multiplayer page.");
			ReportExternalLinkFailure();
			return;
		}
		logger.LogWarning("Failed to open the relay project page from the multiplayer page.");
		ReportExternalLinkFailure();
	}

	private void OnLobbySnapshotChanged(MultiplayerLobbySnapshot snapshot)
	{
		uiDispatcher.Post(delegate
		{
			ApplyLobbySnapshot(snapshot);
		});
	}

	private void OnLobbyStopped(MultiplayerLobbyStopped stopped)
	{
		uiDispatcher.Post(delegate
		{
			ResetLobbyView();
			string text = stopped.Reason switch
			{
				MultiplayerLobbyStopReason.MinecraftWorldClosed => Strings.Multiplayer_LobbyWorldClosed, 
				MultiplayerLobbyStopReason.TerracottaExited => Strings.Multiplayer_LobbyTerracottaExited, 
				MultiplayerLobbyStopReason.TerracottaServiceFailed => Strings.Multiplayer_LobbyTerracottaServiceFailed, 
				_ => string.Empty, 
			};
			if (!string.IsNullOrEmpty(text))
			{
				ReportFailure(text);
			}
		});
	}

	private void ApplyLobbySnapshot(MultiplayerLobbySnapshot snapshot)
	{
		RoomCode = snapshot.RoomCode;
		LobbyOwnerName = snapshot.Players.FirstOrDefault((MultiplayerLobbyPlayer player) => player.Kind == MultiplayerLobbyPlayerKind.Host)?.DisplayName ?? Strings.Multiplayer_LobbyOwnerPlaceholder;
		LobbyPlayers.Clear();
		for (int num = 0; num < snapshot.Players.Count; num++)
		{
			MultiplayerLobbyPlayer multiplayerLobbyPlayer = snapshot.Players[num];
			ObservableCollection<MultiplayerLobbyPlayerItem> lobbyPlayers = LobbyPlayers;
			string displayName = multiplayerLobbyPlayer.DisplayName;
			string vendor = multiplayerLobbyPlayer.Vendor;
			int? latencyMilliseconds = multiplayerLobbyPlayer.LatencyMilliseconds;
			lobbyPlayers.Add(new MultiplayerLobbyPlayerItem(displayName, vendor, latencyMilliseconds.HasValue ? string.Format(arg0: latencyMilliseconds.GetValueOrDefault(), format: Strings.Multiplayer_LobbyLatencyFormat) : Strings.Multiplayer_LobbyLatencyUnknown, (multiplayerLobbyPlayer.Kind == MultiplayerLobbyPlayerKind.Host) ? Strings.Multiplayer_LobbyPlayerRoleHost : Strings.Multiplayer_LobbyPlayerRolePlayer, multiplayerLobbyPlayer.Kind == MultiplayerLobbyPlayerKind.Host, multiplayerLobbyPlayer.IsLocal, num == 0, num == snapshot.Players.Count - 1));
		}
	}

	private void ResetLobbyView()
	{
		CreateLobbyStep = MultiplayerCreateLobbyStep.Setup;
		IsLanWorldDetectionDialogOpen = false;
		IsLeaveLobbyDialogOpen = false;
		IsLobbySectionSwitchBlockedDialogOpen = false;
		IsLobbyHost = false;
		RoomCode = string.Empty;
		LobbyPlayers.Clear();
	}

	private void ReportFailure(string message)
	{
		if (IsJoinLobbySection)
		{
			JoinLobbyStatus = message;
		}
		statusService.Report(message);
		floatingMessageService.Show(message);
	}

	private void ReportJoinFailure(string message)
	{
		JoinLobbyStatus = message;
		statusService.Report(message);
		floatingMessageService.Show(message);
	}

	private void ReportExternalLinkFailure()
	{
		statusService.Report(Strings.Status_OpenTerracottaProjectFailed);
		floatingMessageService.Show(Strings.Status_OpenTerracottaProjectFailed);
	}

	private static string MapCreationFailure(MultiplayerLobbyCreationFailure failure)
	{
		return failure switch
		{
			MultiplayerLobbyCreationFailure.TerracottaUnavailable => Strings.Multiplayer_Create_TerracottaUnavailable, 
			MultiplayerLobbyCreationFailure.MinecraftWorldUnavailable => Strings.Multiplayer_Create_WorldUnavailable, 
			MultiplayerLobbyCreationFailure.TerracottaBusy => Strings.Multiplayer_Create_TerracottaBusy, 
			MultiplayerLobbyCreationFailure.TerracottaProtocolFailed => Strings.Multiplayer_Create_TerracottaProtocolFailed, 
			_ => Strings.Multiplayer_Create_LobbyFailed, 
		};
	}

	private static string MapJoinFailure(MultiplayerLobbyCreationFailure failure)
	{
		return failure switch
		{
			MultiplayerLobbyCreationFailure.InvalidRoomCode => Strings.Multiplayer_Join_InvalidRoomCode, 
			MultiplayerLobbyCreationFailure.TerracottaUnavailable => Strings.Multiplayer_Create_TerracottaUnavailable, 
			MultiplayerLobbyCreationFailure.TerracottaBusy => Strings.Multiplayer_Create_TerracottaBusy, 
			MultiplayerLobbyCreationFailure.TerracottaProtocolFailed => Strings.Multiplayer_Create_TerracottaProtocolFailed, 
			_ => Strings.Multiplayer_Join_Failed, 
		};
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedSectionChanged(MultiplayerSectionItem? value)
	{
		foreach (MultiplayerSectionItem section in Sections)
		{
			section.IsSelected = section == value;
		}
		OnPropertyChanged("SectionTitle");
		OnPropertyChanged("IsCreateLobbySection");
		OnPropertyChanged("IsJoinLobbySection");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCreateLobbyStepChanged(MultiplayerCreateLobbyStep value)
	{
		if (value != MultiplayerCreateLobbyStep.Lobby)
		{
			IsLeaveLobbyDialogOpen = false;
		}
		OnPropertyChanged("IsLobbyStep");
		OnPropertyChanged("SectionTitle");
		RequestLeaveLobbyCommand.NotifyCanExecuteChanged();
		ConfirmLeaveLobbyCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLobbyOwnerNameChanged(string value)
	{
		OnPropertyChanged("LobbyTitle");
		OnPropertyChanged("SectionTitle");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnRoomCodeChanged(string value)
	{
		CopyRoomCodeCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsLobbyHostChanged(bool value)
	{
		OnPropertyChanged("LeaveLobbyButtonText");
		OnPropertyChanged("LeaveLobbyDialogTitle");
		OnPropertyChanged("LeaveLobbyDialogMessage");
		OnPropertyChanged("LeaveLobbyConfirmButtonText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsCreatingLobbyChanged(bool value)
	{
		CancelLobbyDetectionCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsJoiningLobbyChanged(bool value)
	{
		OnPropertyChanged("JoinLobbyButtonText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnJoinLobbyStatusChanged(string value)
	{
		OnPropertyChanged("HasJoinLobbyStatus");
	}
}

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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<PublicRoomItem?>? chooseJoinRoomCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelJoinRoomChoiceCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmJoinRoomCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? rescanLobbyLevelsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<LevelOptionItem?>? selectLobbyLevelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SpawnOptionItem?>? selectLobbySpawnCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SpawnOptionItem?>? selectJoinSpawnCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? increaseCapacityCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? decreaseCapacityCommand;

	public ObservableCollection<MultiplayerSectionItem> Sections { get; }

	public ObservableCollection<PublicRoomItem> PublicRooms { get; } = new ObservableCollection<PublicRoomItem>();

	public ObservableCollection<MultiplayerLobbyPlayerItem> LobbyPlayers { get; } = new ObservableCollection<MultiplayerLobbyPlayerItem>();

	// ── 地图 / 出生位置 ──────────────────────────────────────────────────────
	// 房主侧：LobbyLevels 选地图，LobbySpawns 选自己落哪。
	// 加入侧：JoinSpawns 是「房主那张图」里的出生点，加入者各选各的。
	// 为什么要分两套：加入者的地图是被房主决定的，但他仍要能挑自己的落点，
	// 两边的可选集合来自不同的地图，混用一套会在切页时互相覆盖。

	public ObservableCollection<LevelOptionItem> LobbyLevels { get; } = new ObservableCollection<LevelOptionItem>();

	public ObservableCollection<SpawnOptionItem> LobbySpawns { get; } = new ObservableCollection<SpawnOptionItem>();

	public ObservableCollection<SpawnOptionItem> JoinSpawns { get; } = new ObservableCollection<SpawnOptionItem>();

	// ── 人数上限（只有房主能改）────────────────────────────────────────────
	// 建房前在设置层选，建房后还能在房间面板里改（走 set-capacity，不用重开房间）。
	// 取值范围与中继保持一致：2~16。下限 2 是「一个人不算联机」，上限 16 是中继
	// 单进程扇形广播能撑住的规模（车包按人数平方放大，再多会压垮中继出口带宽）。

	private const int CapacityMin = 2;
	private const int CapacityMax = 16;

	private int lobbyCapacity = 8;

	/// <summary>房主选定的人数上限。</summary>
	public int LobbyCapacity
	{
		get => lobbyCapacity;
		set
		{
			int clamped = Math.Max(CapacityMin, Math.Min(CapacityMax, value));
			if (lobbyCapacity == clamped) return;
			lobbyCapacity = clamped;
			OnPropertyChanged(nameof(LobbyCapacity));
			OnPropertyChanged(nameof(LobbyCapacityText));
			NotifyCapacityCommands();
		}
	}

	/// <summary>「4 人」这样的展示文本。</summary>
	public string LobbyCapacityText => string.Format(Strings.Lobby_CapacityValueFormat, LobbyCapacity);

	public bool CanIncreaseCapacity => LobbyCapacity < CapacityMax;

	public bool CanDecreaseCapacity => LobbyCapacity > CapacityMin;

	/// <summary>房间面板里显示的「人数 / 上限」。加入者也看得到（来自中继快照）。</summary>
	public string LobbyCapacitySummary
	{
		get
		{
			if (lobbyService is IStartRideLobbyCapacity cap)
			{
				int limit = cap.Capacity > 0 ? cap.Capacity : LobbyCapacity;
				return string.Format(Strings.Lobby_CapacitySummaryFormat, cap.PlayerCount, limit);
			}
			return string.Format(Strings.Lobby_CapacitySummaryFormat, LobbyPlayers.Count, LobbyCapacity);
		}
	}

	private void NotifyCapacityCommands()
	{
		IncreaseCapacityCommand.NotifyCanExecuteChanged();
		DecreaseCapacityCommand.NotifyCanExecuteChanged();
	}

	private bool CanIncreaseCapacityExecute() => CanIncreaseCapacity;

	private bool CanDecreaseCapacityExecute() => CanDecreaseCapacity;

	/// <summary>房主把上限 +1；建房前改的是待用值，建房后同时下发到中继。</summary>
	[RelayCommand(CanExecute = "CanIncreaseCapacityExecute")]
	private void IncreaseCapacity() => ApplyCapacity(LobbyCapacity + 1);

	[RelayCommand(CanExecute = "CanDecreaseCapacityExecute")]
	private void DecreaseCapacity() => ApplyCapacity(LobbyCapacity - 1);

	/// <summary>
	/// 落地一次上限修改。
	/// 建房**前**：只写进设置，建房时随 RoomMeta 一起带出去。
	/// 建房**后**（房主 + 已在房里）：同时发一条 set-capacity 让中继立刻生效。
	/// </summary>
	private void ApplyCapacity(int next)
	{
		LobbyCapacity = next;
		try
		{
			AppState.Current.Settings.LobbyCapacity = LobbyCapacity;
			AppState.Current.Settings.Save();
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to persist the multiplayer lobby capacity.");
		}

		if (IsLobbyHost && IsLobbyStep && lobbyService is IStartRideLobbyCapacity cap)
		{
			if (!cap.SetCapacity(LobbyCapacity))
			{
				ReportFailure(Strings.Lobby_CapacitySetFailed);
			}
		}
		OnPropertyChanged(nameof(LobbyCapacitySummary));
	}

	private LevelOptionItem? selectedLobbyLevel;
	private SpawnOptionItem? selectedLobbySpawn;
	private SpawnOptionItem? selectedJoinSpawn;
	private PublicRoomItem? selectedJoinRoom;
	private Dictionary<string, BeamNgLevel> lobbyLevelLookup =
		new Dictionary<string, BeamNgLevel>(StringComparer.OrdinalIgnoreCase);
	private bool lobbyLevelsLoaded;

	/// <summary>房主侧当前选中的地图。</summary>
	public LevelOptionItem? SelectedLobbyLevel
	{
		get => selectedLobbyLevel;
		set
		{
			if (EqualityComparer<LevelOptionItem>.Default.Equals(selectedLobbyLevel, value)) return;
			OnPropertyChanging("SelectedLobbyLevel");
			selectedLobbyLevel = value;
			OnSelectedLobbyLevelChanged(value);
			OnPropertyChanged("SelectedLobbyLevel");
		}
	}

	/// <summary>房主侧当前选中的出生点（也就是自己落地的地方）。</summary>
	public SpawnOptionItem? SelectedLobbySpawn
	{
		get => selectedLobbySpawn;
		set
		{
			if (EqualityComparer<SpawnOptionItem>.Default.Equals(selectedLobbySpawn, value)) return;
			OnPropertyChanging("SelectedLobbySpawn");
			selectedLobbySpawn = value;
			OnSelectedLobbySpawnChanged(value);
			OnPropertyChanged("SelectedLobbySpawn");
		}
	}

	/// <summary>加入侧当前选中的出生点（房主那张图里的，可以和他不一样）。</summary>
	public SpawnOptionItem? SelectedJoinSpawn
	{
		get => selectedJoinSpawn;
		set
		{
			if (EqualityComparer<SpawnOptionItem>.Default.Equals(selectedJoinSpawn, value)) return;
			OnPropertyChanging("SelectedJoinSpawn");
			selectedJoinSpawn = value;
			OnPropertyChanged("SelectedJoinSpawn");
		}
	}

	/// <summary>加入侧已挑好、等确认的房间。为 null 时显示房间列表。</summary>
	public PublicRoomItem? SelectedJoinRoom
	{
		get => selectedJoinRoom;
		set
		{
			if (EqualityComparer<PublicRoomItem>.Default.Equals(selectedJoinRoom, value)) return;
			OnPropertyChanging("SelectedJoinRoom");
			selectedJoinRoom = value;
			OnSelectedJoinRoomChanged(value);
			OnPropertyChanged("SelectedJoinRoom");
		}
	}

	public string SelectedLobbyLevelName => SelectedLobbyLevel?.Name ?? Strings.Lobby_LevelNoneFound;

	public string SelectedLobbyLevelDescription => SelectedLobbyLevel?.Description ?? string.Empty;

	public bool HasSelectedLobbyLevelDescription => !string.IsNullOrWhiteSpace(SelectedLobbyLevelDescription);

	public string SelectedLobbyLevelPreview => SelectedLobbyLevel?.PreviewPath ?? string.Empty;

	public bool HasSelectedLobbyLevelPreview => SelectedLobbyLevel?.HasPreview ?? false;

	public string SelectedLobbyLevelMeta => SelectedLobbyLevel?.Meta ?? string.Empty;

	public bool SelectedLobbyLevelIsMod => SelectedLobbyLevel?.IsMod ?? false;

	public string SelectedLobbySpawnName => SelectedLobbySpawn?.Name ?? Strings.Lobby_SpawnDefaultName;

	public bool HasLobbySpawns => LobbySpawns.Count > 0;

	public bool HasJoinSpawns => JoinSpawns.Count > 0;

	/// <summary>加入侧：已经挑好房间，显示确认区。</summary>
	public bool IsJoinRoomChosen => SelectedJoinRoom != null;

	/// <summary>
	/// 加入侧：列表里确实有房间可挑。
	/// 用来收掉「先在上面的列表里挑一个房间」——一个房间都没有时它和空状态是同一句话。
	/// </summary>
	public bool HasPublicRooms => PublicRooms.Count > 0;

	public string SelectedJoinRoomTitle => SelectedJoinRoom == null
		? string.Empty
		: string.Format(Strings.Lobby_JoinRoomTitleFormat, SelectedJoinRoom.Name);

	public string SelectedJoinRoomMapName => SelectedJoinRoom?.Map ?? string.Empty;

	public string SelectedJoinRoomSpawnName => SelectedJoinSpawn?.Name ?? Strings.Lobby_SpawnDefaultName;

	public string SelectedJoinRoomMeta => SelectedJoinRoom == null
		? string.Empty
		: string.Format(Strings.Lobby_JoinRoomMapFormat, SelectedJoinRoom.Map);

	/// <summary>
	/// 房间面板里显示的「本房地图」。
	/// 读的是设置而不是 SelectedLobbyLevel：加入者进的图是房主的，和房主页选的那张未必一样，
	/// 建房/进房成功后 lobbyService 会把最终结果写回设置，这里显示的就是真进的那张。
	/// </summary>
	public string LobbyMapText
	{
		get
		{
			var s = AppState.Current.Settings;
			if (string.IsNullOrWhiteSpace(s.LobbyMapId)) return "";
			return BeamNgLevelCatalog.DisplayName(s, s.LobbyMapId);
		}
	}

	/// <summary>房间面板里显示的「我的出生位置」。</summary>
	public string LobbySpawnText
	{
		get
		{
			var s = AppState.Current.Settings;
			var level = BeamNgLevelCatalog.Find(s, s.LobbyMapId);
			var hit = level?.SpawnPoints.Find(p =>
				string.Equals(p.ObjectName, s.LobbySpawnPoint, StringComparison.OrdinalIgnoreCase));
			return hit?.Name ?? Strings.Lobby_SpawnDefaultName;
		}
	}

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

	/// <summary>在房间列表里点一个房间：不直接进，先亮出「选出生位置」再确认。</summary>
	public IRelayCommand<PublicRoomItem?> ChooseJoinRoomCommand => chooseJoinRoomCommand ?? (chooseJoinRoomCommand = new RelayCommand<PublicRoomItem>(ChooseJoinRoom, _ => !IsJoiningLobby));

	/// <summary>放弃挑好的房间，回到房间列表。</summary>
	public IRelayCommand CancelJoinRoomChoiceCommand => cancelJoinRoomChoiceCommand ?? (cancelJoinRoomChoiceCommand = new RelayCommand(CancelJoinRoomChoice));

	/// <summary>带着选好的出生位置真正进房。</summary>
	public IAsyncRelayCommand ConfirmJoinRoomCommand => confirmJoinRoomCommand ?? (confirmJoinRoomCommand = new AsyncRelayCommand(ConfirmJoinRoomAsync, () => SelectedJoinRoom != null && !IsJoiningLobby));

	/// <summary>重新扫一遍游戏目录里的地图（刚装了 mod 地图时用）。</summary>
	public IAsyncRelayCommand RescanLobbyLevelsCommand => rescanLobbyLevelsCommand ?? (rescanLobbyLevelsCommand = new AsyncRelayCommand(RescanLobbyLevelsAsync, () => !IsCreatingLobby));

	public IRelayCommand<LevelOptionItem?> SelectLobbyLevelCommand => selectLobbyLevelCommand ?? (selectLobbyLevelCommand = new RelayCommand<LevelOptionItem>(item => { if (item != null) SelectedLobbyLevel = item; }));

	public IRelayCommand<SpawnOptionItem?> SelectLobbySpawnCommand => selectLobbySpawnCommand ?? (selectLobbySpawnCommand = new RelayCommand<SpawnOptionItem>(item => { if (item != null) SelectedLobbySpawn = item; }));

	public IRelayCommand<SpawnOptionItem?> SelectJoinSpawnCommand => selectJoinSpawnCommand ?? (selectJoinSpawnCommand = new RelayCommand<SpawnOptionItem>(item => { if (item != null) SelectedJoinSpawn = item; }));

	/// <summary>房主把人数上限 +1。</summary>
	public IRelayCommand IncreaseCapacityCommand => increaseCapacityCommand ?? (increaseCapacityCommand = new RelayCommand(IncreaseCapacity, CanIncreaseCapacityExecute));

	/// <summary>房主把人数上限 -1。</summary>
	public IRelayCommand DecreaseCapacityCommand => decreaseCapacityCommand ?? (decreaseCapacityCommand = new RelayCommand(DecreaseCapacity, CanDecreaseCapacityExecute));

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
		// 沿用上次建房选的人数上限
		lobbyCapacity = Math.Max(CapacityMin, Math.Min(CapacityMax, AppState.Current.Settings.LobbyCapacity));
		lobbyService.SnapshotChanged += OnLobbySnapshotChanged;
		lobbyService.Stopped += OnLobbyStopped;
		_ = LoadLobbyLevelsAsync();
		_ = RefreshRoomsAsync(CancellationToken.None);
	}

	[RelayCommand]
	private async Task RefreshRoomsAsync(CancellationToken cancellationToken)
	{
		IsLoadingRooms = true;
		RoomsLoadStatus = string.Empty;
		try
		{
			// 房间列表要显示「房主那张图」的名字，先把地图目录准备好（同一份缓存，只会读一次）
			await LoadLobbyLevelsAsync().ConfigureAwait(true);

			List<Room> rooms = await api.GetRoomsAsync().ConfigureAwait(true);
			PublicRooms.Clear();
			SelectedJoinRoom = null;

			foreach (Room room in rooms)
			{
				string mapName = string.Empty;
				if (!string.IsNullOrWhiteSpace(room.Map) &&
					lobbyLevelLookup.TryGetValue(room.Map, out var level))
				{
					mapName = level.Name;
				}
				PublicRooms.Add(new PublicRoomItem(room) { MapName = mapName });
			}
			if (PublicRooms.Count == 0)
			{
				RoomsLoadStatus = Strings.Multiplayer_Join_NoPublicRooms;
			}
			OnPropertyChanged("HasPublicRooms");
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
		OnPropertyChanged("LobbyMapText");
		OnPropertyChanged("LobbySpawnText");
		OnPropertyChanged(nameof(LobbyCapacitySummary));
		// 中继快照带回了权威上限时，把房主的本地选择同步过来，
		// 这样房间面板上的 +/- 与实际生效值始终一致（加入者也会看到房主设的数）。
		if (lobbyService is IStartRideLobbyCapacity cap && cap.Capacity > 0 && cap.Capacity != LobbyCapacity)
		{
			LobbyCapacity = cap.Capacity;
		}
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
		CancelJoinRoomChoice();
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

	// ── 地图 / 出生位置：加载与选择 ─────────────────────────────────────────

	/// <summary>
	/// 扫一遍游戏目录里的地图（含 mods 里的），填进房主侧的地图列表。
	/// 读的是游戏自己的 content/levels/*.zip 与 locales 词条，所以要放到后台线程做，
	/// 否则第一次进联机页会卡住 UI 一两秒。
	/// </summary>
	private async Task LoadLobbyLevelsAsync()
	{
		if (lobbyLevelsLoaded)
		{
			return;
		}
		lobbyLevelsLoaded = true;

		var settings = AppState.Current.Settings;
		List<BeamNgLevel> levels;
		try
		{
			levels = await Task.Run(() => BeamNgLevelCatalog.Load(settings)).ConfigureAwait(true);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to enumerate BeamNG levels for the multiplayer page.");
			levels = new List<BeamNgLevel>();
		}
		PopulateLobbyLevels(levels);
	}

	private void PopulateLobbyLevels(List<BeamNgLevel> levels)
	{
		lobbyLevelLookup = new Dictionary<string, BeamNgLevel>(StringComparer.OrdinalIgnoreCase);
		foreach (var level in levels)
		{
			lobbyLevelLookup[level.Id] = level;
		}

		LobbyLevels.Clear();
		foreach (var level in levels)
		{
			LobbyLevels.Add(new LevelOptionItem(level));
		}
		MarkEnds(LobbyLevels);

		var settings = AppState.Current.Settings;
		string wanted = settings.LobbyMapId ?? string.Empty;

		var pick = LobbyLevels.FirstOrDefault(item => string.Equals(item.Id, wanted, StringComparison.OrdinalIgnoreCase))
			?? LobbyLevels.FirstOrDefault(item => item.HasSpawnPoints)
			?? LobbyLevels.FirstOrDefault();

		SelectedLobbyLevel = pick;
		RebuildLobbySpawns(pick, settings.LobbySpawnPoint);
	}

	/// <summary>列表首尾行要收掉分隔线，否则卡片上下边会多出一条横线。</summary>
	private static void MarkEnds(IList<LevelOptionItem> items)
	{
		for (int i = 0; i < items.Count; i++)
		{
			items[i].IsFirst = i == 0;
			items[i].IsLast = i == items.Count - 1;
		}
	}

	private static void MarkEnds(IList<SpawnOptionItem> items)
	{
		for (int i = 0; i < items.Count; i++)
		{
			items[i].IsFirst = i == 0;
			items[i].IsLast = i == items.Count - 1;
		}
	}

	/// <summary>把「选中」状态刷到列表项上（ListPageItemButton 的选中底色就靠它）。</summary>
	private static void SyncSelection(IEnumerable<LevelOptionItem> items, LevelOptionItem? selected)
	{
		foreach (var item in items)
		{
			item.IsSelected = ReferenceEquals(item, selected);
		}
	}

	private static void SyncSelection(IEnumerable<SpawnOptionItem> items, SpawnOptionItem? selected)
	{
		foreach (var item in items)
		{
			item.IsSelected = ReferenceEquals(item, selected);
		}
	}

	private static void SyncSelection(IEnumerable<PublicRoomItem> items, PublicRoomItem? selected)
	{
		foreach (var item in items)
		{
			item.IsSelected = ReferenceEquals(item, selected);
		}
	}

	private void RebuildLobbySpawns(LevelOptionItem? level, string? wanted)
	{
		LobbySpawns.Clear();

		if (level != null)
		{
			foreach (var point in level.Level.SpawnPoints)
			{
				LobbySpawns.Add(new SpawnOptionItem(point));
			}
		}
		MarkEnds(LobbySpawns);

		SelectedLobbySpawn = PickSpawn(LobbySpawns, wanted);
		OnPropertyChanged("HasLobbySpawns");
	}

	private void RebuildJoinSpawns(BeamNgLevel? level, string? wanted)
	{
		JoinSpawns.Clear();

		if (level != null)
		{
			foreach (var point in level.SpawnPoints)
			{
				JoinSpawns.Add(new SpawnOptionItem(point));
			}
		}
		MarkEnds(JoinSpawns);

		SelectedJoinSpawn = PickSpawn(JoinSpawns, wanted);
		OnPropertyChanged("HasJoinSpawns");
	}

	/// <summary>优先用记忆里的出生点；它不属于这张图（换图了）就退到该图默认出生点。</summary>
	private static SpawnOptionItem? PickSpawn(IEnumerable<SpawnOptionItem> options, string? wanted)
	{
		var list = options as IList<SpawnOptionItem> ?? options.ToList();

		if (!string.IsNullOrWhiteSpace(wanted))
		{
			var hit = list.FirstOrDefault(item =>
				string.Equals(item.ObjectName, wanted, StringComparison.OrdinalIgnoreCase));
			if (hit != null) return hit;
		}

		return list.FirstOrDefault(item => item.IsDefault) ?? list.FirstOrDefault();
	}

	/// <summary>
	/// 把当前选择写回设置对象（不落盘）。
	/// 不立刻 Save 的原因：Save 会触发 AppSettings.Saved，让别的页面重读一遍设置；
	/// 真正落盘交给建房/进房时的 PersistLobbyChoice。
	/// </summary>
	private void RememberLobbyChoice()
	{
		var settings = AppState.Current.Settings;
		if (SelectedLobbyLevel != null) settings.LobbyMapId = SelectedLobbyLevel.Id;
		if (SelectedLobbySpawn != null) settings.LobbySpawnPoint = SelectedLobbySpawn.ObjectName;
	}

	private void ChooseJoinRoom(PublicRoomItem? room)
	{
		if (room == null)
		{
			return;
		}

		SelectedJoinRoom = room;

		// 出生点必须来自房主那张图 —— 拿的是自己的记忆值，但只在房主的图里有意义
		lobbyLevelLookup.TryGetValue(room.MapId ?? string.Empty, out var level);
		RebuildJoinSpawns(level, AppState.Current.Settings.LobbySpawnPoint);
	}

	private void CancelJoinRoomChoice()
	{
		SelectedJoinRoom = null;
		JoinSpawns.Clear();
		SelectedJoinSpawn = null;
		OnPropertyChanged("HasJoinSpawns");
	}

	private async Task ConfirmJoinRoomAsync(CancellationToken cancellationToken)
	{
		var room = SelectedJoinRoom;
		if (room == null)
		{
			return;
		}

		// 先把出生点写进设置：lobbyService.JoinAsync 会读 settings.LobbySpawnPoint，
		// 并按房主那张图校验（图上没有这个点就退回该图默认点）。
		AppState.Current.Settings.LobbySpawnPoint = SelectedJoinSpawn?.ObjectName ?? string.Empty;

		await JoinRoomAsync(room, cancellationToken).ConfigureAwait(true);
	}

	private async Task RescanLobbyLevelsAsync(CancellationToken cancellationToken)
	{
		await Task.Yield();
		BeamNgLevelCatalog.Invalidate();
		lobbyLevelsLoaded = false;
		await LoadLobbyLevelsAsync().ConfigureAwait(true);
	}

	private void OnSelectedLobbyLevelChanged(LevelOptionItem? value)
	{
		// 换图了：出生点集合跟着换，记忆的那个点如果不在这张图上会自动退到默认点
		RebuildLobbySpawns(value, AppState.Current.Settings.LobbySpawnPoint);
		RememberLobbyChoice();
		SyncSelection(LobbyLevels, value);

		OnPropertyChanged("SelectedLobbyLevelName");
		OnPropertyChanged("SelectedLobbyLevelDescription");
		OnPropertyChanged("HasSelectedLobbyLevelDescription");
		OnPropertyChanged("SelectedLobbyLevelPreview");
		OnPropertyChanged("HasSelectedLobbyLevelPreview");
		OnPropertyChanged("SelectedLobbyLevelMeta");
		OnPropertyChanged("SelectedLobbyLevelIsMod");
	}

	private void OnSelectedLobbySpawnChanged(SpawnOptionItem? value)
	{
		RememberLobbyChoice();
		SyncSelection(LobbySpawns, value);
		OnPropertyChanged("SelectedLobbySpawnName");
	}

	private void OnSelectedJoinRoomChanged(PublicRoomItem? value)
	{
		SyncSelection(PublicRooms, value);
		OnPropertyChanged("IsJoinRoomChosen");
		OnPropertyChanged("SelectedJoinRoomTitle");
		OnPropertyChanged("SelectedJoinRoomMapName");
		OnPropertyChanged("SelectedJoinRoomMeta");
		OnPropertyChanged("SelectedJoinRoomSpawnName");
		ConfirmJoinRoomCommand.NotifyCanExecuteChanged();
	}

	private void OnSelectedJoinSpawnChanged(SpawnOptionItem? value)
	{
		SyncSelection(JoinSpawns, value);
		OnPropertyChanged("SelectedJoinRoomSpawnName");
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
		RescanLobbyLevelsCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsJoiningLobbyChanged(bool value)
	{
		OnPropertyChanged("JoinLobbyButtonText");
		ChooseJoinRoomCommand.NotifyCanExecuteChanged();
		ConfirmJoinRoomCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnJoinLobbyStatusChanged(string value)
	{
		OnPropertyChanged("HasJoinLobbyStatus");
	}
}

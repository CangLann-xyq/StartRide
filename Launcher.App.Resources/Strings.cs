using System.Globalization;
using System.Resources;

namespace Launcher.App.Resources;

public static class Strings
{
	private static readonly ResourceManager ResourceManager = new ResourceManager("Launcher.App.Resources.Strings", typeof(Strings).Assembly);

	public static string App_Title => Get("App_Title");

	public static string MicrosoftLogin_BrowserCompletionHeading => Get("MicrosoftLogin_BrowserCompletionHeading");

	public static string MicrosoftLogin_BrowserCompletionMessageFormat => Get("MicrosoftLogin_BrowserCompletionMessageFormat");

	public static string Menu_Button => Get("Menu_Button");

	public static string Search_Placeholder => Get("Search_Placeholder");

	public static string Back_Button => Get("Back_Button");

	public static string Back_Tooltip => Get("Back_Tooltip");

	public static string Cancel_Button => Get("Cancel_Button");

	public static string Confirm_Button => Get("Confirm_Button");

	public static string Delete_Button => Get("Delete_Button");

	public static string Dialog_CloseWithDownloadsTitle => Get("Dialog_CloseWithDownloadsTitle");

	public static string Dialog_CloseWithDownloadsMessage => Get("Dialog_CloseWithDownloadsMessage");

	public static string Dialog_CloseAnywayButton => Get("Dialog_CloseAnywayButton");

	public static string Dialog_UserAgreementTitle => Get("Dialog_UserAgreementTitle");

	public static string Dialog_UserAgreementMessage => Get("Dialog_UserAgreementMessage");

	public static string Dialog_UserAgreementLink => Get("Dialog_UserAgreementLink");

	public static string Dialog_UserAgreementDisagreeButton => Get("Dialog_UserAgreementDisagreeButton");

	public static string Dialog_UserAgreementAgreeButton => Get("Dialog_UserAgreementAgreeButton");

	public static string Status_UserAgreementSaveFailed => Get("Status_UserAgreementSaveFailed");

	public static string Status_OpenUserAgreementFailed => Get("Status_OpenUserAgreementFailed");

	public static string Dialog_TerracottaAgreementTitle => Get("Dialog_TerracottaAgreementTitle");

	public static string Dialog_TerracottaAgreementMessage => Get("Dialog_TerracottaAgreementMessage");

	public static string Dialog_TerracottaProjectLink => Get("Dialog_TerracottaProjectLink");

	public static string Dialog_TerracottaDisagreeButton => Get("Dialog_TerracottaDisagreeButton");

	public static string Dialog_TerracottaAgreeButton => Get("Dialog_TerracottaAgreeButton");

	public static string Dialog_TerracottaDownloadPreparing => Get("Dialog_TerracottaDownloadPreparing");

	public static string Dialog_TerracottaDownloading => Get("Dialog_TerracottaDownloading");

	public static string Dialog_TerracottaExtracting => Get("Dialog_TerracottaExtracting");

	public static string Dialog_TerracottaDownloadComplete => Get("Dialog_TerracottaDownloadComplete");

	public static string Dialog_TerracottaDownloadFailed => Get("Dialog_TerracottaDownloadFailed");

	public static string Status_TerracottaReady => Get("Status_TerracottaReady");

	public static string Status_TerracottaDownloadFailed => Get("Status_TerracottaDownloadFailed");

	public static string Status_OpenTerracottaProjectFailed => Get("Status_OpenTerracottaProjectFailed");

	public static string Refresh_Button => Get("Refresh_Button");

	public static string ManualImport_Button => Get("ManualImport_Button");

	public static string Apply_Button => Get("Apply_Button");

	public static string Page_Account => Get("Page_Account");

	public static string Page_Home => Get("Page_Home");

	public static string Page_Download => Get("Page_Download");

	public static string Page_Install => Get("Page_Install");

	public static string Page_GameSettings => Get("Page_GameSettings");

	public static string Page_Multiplayer => Get("Page_Multiplayer");

	public static string Multiplayer_SectionCreateLobby => Get("Multiplayer_SectionCreateLobby");

	public static string Multiplayer_SectionJoinLobby => Get("Multiplayer_SectionJoinLobby");

	public static string Multiplayer_Create_LobbyFailed => Get("Multiplayer_Create_LobbyFailed");

	public static string Multiplayer_Create_TerracottaUnavailable => Get("Multiplayer_Create_TerracottaUnavailable");

	public static string Multiplayer_Create_WorldUnavailable => Get("Multiplayer_Create_WorldUnavailable");

	public static string Multiplayer_Create_TerracottaBusy => Get("Multiplayer_Create_TerracottaBusy");

	public static string Multiplayer_Create_TerracottaProtocolFailed => Get("Multiplayer_Create_TerracottaProtocolFailed");

	public static string Multiplayer_Create_StepOpenToLan => Get("Multiplayer_Create_StepOpenToLan");

	public static string Multiplayer_Create_StepSelectInstance => Get("Multiplayer_Create_StepSelectInstance");

	public static string Multiplayer_Create_StepCreateLobby => Get("Multiplayer_Create_StepCreateLobby");

	public static string Multiplayer_Create_OneClickHint => Get("Multiplayer_Create_OneClickHint");

	public static string Multiplayer_Create_OneClickButton => Get("Multiplayer_Create_OneClickButton");

	public static string Multiplayer_Create_TerracottaAttributionPrefix => Get("Multiplayer_Create_TerracottaAttributionPrefix");

	public static string Multiplayer_Create_TerracottaAttributionLinkText => Get("Multiplayer_Create_TerracottaAttributionLinkText");

	public static string Multiplayer_Create_TerracottaAttributionSuffix => Get("Multiplayer_Create_TerracottaAttributionSuffix");

	public static string Multiplayer_Join_StepEnterRoomCode => Get("Multiplayer_Join_StepEnterRoomCode");

	public static string Multiplayer_Join_StepJoinLobby => Get("Multiplayer_Join_StepJoinLobby");

	public static string Multiplayer_Join_RoomCodeSection => Get("Multiplayer_Join_RoomCodeSection");

	public static string Multiplayer_Join_RoomCodePlaceholder => Get("Multiplayer_Join_RoomCodePlaceholder");

	public static string Multiplayer_Join_PasteButton => Get("Multiplayer_Join_PasteButton");

	public static string Multiplayer_Join_Joining => Get("Multiplayer_Join_Joining");

	public static string Multiplayer_Join_InvalidRoomCode => Get("Multiplayer_Join_InvalidRoomCode");

	public static string Multiplayer_Join_Failed => Get("Multiplayer_Join_Failed");

	public static string Multiplayer_Join_ClipboardEmpty => Get("Multiplayer_Join_ClipboardEmpty");

	public static string Multiplayer_Join_RoomsSection => Get("Multiplayer_Join_RoomsSection");

	public static string Multiplayer_Join_RefreshButton => Get("Multiplayer_Join_RefreshButton");

	public static string Multiplayer_Join_NoPublicRooms => Get("Multiplayer_Join_NoPublicRooms");

	public static string Multiplayer_Join_RoomsLoadFailed => Get("Multiplayer_Join_RoomsLoadFailed");

	public static string Multiplayer_LobbyTitleFormat => Get("Multiplayer_LobbyTitleFormat");

	public static string Multiplayer_LobbyOwnerPlaceholder => Get("Multiplayer_LobbyOwnerPlaceholder");

	public static string Multiplayer_LobbyGameWorldPlaceholder => Get("Multiplayer_LobbyGameWorldPlaceholder");

	public static string Multiplayer_LobbyRoomCodeHeader => Get("Multiplayer_LobbyRoomCodeHeader");

	public static string Multiplayer_LobbyRoomCodePlaceholder => Get("Multiplayer_LobbyRoomCodePlaceholder");

	public static string Multiplayer_LobbyPlayerListHeader => Get("Multiplayer_LobbyPlayerListHeader");

	public static string Multiplayer_LobbyPlayerPlaceholderFormat => Get("Multiplayer_LobbyPlayerPlaceholderFormat");

	public static string Multiplayer_LobbyClientIdPlaceholderFormat => Get("Multiplayer_LobbyClientIdPlaceholderFormat");

	public static string Multiplayer_LobbyLatencyPlaceholder => Get("Multiplayer_LobbyLatencyPlaceholder");

	public static string Multiplayer_LobbyLatencyUnknown => Get("Multiplayer_LobbyLatencyUnknown");

	public static string Multiplayer_LobbyLatencyFormat => Get("Multiplayer_LobbyLatencyFormat");

	public static string Multiplayer_LobbyRoomCodeCopied => Get("Multiplayer_LobbyRoomCodeCopied");

	public static string Multiplayer_LobbyRoomCodeCopyFailed => Get("Multiplayer_LobbyRoomCodeCopyFailed");

	public static string Multiplayer_LobbyDisbandFailed => Get("Multiplayer_LobbyDisbandFailed");

	public static string Multiplayer_LobbyWorldClosed => Get("Multiplayer_LobbyWorldClosed");

	public static string Multiplayer_LobbyTerracottaExited => Get("Multiplayer_LobbyTerracottaExited");

	public static string Multiplayer_LobbyTerracottaServiceFailed => Get("Multiplayer_LobbyTerracottaServiceFailed");

	public static string Multiplayer_LobbyPoweredByTerracotta => Get("Multiplayer_LobbyPoweredByTerracotta");

	public static string Multiplayer_LobbyPlayerRoleHost => Get("Multiplayer_LobbyPlayerRoleHost");

	public static string Multiplayer_LobbyPlayerRolePlayer => Get("Multiplayer_LobbyPlayerRolePlayer");

	public static string Multiplayer_LobbyPlayerRoleSelf => Get("Multiplayer_LobbyPlayerRoleSelf");

	public static string Multiplayer_LobbyLeaveAndDisbandButton => Get("Multiplayer_LobbyLeaveAndDisbandButton");

	public static string Multiplayer_LobbyLeaveButton => Get("Multiplayer_LobbyLeaveButton");

	public static string Multiplayer_LobbyLeaveFailed => Get("Multiplayer_LobbyLeaveFailed");

	public static string Dialog_MultiplayerLeaveLobbyTitle => Get("Dialog_MultiplayerLeaveLobbyTitle");

	public static string Dialog_MultiplayerLeaveLobbyMessage => Get("Dialog_MultiplayerLeaveLobbyMessage");

	public static string Dialog_MultiplayerLeaveLobbyConfirmButton => Get("Dialog_MultiplayerLeaveLobbyConfirmButton");

	public static string Dialog_MultiplayerLeaveJoinedLobbyTitle => Get("Dialog_MultiplayerLeaveJoinedLobbyTitle");

	public static string Dialog_MultiplayerLeaveJoinedLobbyMessage => Get("Dialog_MultiplayerLeaveJoinedLobbyMessage");

	public static string Dialog_MultiplayerLeaveJoinedLobbyConfirmButton => Get("Dialog_MultiplayerLeaveJoinedLobbyConfirmButton");

	public static string Dialog_MultiplayerLobbySectionSwitchBlockedTitle => Get("Dialog_MultiplayerLobbySectionSwitchBlockedTitle");

	public static string Dialog_MultiplayerLobbySectionSwitchBlockedMessage => Get("Dialog_MultiplayerLobbySectionSwitchBlockedMessage");

	public static string Dialog_MultiplayerLanWorldDetectionTitle => Get("Dialog_MultiplayerLanWorldDetectionTitle");

	public static string Dialog_MultiplayerLanWorldDetectionMessage => Get("Dialog_MultiplayerLanWorldDetectionMessage");

	public static string Page_Resources => Get("Page_Resources");

	public static string Resources_SectionMods => Get("Resources_SectionMods");

	public static string Resources_SectionResourcePacks => Get("Resources_SectionResourcePacks");

	public static string Resources_SectionShaderPacks => Get("Resources_SectionShaderPacks");

	public static string Resources_SectionWorlds => Get("Resources_SectionWorlds");

	public static string Resources_SectionModpacks => Get("Resources_SectionModpacks");

	public static string Resources_PlaceholderMessage => Get("Resources_PlaceholderMessage");

	public static string Resources_SelectedPlaceholderMessageFormat => Get("Resources_SelectedPlaceholderMessageFormat");

	public static string Resources_ModFilterVersionLabel => Get("Resources_ModFilterVersionLabel");

	public static string Resources_ModFilterAllVersions => Get("Resources_ModFilterAllVersions");

	public static string Resources_ModFilterLoaderLabel => Get("Resources_ModFilterLoaderLabel");

	public static string Resources_ModFilterAllLoaders => Get("Resources_ModFilterAllLoaders");

	public static string Resources_ModFilterSourceLabel => Get("Resources_ModFilterSourceLabel");

	public static string Resources_ModFilterAllSources => Get("Resources_ModFilterAllSources");

	public static string Resources_ModFilterTypeLabel => Get("Resources_ModFilterTypeLabel");

	public static string Resources_ModFilterAllTypes => Get("Resources_ModFilterAllTypes");

	public static string Resources_ModFilterTypeOptimization => Get("Resources_ModFilterTypeOptimization");

	public static string Resources_ModFilterTypeUtility => Get("Resources_ModFilterTypeUtility");

	public static string Resources_ModFilterTypeAdventure => Get("Resources_ModFilterTypeAdventure");

	public static string Resources_ModFilterTypeDecoration => Get("Resources_ModFilterTypeDecoration");

	public static string Resources_ModFilterTypeEquipment => Get("Resources_ModFilterTypeEquipment");

	public static string Resources_ModFilterTypeTechnology => Get("Resources_ModFilterTypeTechnology");

	public static string Resources_ModFilterTypeMagic => Get("Resources_ModFilterTypeMagic");

	public static string Resources_ModFilterTypeMobs => Get("Resources_ModFilterTypeMobs");

	public static string Resources_ModFilterTypeWorldGeneration => Get("Resources_ModFilterTypeWorldGeneration");

	public static string Resources_ModFilterTypeStorage => Get("Resources_ModFilterTypeStorage");

	public static string Resources_ModFilterTypeLibrary => Get("Resources_ModFilterTypeLibrary");

	public static string Resources_ModFilterButtonLabel => Get("Resources_ModFilterButtonLabel");

	public static string Resources_ModSourceModrinth => Get("Resources_ModSourceModrinth");

	public static string Resources_ModSourceCurseForge => Get("Resources_ModSourceCurseForge");

	public static string Resources_ModVersionsUnknown => Get("Resources_ModVersionsUnknown");

	public static string Resources_ModLoadersUnknown => Get("Resources_ModLoadersUnknown");

	public static string Resources_ModProjectsLoading => Get("Resources_ModProjectsLoading");

	public static string Resources_ModProjectsEmpty => Get("Resources_ModProjectsEmpty");

	public static string Resources_ModProjectsLoadError => Get("Resources_ModProjectsLoadError");

	public static string Resources_ModProjectsLoadingMore => Get("Resources_ModProjectsLoadingMore");

	public static string Resources_ModProjectsNoMore => Get("Resources_ModProjectsNoMore");

	public static string Resources_ModProjectsLoadMoreError => Get("Resources_ModProjectsLoadMoreError");

	public static string Resources_ModCurseForgeMissingApiKey => Get("Resources_ModCurseForgeMissingApiKey");

	public static string Resources_ModDownloadsFormat => Get("Resources_ModDownloadsFormat");

	public static string Resources_ModDownloadsTenThousandFormat => Get("Resources_ModDownloadsTenThousandFormat");

	public static string Resources_ModDownloadsHundredMillionFormat => Get("Resources_ModDownloadsHundredMillionFormat");

	public static string Resources_ModDetailsInfoSection => Get("Resources_ModDetailsInfoSection");

	public static string Resources_ModDetailsVersionLabel => Get("Resources_ModDetailsVersionLabel");

	public static string Resources_ModDetailsLoaderLabel => Get("Resources_ModDetailsLoaderLabel");

	public static string Resources_ModDetailsSourceLabel => Get("Resources_ModDetailsSourceLabel");

	public static string Resources_ModDetailsRelatedWebsitesLabel => Get("Resources_ModDetailsRelatedWebsitesLabel");

	public static string Resources_ModDetailsDownloadsLabel => Get("Resources_ModDetailsDownloadsLabel");

	public static string Resources_ModDetailsTagsLabel => Get("Resources_ModDetailsTagsLabel");

	public static string Resources_ModDetailsDependenciesLabel => Get("Resources_ModDetailsDependenciesLabel");

	public static string Resources_ModInstallTargetSection => Get("Resources_ModInstallTargetSection");

	public static string Resources_ModInstallTargetLocal => Get("Resources_ModInstallTargetLocal");

	public static string Resources_ModInstallTargetsLoading => Get("Resources_ModInstallTargetsLoading");

	public static string Resources_ModInstallTargetsLoadError => Get("Resources_ModInstallTargetsLoadError");

	public static string Resources_ModVersionsLoading => Get("Resources_ModVersionsLoading");

	public static string Resources_ModVersionsEmpty => Get("Resources_ModVersionsEmpty");

	public static string Resources_ModVersionsEmptyLocal => Get("Resources_ModVersionsEmptyLocal");

	public static string Resources_ModVersionsFilterEmpty => Get("Resources_ModVersionsFilterEmpty");

	public static string Resources_ModVersionsLoadError => Get("Resources_ModVersionsLoadError");

	public static string Resources_ModVersionsLoadingMore => Get("Resources_ModVersionsLoadingMore");

	public static string Resources_ModVersionsNoMore => Get("Resources_ModVersionsNoMore");

	public static string Resources_ModVersionsLoadMoreError => Get("Resources_ModVersionsLoadMoreError");

	public static string Resources_ModVersionsAllTitle => Get("Resources_ModVersionsAllTitle");

	public static string Resources_ResourcePackFilterAllLoaders => Get("Resources_ResourcePackFilterAllLoaders");

	public static string Resources_ResourcePackFilterTypeSimplistic => Get("Resources_ResourcePackFilterTypeSimplistic");

	public static string Resources_ResourcePackFilterTypeThemed => Get("Resources_ResourcePackFilterTypeThemed");

	public static string Resources_ResourcePackFilterTypeRealistic => Get("Resources_ResourcePackFilterTypeRealistic");

	public static string Resources_ResourcePackFilterTypeVanillaLike => Get("Resources_ResourcePackFilterTypeVanillaLike");

	public static string Resources_ResourcePackFilterTypeAudio => Get("Resources_ResourcePackFilterTypeAudio");

	public static string Resources_ResourcePackProjectsLoading => Get("Resources_ResourcePackProjectsLoading");

	public static string Resources_ResourcePackProjectsEmpty => Get("Resources_ResourcePackProjectsEmpty");

	public static string Resources_ResourcePackProjectsLoadError => Get("Resources_ResourcePackProjectsLoadError");

	public static string Resources_ResourcePackProjectsLoadingMore => Get("Resources_ResourcePackProjectsLoadingMore");

	public static string Resources_ResourcePackProjectsNoMore => Get("Resources_ResourcePackProjectsNoMore");

	public static string Resources_ResourcePackProjectsLoadMoreError => Get("Resources_ResourcePackProjectsLoadMoreError");

	public static string Resources_ResourcePackCurseForgeMissingApiKey => Get("Resources_ResourcePackCurseForgeMissingApiKey");

	public static string Resources_ResourcePackDetailsInfoSection => Get("Resources_ResourcePackDetailsInfoSection");

	public static string Resources_ResourcePackInstallTargetSection => Get("Resources_ResourcePackInstallTargetSection");

	public static string Resources_ResourcePackInstallTargetLocal => Get("Resources_ResourcePackInstallTargetLocal");

	public static string Resources_ResourcePackInstallTargetsLoading => Get("Resources_ResourcePackInstallTargetsLoading");

	public static string Resources_ResourcePackInstallTargetsLoadError => Get("Resources_ResourcePackInstallTargetsLoadError");

	public static string Resources_ResourcePackVersionsLoading => Get("Resources_ResourcePackVersionsLoading");

	public static string Resources_ResourcePackVersionsEmpty => Get("Resources_ResourcePackVersionsEmpty");

	public static string Resources_ResourcePackVersionsEmptyLocal => Get("Resources_ResourcePackVersionsEmptyLocal");

	public static string Resources_ResourcePackVersionsFilterEmpty => Get("Resources_ResourcePackVersionsFilterEmpty");

	public static string Resources_ResourcePackVersionsLoadError => Get("Resources_ResourcePackVersionsLoadError");

	public static string Resources_ResourcePackVersionsLoadingMore => Get("Resources_ResourcePackVersionsLoadingMore");

	public static string Resources_ResourcePackVersionsNoMore => Get("Resources_ResourcePackVersionsNoMore");

	public static string Resources_ResourcePackVersionsLoadMoreError => Get("Resources_ResourcePackVersionsLoadMoreError");

	public static string Resources_ResourcePackVersionsAllTitle => Get("Resources_ResourcePackVersionsAllTitle");

	public static string Resources_ShaderPackFilterAllLoaders => Get("Resources_ShaderPackFilterAllLoaders");

	public static string Resources_ShaderPackFilterTypeCartoon => Get("Resources_ShaderPackFilterTypeCartoon");

	public static string Resources_ShaderPackFilterTypeCursed => Get("Resources_ShaderPackFilterTypeCursed");

	public static string Resources_ShaderPackFilterTypeFantasy => Get("Resources_ShaderPackFilterTypeFantasy");

	public static string Resources_ShaderPackFilterTypeRealistic => Get("Resources_ShaderPackFilterTypeRealistic");

	public static string Resources_ShaderPackFilterTypeSemiRealistic => Get("Resources_ShaderPackFilterTypeSemiRealistic");

	public static string Resources_ShaderPackFilterTypeVanillaLike => Get("Resources_ShaderPackFilterTypeVanillaLike");

	public static string Resources_ShaderPackProjectsLoading => Get("Resources_ShaderPackProjectsLoading");

	public static string Resources_ShaderPackProjectsEmpty => Get("Resources_ShaderPackProjectsEmpty");

	public static string Resources_ShaderPackProjectsLoadError => Get("Resources_ShaderPackProjectsLoadError");

	public static string Resources_ShaderPackProjectsLoadingMore => Get("Resources_ShaderPackProjectsLoadingMore");

	public static string Resources_ShaderPackProjectsNoMore => Get("Resources_ShaderPackProjectsNoMore");

	public static string Resources_ShaderPackProjectsLoadMoreError => Get("Resources_ShaderPackProjectsLoadMoreError");

	public static string Resources_ShaderPackCurseForgeMissingApiKey => Get("Resources_ShaderPackCurseForgeMissingApiKey");

	public static string Resources_ShaderPackDetailsInfoSection => Get("Resources_ShaderPackDetailsInfoSection");

	public static string Resources_ShaderPackInstallTargetSection => Get("Resources_ShaderPackInstallTargetSection");

	public static string Resources_ShaderPackInstallTargetLocal => Get("Resources_ShaderPackInstallTargetLocal");

	public static string Resources_ShaderPackInstallTargetsLoading => Get("Resources_ShaderPackInstallTargetsLoading");

	public static string Resources_ShaderPackInstallTargetsLoadError => Get("Resources_ShaderPackInstallTargetsLoadError");

	public static string Resources_ShaderPackVersionsLoading => Get("Resources_ShaderPackVersionsLoading");

	public static string Resources_ShaderPackVersionsEmpty => Get("Resources_ShaderPackVersionsEmpty");

	public static string Resources_ShaderPackVersionsEmptyLocal => Get("Resources_ShaderPackVersionsEmptyLocal");

	public static string Resources_ShaderPackVersionsFilterEmpty => Get("Resources_ShaderPackVersionsFilterEmpty");

	public static string Resources_ShaderPackVersionsLoadError => Get("Resources_ShaderPackVersionsLoadError");

	public static string Resources_ShaderPackVersionsLoadingMore => Get("Resources_ShaderPackVersionsLoadingMore");

	public static string Resources_ShaderPackVersionsNoMore => Get("Resources_ShaderPackVersionsNoMore");

	public static string Resources_ShaderPackVersionsLoadMoreError => Get("Resources_ShaderPackVersionsLoadMoreError");

	public static string Resources_ShaderPackVersionsAllTitle => Get("Resources_ShaderPackVersionsAllTitle");

	public static string Resources_WorldFilterAllLoaders => Get("Resources_WorldFilterAllLoaders");

	public static string Resources_WorldFilterTypeAdventure => Get("Resources_WorldFilterTypeAdventure");

	public static string Resources_WorldFilterTypeCreation => Get("Resources_WorldFilterTypeCreation");

	public static string Resources_WorldFilterTypeGameMap => Get("Resources_WorldFilterTypeGameMap");

	public static string Resources_WorldFilterTypeParkour => Get("Resources_WorldFilterTypeParkour");

	public static string Resources_WorldFilterTypePuzzle => Get("Resources_WorldFilterTypePuzzle");

	public static string Resources_WorldFilterTypeSurvival => Get("Resources_WorldFilterTypeSurvival");

	public static string Resources_WorldProjectsLoading => Get("Resources_WorldProjectsLoading");

	public static string Resources_WorldProjectsEmpty => Get("Resources_WorldProjectsEmpty");

	public static string Resources_WorldProjectsLoadError => Get("Resources_WorldProjectsLoadError");

	public static string Resources_WorldProjectsLoadingMore => Get("Resources_WorldProjectsLoadingMore");

	public static string Resources_WorldProjectsNoMore => Get("Resources_WorldProjectsNoMore");

	public static string Resources_WorldProjectsLoadMoreError => Get("Resources_WorldProjectsLoadMoreError");

	public static string Resources_WorldCurseForgeMissingApiKey => Get("Resources_WorldCurseForgeMissingApiKey");

	public static string Resources_WorldDetailsInfoSection => Get("Resources_WorldDetailsInfoSection");

	public static string Resources_WorldInstallTargetSection => Get("Resources_WorldInstallTargetSection");

	public static string Resources_WorldInstallTargetLocal => Get("Resources_WorldInstallTargetLocal");

	public static string Resources_WorldInstallTargetsLoading => Get("Resources_WorldInstallTargetsLoading");

	public static string Resources_WorldInstallTargetsLoadError => Get("Resources_WorldInstallTargetsLoadError");

	public static string Resources_WorldVersionsLoading => Get("Resources_WorldVersionsLoading");

	public static string Resources_WorldVersionsEmpty => Get("Resources_WorldVersionsEmpty");

	public static string Resources_WorldVersionsEmptyLocal => Get("Resources_WorldVersionsEmptyLocal");

	public static string Resources_WorldVersionsFilterEmpty => Get("Resources_WorldVersionsFilterEmpty");

	public static string Resources_WorldVersionsLoadError => Get("Resources_WorldVersionsLoadError");

	public static string Resources_WorldVersionsLoadingMore => Get("Resources_WorldVersionsLoadingMore");

	public static string Resources_WorldVersionsNoMore => Get("Resources_WorldVersionsNoMore");

	public static string Resources_WorldVersionsLoadMoreError => Get("Resources_WorldVersionsLoadMoreError");

	public static string Resources_WorldVersionsAllTitle => Get("Resources_WorldVersionsAllTitle");

	public static string Resources_ModpackFilterAllLoaders => Get("Resources_ModpackFilterAllLoaders");

	public static string Resources_ModpackFilterTypeAdventure => Get("Resources_ModpackFilterTypeAdventure");

	public static string Resources_ModpackFilterTypeTechnology => Get("Resources_ModpackFilterTypeTechnology");

	public static string Resources_ModpackFilterTypeMagic => Get("Resources_ModpackFilterTypeMagic");

	public static string Resources_ModpackFilterTypeOptimization => Get("Resources_ModpackFilterTypeOptimization");

	public static string Resources_ModpackFilterTypeQuests => Get("Resources_ModpackFilterTypeQuests");

	public static string Resources_ModpackFilterTypeKitchenSink => Get("Resources_ModpackFilterTypeKitchenSink");

	public static string Resources_ModpackFilterTypeLightweight => Get("Resources_ModpackFilterTypeLightweight");

	public static string Resources_ModpackFilterTypeMultiplayer => Get("Resources_ModpackFilterTypeMultiplayer");

	public static string Resources_ModpackFilterTypeExploration => Get("Resources_ModpackFilterTypeExploration");

	public static string Resources_ModpackProjectsLoading => Get("Resources_ModpackProjectsLoading");

	public static string Resources_ModpackProjectsEmpty => Get("Resources_ModpackProjectsEmpty");

	public static string Resources_ModpackProjectsLoadError => Get("Resources_ModpackProjectsLoadError");

	public static string Resources_ModpackProjectsLoadingMore => Get("Resources_ModpackProjectsLoadingMore");

	public static string Resources_ModpackProjectsNoMore => Get("Resources_ModpackProjectsNoMore");

	public static string Resources_ModpackProjectsLoadMoreError => Get("Resources_ModpackProjectsLoadMoreError");

	public static string Resources_ModpackCurseForgeMissingApiKey => Get("Resources_ModpackCurseForgeMissingApiKey");

	public static string Resources_ModpackDetailsInfoSection => Get("Resources_ModpackDetailsInfoSection");

	public static string Resources_ModpackInstallTargetSection => Get("Resources_ModpackInstallTargetSection");

	public static string Resources_ModpackInstallTargetNewInstance => Get("Resources_ModpackInstallTargetNewInstance");

	public static string Resources_ModpackInstallTargetServer => Get("Resources_ModpackInstallTargetServer");

	public static string Resources_ModpackInstallTargetLocal => Get("Resources_ModpackInstallTargetLocal");

	public static string Resources_ModpackInstallTargetsLoading => Get("Resources_ModpackInstallTargetsLoading");

	public static string Resources_ModpackInstallTargetsLoadError => Get("Resources_ModpackInstallTargetsLoadError");

	public static string Resources_ModpackVersionsLoading => Get("Resources_ModpackVersionsLoading");

	public static string Resources_ModpackVersionsEmpty => Get("Resources_ModpackVersionsEmpty");

	public static string Resources_ModpackVersionsEmptyLocal => Get("Resources_ModpackVersionsEmptyLocal");

	public static string Resources_ModpackVersionsFilterEmpty => Get("Resources_ModpackVersionsFilterEmpty");

	public static string Resources_ModpackVersionsLoadError => Get("Resources_ModpackVersionsLoadError");

	public static string Resources_ModpackVersionsLoadingMore => Get("Resources_ModpackVersionsLoadingMore");

	public static string Resources_ModpackVersionsNoMore => Get("Resources_ModpackVersionsNoMore");

	public static string Resources_ModpackVersionsLoadMoreError => Get("Resources_ModpackVersionsLoadMoreError");

	public static string Resources_ModpackVersionsAllTitle => Get("Resources_ModpackVersionsAllTitle");

	public static string Resources_ModDownloadFileExistsTitle => Get("Resources_ModDownloadFileExistsTitle");

	public static string Resources_ModDownloadFileExistsMessageFormat => Get("Resources_ModDownloadFileExistsMessageFormat");

	public static string Resources_ResourcePackDownloadFileExistsMessageFormat => Get("Resources_ResourcePackDownloadFileExistsMessageFormat");

	public static string Resources_ShaderPackDownloadFileExistsMessageFormat => Get("Resources_ShaderPackDownloadFileExistsMessageFormat");

	public static string Resources_WorldDownloadFileExistsMessageFormat => Get("Resources_WorldDownloadFileExistsMessageFormat");

	public static string Resources_ModpackDownloadFileExistsMessageFormat => Get("Resources_ModpackDownloadFileExistsMessageFormat");

	public static string Resources_ModUnknownInstanceVersionTitle => Get("Resources_ModUnknownInstanceVersionTitle");

	public static string Resources_ModUnknownInstanceVersionMessage => Get("Resources_ModUnknownInstanceVersionMessage");

	public static string Resources_ModRequiredDependenciesDialogTitle => Get("Resources_ModRequiredDependenciesDialogTitle");

	public static string Resources_ModRequiredDependenciesDialogMessage => Get("Resources_ModRequiredDependenciesDialogMessage");

	public static string Resources_ModRequiredDependencyInstalled => Get("Resources_ModRequiredDependencyInstalled");

	public static string Resources_ModRequiredDependencyMissing => Get("Resources_ModRequiredDependencyMissing");

	public static string Resources_ModRequiredDependencyUpdateRequired => Get("Resources_ModRequiredDependencyUpdateRequired");

	public static string Resources_ModRequiredDependencyVersionFormat => Get("Resources_ModRequiredDependencyVersionFormat");

	public static string Resources_ModRequiredDependencyMinimumVersionFormat => Get("Resources_ModRequiredDependencyMinimumVersionFormat");

	public static string Resources_ModRequiredDependencyInstallVersionFormat => Get("Resources_ModRequiredDependencyInstallVersionFormat");

	public static string Resources_ModRequiredDependencyVersionUnresolved => Get("Resources_ModRequiredDependencyVersionUnresolved");

	public static string Resources_ModRequiredDependenciesSkipButton => Get("Resources_ModRequiredDependenciesSkipButton");

	public static string Resources_ModRequiredDependenciesAutoInstallButton => Get("Resources_ModRequiredDependenciesAutoInstallButton");

	public static string Page_Settings => Get("Page_Settings");

	public static string Settings_SectionGeneral => Get("Settings_SectionGeneral");

	public static string Settings_SectionDownload => Get("Settings_SectionDownload");

	public static string Settings_SectionLanguage => Get("Settings_SectionLanguage");

	public static string Settings_SectionLaunchMemory => Get("Settings_SectionLaunchMemory");

	public static string Settings_SectionJava => Get("Settings_SectionJava");

	public static string Settings_SectionTheme => Get("Settings_SectionTheme");

	public static string Settings_SectionFeedback => Get("Settings_SectionFeedback");

	public static string Settings_SectionInfo => Get("Settings_SectionInfo");

	public static string Settings_SectionControlList => Get("Settings_SectionControlList");

	public static string Settings_InfoContent => Get("Settings_InfoContent");

	public static string Settings_LauncherVersionSection => Get("Settings_LauncherVersionSection");

	public static string Settings_LauncherVersionUnknown => Get("Settings_LauncherVersionUnknown");

	public static string Settings_ViewGithubRepositoryButton => Get("Settings_ViewGithubRepositoryButton");

	public static string Settings_CheckUpdatesButton => Get("Settings_CheckUpdatesButton");

	public static string Settings_UpdateSection => Get("Settings_UpdateSection");

	public static string Settings_UpdateChannelLabel => Get("Settings_UpdateChannelLabel");

	public static string Settings_UpdateChannelReleaseTitle => Get("Settings_UpdateChannelReleaseTitle");

	public static string Settings_UpdateChannelBetaTitle => Get("Settings_UpdateChannelBetaTitle");

	public static string Settings_ReferenceProjectsSection => Get("Settings_ReferenceProjectsSection");

	public static string Settings_LegalSection => Get("Settings_LegalSection");

	public static string Settings_CopyrightNotice => Get("Settings_CopyrightNotice");

	public static string Settings_CopyrightNoticeDescription => Get("Settings_CopyrightNoticeDescription");

	public static string Settings_OpenSourceLicense => Get("Settings_OpenSourceLicense");

	public static string Settings_OpenSourceLicenseDescription => Get("Settings_OpenSourceLicenseDescription");

	public static string Settings_UserAgreement => Get("Settings_UserAgreement");

	public static string Settings_UserAgreementDescription => Get("Settings_UserAgreementDescription");

	public static string Settings_ViewLegalDocumentButton => Get("Settings_ViewLegalDocumentButton");

	public static string Status_OpenLegalDocumentFailed => Get("Status_OpenLegalDocumentFailed");

	public static string Dialog_UpdateAvailableTitle => Get("Dialog_UpdateAvailableTitle");

	public static string Dialog_FeedbackTitle => Get("Dialog_FeedbackTitle");

	public static string Dialog_FeedbackFeatureSuggestionsButton => Get("Dialog_FeedbackFeatureSuggestionsButton");

	public static string Dialog_FeedbackBugReportsButton => Get("Dialog_FeedbackBugReportsButton");

	public static string Dialog_UpdateAvailableVersionFormat => Get("Dialog_UpdateAvailableVersionFormat");

	public static string Dialog_OpenUpdateChangelogButton => Get("Dialog_OpenUpdateChangelogButton");

	public static string Dialog_UpdateButton => Get("Dialog_UpdateButton");

	public static string Settings_GeneralAccountSection => Get("Settings_GeneralAccountSection");

	public static string Settings_OfflineUsernameLabel => Get("Settings_OfflineUsernameLabel");

	public static string Settings_GeneralDirectorySection => Get("Settings_GeneralDirectorySection");

	public static string Settings_DownloadSection => Get("Settings_DownloadSection");

	public static string Settings_DownloadSourceLabel => Get("Settings_DownloadSourceLabel");

	public static string Settings_MaximumDownloadThreadsLabel => Get("Settings_MaximumDownloadThreadsLabel");

	public static string Settings_DownloadSpeedLimitLabel => Get("Settings_DownloadSpeedLimitLabel");

	public static string Settings_DownloadSpeedLimitUnit => Get("Settings_DownloadSpeedLimitUnit");

	public static string Settings_CustomFileDownloadSection => Get("Settings_CustomFileDownloadSection");

	public static string Settings_CustomFileDownloadNewTaskLabel => Get("Settings_CustomFileDownloadNewTaskLabel");

	public static string Settings_CustomFileDownloadNewTaskButton => Get("Settings_CustomFileDownloadNewTaskButton");

	public static string Dialog_CustomFileDownloadTitle => Get("Dialog_CustomFileDownloadTitle");

	public static string Dialog_CustomFileDownloadAddressLabel => Get("Dialog_CustomFileDownloadAddressLabel");

	public static string Dialog_CustomFileDownloadAddressValidation => Get("Dialog_CustomFileDownloadAddressValidation");

	public static string Dialog_CustomFileDownloadDownloadButton => Get("Dialog_CustomFileDownloadDownloadButton");

	public static string FilePicker_CustomFileDownloadTitle => Get("FilePicker_CustomFileDownloadTitle");

	public static string FilePicker_CustomFileDownloadFilter => Get("FilePicker_CustomFileDownloadFilter");

	public static string Status_CustomFileDownloadPreparing => Get("Status_CustomFileDownloadPreparing");

	public static string Status_CustomFileDownloadStartedFormat => Get("Status_CustomFileDownloadStartedFormat");

	public static string Status_CustomFileDownloading => Get("Status_CustomFileDownloading");

	public static string Status_CustomFileDownloadCompleted => Get("Status_CustomFileDownloadCompleted");

	public static string Status_CustomFileDownloadFailed => Get("Status_CustomFileDownloadFailed");

	public static string DownloadSpeed_BytesPerSecondFormat => Get("DownloadSpeed_BytesPerSecondFormat");

	public static string DownloadSpeed_KilobytesPerSecondFormat => Get("DownloadSpeed_KilobytesPerSecondFormat");

	public static string DownloadSpeed_MegabytesPerSecondFormat => Get("DownloadSpeed_MegabytesPerSecondFormat");

	public static string Settings_DownloadSourceOfficial => Get("Settings_DownloadSourceOfficial");

	public static string Settings_DownloadSourceBmclApi => Get("Settings_DownloadSourceBmclApi");

	public static string Settings_LanguageSection => Get("Settings_LanguageSection");

	public static string Settings_LauncherLanguageLabel => Get("Settings_LauncherLanguageLabel");

	public static string Settings_LanguageSimplifiedChinese => Get("Settings_LanguageSimplifiedChinese");

	public static string Settings_LanguageTraditionalChinese => Get("Settings_LanguageTraditionalChinese");

	public static string Settings_LanguageJapanese => Get("Settings_LanguageJapanese");

	public static string Settings_LanguageEnglish => Get("Settings_LanguageEnglish");

	public static string Settings_LanguageRestartNotice => Get("Settings_LanguageRestartNotice");

	public static string Settings_GameLanguageSection => Get("Settings_GameLanguageSection");

	public static string Settings_AutoSetGameLanguageToLauncherLanguageLabel => Get("Settings_AutoSetGameLanguageToLauncherLanguageLabel");

	public static string Settings_GeneralFilesAndDirectoriesSection => Get("Settings_GeneralFilesAndDirectoriesSection");

	public static string Settings_DataDirectoryLabel => Get("Settings_DataDirectoryLabel");

	public static string Settings_MinecraftDirectoryLabel => Get("Settings_MinecraftDirectoryLabel");

	public static string Settings_MinecraftDirectoryItemTitle => Get("Settings_MinecraftDirectoryItemTitle");

	public static string Settings_MinecraftDirectoryListLabel => Get("Settings_MinecraftDirectoryListLabel");

	public static string Settings_AddMinecraftDirectoryButton => Get("Settings_AddMinecraftDirectoryButton");

	public static string Settings_RenameMinecraftDirectoryButton => Get("Settings_RenameMinecraftDirectoryButton");

	public static string Settings_RemoveMinecraftDirectoryFromListButton => Get("Settings_RemoveMinecraftDirectoryFromListButton");

	public static string Settings_RemoveMinecraftDirectoryConfirmButton => Get("Settings_RemoveMinecraftDirectoryConfirmButton");

	public static string Settings_AddMinecraftDirectoryConfirmButton => Get("Settings_AddMinecraftDirectoryConfirmButton");

	public static string Settings_RenameMinecraftDirectoryConfirmButton => Get("Settings_RenameMinecraftDirectoryConfirmButton");

	public static string Dialog_AddMinecraftDirectoryNameTitle => Get("Dialog_AddMinecraftDirectoryNameTitle");

	public static string Dialog_RenameMinecraftDirectoryNameTitle => Get("Dialog_RenameMinecraftDirectoryNameTitle");

	public static string Dialog_MinecraftDirectoryNameLabel => Get("Dialog_MinecraftDirectoryNameLabel");

	public static string Dialog_MinecraftDirectoryNameValidation => Get("Dialog_MinecraftDirectoryNameValidation");

	public static string Dialog_MinecraftDirectoryStartupRecoveryTitle => Get("Dialog_MinecraftDirectoryStartupRecoveryTitle");

	public static string Dialog_MinecraftDirectoryStartupSwitchedMessageFormat => Get("Dialog_MinecraftDirectoryStartupSwitchedMessageFormat");

	public static string Dialog_MinecraftDirectoryStartupDefaultMessageFormat => Get("Dialog_MinecraftDirectoryStartupDefaultMessageFormat");

	public static string Dialog_MinecraftDirectoryStartupRecoveryFailedTitle => Get("Dialog_MinecraftDirectoryStartupRecoveryFailedTitle");

	public static string Dialog_MinecraftDirectoryStartupRecoveryFailedMessageFormat => Get("Dialog_MinecraftDirectoryStartupRecoveryFailedMessageFormat");

	public static string Dialog_RemoveMinecraftDirectoryTitle => Get("Dialog_RemoveMinecraftDirectoryTitle");

	public static string Dialog_RemoveMinecraftDirectoryMessageFormat => Get("Dialog_RemoveMinecraftDirectoryMessageFormat");

	public static string Dialog_ClearLauncherLogsTitle => Get("Dialog_ClearLauncherLogsTitle");

	public static string Dialog_ClearLauncherLogsMessage => Get("Dialog_ClearLauncherLogsMessage");

	public static string Settings_MinecraftDirectoryUnavailable => Get("Settings_MinecraftDirectoryUnavailable");

	public static string Settings_OfficialMinecraftDirectoryDisplayName => Get("Settings_OfficialMinecraftDirectoryDisplayName");

	public static string Settings_MinecraftDirectoryChangeBlockedByActiveTasks => Get("Settings_MinecraftDirectoryChangeBlockedByActiveTasks");

	public static string Settings_LauncherLogDirectoryLabel => Get("Settings_LauncherLogDirectoryLabel");

	public static string Settings_LauncherLogDirectoryDescription => Get("Settings_LauncherLogDirectoryDescription");

	public static string Settings_DiagnosticLoggingLabel => Get("Settings_DiagnosticLoggingLabel");

	public static string Settings_DiagnosticLoggingDescription => Get("Settings_DiagnosticLoggingDescription");

	public static string Settings_OpenMinecraftDirectoryButton => Get("Settings_OpenMinecraftDirectoryButton");

	public static string Settings_ChangeMinecraftDirectoryButton => Get("Settings_ChangeMinecraftDirectoryButton");

	public static string Settings_OpenLauncherLogDirectoryButton => Get("Settings_OpenLauncherLogDirectoryButton");

	public static string Settings_ClearLauncherLogsButton => Get("Settings_ClearLauncherLogsButton");

	public static string Settings_ClearLauncherLogsConfirmButton => Get("Settings_ClearLauncherLogsConfirmButton");

	public static string Settings_LaunchDefaultsSection => Get("Settings_LaunchDefaultsSection");

	public static string Settings_MemorySection => Get("Settings_MemorySection");

	public static string Settings_DefaultMemoryLabel => Get("Settings_DefaultMemoryLabel");

	public static string Settings_MemoryOptionFormat => Get("Settings_MemoryOptionFormat");

	public static string Settings_MemoryModeLabel => Get("Settings_MemoryModeLabel");

	public static string Settings_MemoryModeAuto => Get("Settings_MemoryModeAuto");

	public static string Settings_MemoryModeManual => Get("Settings_MemoryModeManual");

	public static string Settings_MaxMemoryLabel => Get("Settings_MaxMemoryLabel");

	public static string Settings_AutomaticMemoryAllocationLabel => Get("Settings_AutomaticMemoryAllocationLabel");

	public static string Settings_SystemTotalMemoryLabel => Get("Settings_SystemTotalMemoryLabel");

	public static string Settings_SystemAvailableMemoryLabel => Get("Settings_SystemAvailableMemoryLabel");

	public static string Settings_SystemMemorySummaryLabel => Get("Settings_SystemMemorySummaryLabel");

	public static string Settings_SystemMemorySummaryFormat => Get("Settings_SystemMemorySummaryFormat");

	public static string Settings_MemorySizeMbFormat => Get("Settings_MemorySizeMbFormat");

	public static string Settings_MemorySizeGbFormat => Get("Settings_MemorySizeGbFormat");

	public static string Settings_MemoryUnavailable => Get("Settings_MemoryUnavailable");

	public static string Settings_JavaSection => Get("Settings_JavaSection");

	public static string Settings_JavaSelectionLabel => Get("Settings_JavaSelectionLabel");

	public static string Settings_JavaSelectionAuto => Get("Settings_JavaSelectionAuto");

	public static string Settings_JavaSelectionManual => Get("Settings_JavaSelectionManual");

	public static string Settings_JavaListLabel => Get("Settings_JavaListLabel");

	public static string Settings_JavaListLoading => Get("Settings_JavaListLoading");

	public static string Settings_JavaListEmpty => Get("Settings_JavaListEmpty");

	public static string Settings_JavaVersionUnknown => Get("Settings_JavaVersionUnknown");

	public static string Settings_ThemeSection => Get("Settings_ThemeSection");

	public static string Settings_ThemeFollowSystemLabel => Get("Settings_ThemeFollowSystemLabel");

	public static string Settings_ThemeSelectionLabel => Get("Settings_ThemeSelectionLabel");

	public static string Settings_AccentSection => Get("Settings_AccentSection");

	public static string Settings_AccentSelectionLabel => Get("Settings_AccentSelectionLabel");

	public static string Settings_BackgroundSection => Get("Settings_BackgroundSection");

	public static string Settings_BackgroundEffectNoneTitle => Get("Settings_BackgroundEffectNoneTitle");

	public static string Settings_BackgroundEffectAcrylicTitle => Get("Settings_BackgroundEffectAcrylicTitle");

	public static string Settings_BackgroundEffectImageTitle => Get("Settings_BackgroundEffectImageTitle");

	public static string Settings_BackgroundImageSelectionLabel => Get("Settings_BackgroundImageSelectionLabel");

	public static string Settings_BackgroundImageSelectionDescription => Get("Settings_BackgroundImageSelectionDescription");

	public static string Settings_BackgroundImageControlBlurLabel => Get("Settings_BackgroundImageControlBlurLabel");

	public static string Settings_BackgroundImageControlBlurDescription => Get("Settings_BackgroundImageControlBlurDescription");

	public static string Settings_BackgroundImageOpenFolderButton => Get("Settings_BackgroundImageOpenFolderButton");

	public static string Settings_BackgroundImageRefreshButton => Get("Settings_BackgroundImageRefreshButton");

	public static string Settings_BackgroundImageClearButton => Get("Settings_BackgroundImageClearButton");

	public static string Status_OpenLauncherBackgroundImageFolderFailed => Get("Status_OpenLauncherBackgroundImageFolderFailed");

	public static string Status_ClearLauncherBackgroundImagesFailed => Get("Status_ClearLauncherBackgroundImagesFailed");

	public static string Status_NoLauncherBackgroundImages => Get("Status_NoLauncherBackgroundImages");

	public static string Status_NoOtherLauncherBackgroundImages => Get("Status_NoOtherLauncherBackgroundImages");

	public static string Status_ReadLauncherBackgroundImagesFailed => Get("Status_ReadLauncherBackgroundImagesFailed");

	public static string Settings_BackgroundOpacityLabel => Get("Settings_BackgroundOpacityLabel");

	public static string Settings_ThemeDarkTitle => Get("Settings_ThemeDarkTitle");

	public static string Settings_ThemeLightTitle => Get("Settings_ThemeLightTitle");

	public static string Settings_AccentColorBlueTitle => Get("Settings_AccentColorBlueTitle");

	public static string Settings_AccentColorCyanTitle => Get("Settings_AccentColorCyanTitle");

	public static string Settings_AccentColorGreenTitle => Get("Settings_AccentColorGreenTitle");

	public static string Settings_AccentColorEmeraldTitle => Get("Settings_AccentColorEmeraldTitle");

	public static string Settings_AccentColorPurpleTitle => Get("Settings_AccentColorPurpleTitle");

	public static string Settings_AccentColorPinkTitle => Get("Settings_AccentColorPinkTitle");

	public static string Settings_AccentColorOrangeTitle => Get("Settings_AccentColorOrangeTitle");

	public static string Settings_AccentColorAmberTitle => Get("Settings_AccentColorAmberTitle");

	public static string Settings_ControlDemoButtonsSection => Get("Settings_ControlDemoButtonsSection");

	public static string Settings_ControlDemoInputsSection => Get("Settings_ControlDemoInputsSection");

	public static string Settings_ControlDemoSelectionSection => Get("Settings_ControlDemoSelectionSection");

	public static string Settings_ControlDemoFeedbackSection => Get("Settings_ControlDemoFeedbackSection");

	public static string Settings_ControlDemoMenuButton => Get("Settings_ControlDemoMenuButton");

	public static string Settings_ControlDemoListItemTitle => Get("Settings_ControlDemoListItemTitle");

	public static string Settings_ControlDemoListItemSubtitle => Get("Settings_ControlDemoListItemSubtitle");

	public static string Settings_ControlDemoListItemTrailing => Get("Settings_ControlDemoListItemTrailing");

	public static string Settings_ControlDemoNormalButton => Get("Settings_ControlDemoNormalButton");

	public static string Settings_ControlDemoPrimaryButton => Get("Settings_ControlDemoPrimaryButton");

	public static string Settings_ControlDemoDangerButton => Get("Settings_ControlDemoDangerButton");

	public static string Settings_ControlDemoIconButton => Get("Settings_ControlDemoIconButton");

	public static string Settings_ControlDemoSwitchLabel => Get("Settings_ControlDemoSwitchLabel");

	public static string Settings_ControlDemoSliderLabel => Get("Settings_ControlDemoSliderLabel");

	public static string Settings_ControlDemoTextBoxLabel => Get("Settings_ControlDemoTextBoxLabel");

	public static string Settings_ControlDemoMultilineTextBoxLabel => Get("Settings_ControlDemoMultilineTextBoxLabel");

	public static string Settings_ControlDemoSearchBoxLabel => Get("Settings_ControlDemoSearchBoxLabel");

	public static string Settings_ControlDemoReadOnlyFieldLabel => Get("Settings_ControlDemoReadOnlyFieldLabel");

	public static string Settings_ControlDemoComboBoxLabel => Get("Settings_ControlDemoComboBoxLabel");

	public static string Settings_ControlDemoListBoxLabel => Get("Settings_ControlDemoListBoxLabel");

	public static string Settings_ControlDemoProgressLabel => Get("Settings_ControlDemoProgressLabel");

	public static string Settings_ControlDemoStatusLabel => Get("Settings_ControlDemoStatusLabel");

	public static string Settings_ControlDemoDefaultInput => Get("Settings_ControlDemoDefaultInput");

	public static string Settings_ControlDemoDefaultMultilineInput => Get("Settings_ControlDemoDefaultMultilineInput");

	public static string Settings_ControlDemoStatusReady => Get("Settings_ControlDemoStatusReady");

	public static string Settings_ControlDemoStatusClicked => Get("Settings_ControlDemoStatusClicked");

	public static string Settings_ControlCategoryNavigation => Get("Settings_ControlCategoryNavigation");

	public static string Settings_ControlCategoryButton => Get("Settings_ControlCategoryButton");

	public static string Settings_ControlCategoryToggle => Get("Settings_ControlCategoryToggle");

	public static string Settings_ControlCategorySelection => Get("Settings_ControlCategorySelection");

	public static string Settings_ControlCategoryInput => Get("Settings_ControlCategoryInput");

	public static string Settings_ControlSecondaryMenuButton => Get("Settings_ControlSecondaryMenuButton");

	public static string Settings_ControlListPageItemButton => Get("Settings_ControlListPageItemButton");

	public static string Settings_ControlDialogButton => Get("Settings_ControlDialogButton");

	public static string Settings_ControlPrimaryButton => Get("Settings_ControlPrimaryButton");

	public static string Settings_ControlDangerButton => Get("Settings_ControlDangerButton");

	public static string Settings_ControlInlineIconButton => Get("Settings_ControlInlineIconButton");

	public static string Settings_ControlSwitch => Get("Settings_ControlSwitch");

	public static string Settings_ControlSlider => Get("Settings_ControlSlider");

	public static string Settings_ControlComboBox => Get("Settings_ControlComboBox");

	public static string Settings_ControlTextBox => Get("Settings_ControlTextBox");

	public static string Settings_ControlMultilineTextBox => Get("Settings_ControlMultilineTextBox");

	public static string Settings_ControlSearchBox => Get("Settings_ControlSearchBox");

	public static string Settings_ControlChoiceList => Get("Settings_ControlChoiceList");

	public static string Settings_ControlVirtualizedList => Get("Settings_ControlVirtualizedList");

	public static string GameSettings_AllCategory => Get("GameSettings_AllCategory");

	public static string GameSettings_ModLoaderCategory => Get("GameSettings_ModLoaderCategory");

	public static string GameSettings_NoInstances => Get("GameSettings_NoInstances");

	public static string GameSettings_NoMatchingInstances => Get("GameSettings_NoMatchingInstances");

	public static string GameSettings_NoCategoryInstancesFormat => Get("GameSettings_NoCategoryInstancesFormat");

	public static string GameSettings_UnknownMinecraftVersion => Get("GameSettings_UnknownMinecraftVersion");

	public static string GameSettings_DetailGeneral => Get("GameSettings_DetailGeneral");

	public static string GameSettings_DetailLaunch => Get("GameSettings_DetailLaunch");

	public static string GameSettings_DetailJava => Get("GameSettings_DetailJava");

	public static string GameSettings_DetailModManagement => Get("GameSettings_DetailModManagement");

	public static string GameSettings_DetailSaves => Get("GameSettings_DetailSaves");

	public static string GameSettings_DetailResourcePacks => Get("GameSettings_DetailResourcePacks");

	public static string GameSettings_DetailShaders => Get("GameSettings_DetailShaders");

	public static string GameSettings_DetailLoader => Get("GameSettings_DetailLoader");

	public static string GameSettings_DetailAdvanced => Get("GameSettings_DetailAdvanced");

	public static string GameSettings_DetailBackup => Get("GameSettings_DetailBackup");

	public static string GameSettings_DetailExport => Get("GameSettings_DetailExport");

	public static string GameSettings_DetailsPlaceholderHeader => Get("GameSettings_DetailsPlaceholderHeader");

	public static string GameSettings_DetailsPlaceholderBody => Get("GameSettings_DetailsPlaceholderBody");

	public static string GameSettings_DetailPlaceholderBodyFormat => Get("GameSettings_DetailPlaceholderBodyFormat");

	public static string GameSettings_GeneralDescriptionSection => Get("GameSettings_GeneralDescriptionSection");

	public static string GameSettings_GeneralSaveDescriptionButton => Get("GameSettings_GeneralSaveDescriptionButton");

	public static string GameSettings_GeneralInstanceDirectorySection => Get("GameSettings_GeneralInstanceDirectorySection");

	public static string GameSettings_GeneralOpenInstanceDirectoryButton => Get("GameSettings_GeneralOpenInstanceDirectoryButton");

	public static string GameSettings_GeneralCreatedAtSection => Get("GameSettings_GeneralCreatedAtSection");

	public static string GameSettings_DeleteGameButton => Get("GameSettings_DeleteGameButton");

	public static string GameSettings_BackupInfoSection => Get("GameSettings_BackupInfoSection");

	public static string GameSettings_BackupListSection => Get("GameSettings_BackupListSection");

	public static string GameSettings_ExportSettingsSection => Get("GameSettings_ExportSettingsSection");

	public static string GameSettings_ExportModpackTypeLabel => Get("GameSettings_ExportModpackTypeLabel");

	public static string GameSettings_ExportTypeCurseForge => Get("GameSettings_ExportTypeCurseForge");

	public static string GameSettings_ExportTypeModrinth => Get("GameSettings_ExportTypeModrinth");

	public static string GameSettings_ExportModpackInfoSection => Get("GameSettings_ExportModpackInfoSection");

	public static string GameSettings_ExportModpackNameLabel => Get("GameSettings_ExportModpackNameLabel");

	public static string GameSettings_ExportModpackNameRequired => Get("GameSettings_ExportModpackNameRequired");

	public static string GameSettings_ExportAuthorLabel => Get("GameSettings_ExportAuthorLabel");

	public static string GameSettings_ExportAuthorHint => Get("GameSettings_ExportAuthorHint");

	public static string GameSettings_ExportVersionLabel => Get("GameSettings_ExportVersionLabel");

	public static string GameSettings_ExportVersionPlaceholder => Get("GameSettings_ExportVersionPlaceholder");

	public static string GameSettings_ExportPackModsLabel => Get("GameSettings_ExportPackModsLabel");

	public static string GameSettings_ExportPackDisabledModsLabel => Get("GameSettings_ExportPackDisabledModsLabel");

	public static string GameSettings_ExportPackResourcePacksLabel => Get("GameSettings_ExportPackResourcePacksLabel");

	public static string GameSettings_ExportPackShaderPacksLabel => Get("GameSettings_ExportPackShaderPacksLabel");

	public static string GameSettings_ExportPackSavesLabel => Get("GameSettings_ExportPackSavesLabel");

	public static string GameSettings_BackupInfoSummaryFormat => Get("GameSettings_BackupInfoSummaryFormat");

	public static string GameSettings_BackupDirectorySection => Get("GameSettings_BackupDirectorySection");

	public static string GameSettings_BackupDirectoryNotSelected => Get("GameSettings_BackupDirectoryNotSelected");

	public static string GameSettings_BackupActionsLabel => Get("GameSettings_BackupActionsLabel");

	public static string GameSettings_BackupOpenFolderButton => Get("GameSettings_BackupOpenFolderButton");

	public static string GameSettings_BackupChangeDirectoryButton => Get("GameSettings_BackupChangeDirectoryButton");

	public static string GameSettings_BackupCreateNowButton => Get("GameSettings_BackupCreateNowButton");

	public static string GameSettings_BackupMultiSelectButton => Get("GameSettings_BackupMultiSelectButton");

	public static string GameSettings_BackupCancelMultiSelectButton => Get("GameSettings_BackupCancelMultiSelectButton");

	public static string GameSettings_BackupSelectAllButton => Get("GameSettings_BackupSelectAllButton");

	public static string GameSettings_BackupCancelSelectAllButton => Get("GameSettings_BackupCancelSelectAllButton");

	public static string GameSettings_BackupDeleteButton => Get("GameSettings_BackupDeleteButton");

	public static string GameSettings_BackupDefaultNameFormat => Get("GameSettings_BackupDefaultNameFormat");

	public static string GameSettings_BackupItemSubtitleFormat => Get("GameSettings_BackupItemSubtitleFormat");

	public static string GameSettings_BackupLoading => Get("GameSettings_BackupLoading");

	public static string GameSettings_BackupEmpty => Get("GameSettings_BackupEmpty");

	public static string GameSettings_BackupSearchEmpty => Get("GameSettings_BackupSearchEmpty");

	public static string GameSettings_BackupOpenLocationTooltip => Get("GameSettings_BackupOpenLocationTooltip");

	public static string GameSettings_BackupRestoreTooltip => Get("GameSettings_BackupRestoreTooltip");

	public static string GameSettings_BackupRestoreUnavailableTooltip => Get("GameSettings_BackupRestoreUnavailableTooltip");

	public static string GameSettings_BackupPreRestoreNameFormat => Get("GameSettings_BackupPreRestoreNameFormat");

	public static string GameSettings_ModManagementInfoSection => Get("GameSettings_ModManagementInfoSection");

	public static string GameSettings_ModManagementInstalledSummaryFormat => Get("GameSettings_ModManagementInstalledSummaryFormat");

	public static string GameSettings_ModManagementActionsLabel => Get("GameSettings_ModManagementActionsLabel");

	public static string GameSettings_ModManagementFilterLabel => Get("GameSettings_ModManagementFilterLabel");

	public static string GameSettings_ModManagementFilterAllButton => Get("GameSettings_ModManagementFilterAllButton");

	public static string GameSettings_ModManagementFilterEnabledButton => Get("GameSettings_ModManagementFilterEnabledButton");

	public static string GameSettings_ModManagementFilterDisabledButton => Get("GameSettings_ModManagementFilterDisabledButton");

	public static string GameSettings_ModManagementOpenFolderButton => Get("GameSettings_ModManagementOpenFolderButton");

	public static string GameSettings_ModManagementLocalImportButton => Get("GameSettings_ModManagementLocalImportButton");

	public static string GameSettings_ModManagementOnlineInstallButton => Get("GameSettings_ModManagementOnlineInstallButton");

	public static string GameSettings_ModManagementMultiSelectButton => Get("GameSettings_ModManagementMultiSelectButton");

	public static string GameSettings_ModManagementCancelMultiSelectButton => Get("GameSettings_ModManagementCancelMultiSelectButton");

	public static string GameSettings_ModManagementSelectAllButton => Get("GameSettings_ModManagementSelectAllButton");

	public static string GameSettings_ModManagementCancelSelectAllButton => Get("GameSettings_ModManagementCancelSelectAllButton");

	public static string GameSettings_ModManagementEnableButton => Get("GameSettings_ModManagementEnableButton");

	public static string GameSettings_ModManagementDisableButton => Get("GameSettings_ModManagementDisableButton");

	public static string GameSettings_ModManagementDeleteButton => Get("GameSettings_ModManagementDeleteButton");

	public static string GameSettings_ModManagementEnableAllButton => Get("GameSettings_ModManagementEnableAllButton");

	public static string GameSettings_ModManagementDisableAllButton => Get("GameSettings_ModManagementDisableAllButton");

	public static string GameSettings_ModManagementModListSection => Get("GameSettings_ModManagementModListSection");

	public static string GameSettings_ModManagementEmptyMessage => Get("GameSettings_ModManagementEmptyMessage");

	public static string GameSettings_ModManagementLoading => Get("GameSettings_ModManagementLoading");

	public static string GameSettings_ModManagementSearchEmptyMessage => Get("GameSettings_ModManagementSearchEmptyMessage");

	public static string GameSettings_ModManagementUnavailableMessage => Get("GameSettings_ModManagementUnavailableMessage");

	public static string GameSettings_ModManagementEnabledState => Get("GameSettings_ModManagementEnabledState");

	public static string GameSettings_ModManagementDisabledState => Get("GameSettings_ModManagementDisabledState");

	public static string GameSettings_SaveManagementInfoSection => Get("GameSettings_SaveManagementInfoSection");

	public static string GameSettings_SaveManagementInstalledSummaryFormat => Get("GameSettings_SaveManagementInstalledSummaryFormat");

	public static string GameSettings_SaveManagementActionsLabel => Get("GameSettings_SaveManagementActionsLabel");

	public static string GameSettings_SaveManagementOpenFolderButton => Get("GameSettings_SaveManagementOpenFolderButton");

	public static string GameSettings_SaveManagementLocalImportButton => Get("GameSettings_SaveManagementLocalImportButton");

	public static string GameSettings_SaveManagementMultiSelectButton => Get("GameSettings_SaveManagementMultiSelectButton");

	public static string GameSettings_SaveManagementCancelMultiSelectButton => Get("GameSettings_SaveManagementCancelMultiSelectButton");

	public static string GameSettings_SaveManagementSelectAllButton => Get("GameSettings_SaveManagementSelectAllButton");

	public static string GameSettings_SaveManagementCancelSelectAllButton => Get("GameSettings_SaveManagementCancelSelectAllButton");

	public static string GameSettings_SaveManagementDeleteButton => Get("GameSettings_SaveManagementDeleteButton");

	public static string GameSettings_SaveManagementSaveListSection => Get("GameSettings_SaveManagementSaveListSection");

	public static string GameSettings_SaveManagementEmptyMessage => Get("GameSettings_SaveManagementEmptyMessage");

	public static string GameSettings_SaveManagementLoading => Get("GameSettings_SaveManagementLoading");

	public static string GameSettings_SaveManagementSearchEmptyMessage => Get("GameSettings_SaveManagementSearchEmptyMessage");

	public static string GameSettings_ResourcePackManagementInfoSection => Get("GameSettings_ResourcePackManagementInfoSection");

	public static string GameSettings_ResourcePackManagementInstalledSummaryFormat => Get("GameSettings_ResourcePackManagementInstalledSummaryFormat");

	public static string GameSettings_ResourcePackManagementActionsLabel => Get("GameSettings_ResourcePackManagementActionsLabel");

	public static string GameSettings_ResourcePackManagementOpenFolderButton => Get("GameSettings_ResourcePackManagementOpenFolderButton");

	public static string GameSettings_ResourcePackManagementLocalImportButton => Get("GameSettings_ResourcePackManagementLocalImportButton");

	public static string GameSettings_ResourcePackManagementMultiSelectButton => Get("GameSettings_ResourcePackManagementMultiSelectButton");

	public static string GameSettings_ResourcePackManagementCancelMultiSelectButton => Get("GameSettings_ResourcePackManagementCancelMultiSelectButton");

	public static string GameSettings_ResourcePackManagementSelectAllButton => Get("GameSettings_ResourcePackManagementSelectAllButton");

	public static string GameSettings_ResourcePackManagementCancelSelectAllButton => Get("GameSettings_ResourcePackManagementCancelSelectAllButton");

	public static string GameSettings_ResourcePackManagementDeleteButton => Get("GameSettings_ResourcePackManagementDeleteButton");

	public static string GameSettings_ResourcePackManagementListSection => Get("GameSettings_ResourcePackManagementListSection");

	public static string GameSettings_ResourcePackManagementEmptyMessage => Get("GameSettings_ResourcePackManagementEmptyMessage");

	public static string GameSettings_ResourcePackManagementLoading => Get("GameSettings_ResourcePackManagementLoading");

	public static string GameSettings_ResourcePackManagementSearchEmptyMessage => Get("GameSettings_ResourcePackManagementSearchEmptyMessage");

	public static string GameSettings_ShaderPackManagementInfoSection => Get("GameSettings_ShaderPackManagementInfoSection");

	public static string GameSettings_ShaderPackManagementInstalledSummaryFormat => Get("GameSettings_ShaderPackManagementInstalledSummaryFormat");

	public static string GameSettings_ShaderPackManagementActionsLabel => Get("GameSettings_ShaderPackManagementActionsLabel");

	public static string GameSettings_ShaderPackManagementOpenFolderButton => Get("GameSettings_ShaderPackManagementOpenFolderButton");

	public static string GameSettings_ShaderPackManagementLocalImportButton => Get("GameSettings_ShaderPackManagementLocalImportButton");

	public static string GameSettings_ShaderPackManagementMultiSelectButton => Get("GameSettings_ShaderPackManagementMultiSelectButton");

	public static string GameSettings_ShaderPackManagementCancelMultiSelectButton => Get("GameSettings_ShaderPackManagementCancelMultiSelectButton");

	public static string GameSettings_ShaderPackManagementSelectAllButton => Get("GameSettings_ShaderPackManagementSelectAllButton");

	public static string GameSettings_ShaderPackManagementCancelSelectAllButton => Get("GameSettings_ShaderPackManagementCancelSelectAllButton");

	public static string GameSettings_ShaderPackManagementDeleteButton => Get("GameSettings_ShaderPackManagementDeleteButton");

	public static string GameSettings_ShaderPackManagementListSection => Get("GameSettings_ShaderPackManagementListSection");

	public static string GameSettings_ShaderPackManagementEmptyMessage => Get("GameSettings_ShaderPackManagementEmptyMessage");

	public static string GameSettings_ShaderPackManagementLoading => Get("GameSettings_ShaderPackManagementLoading");

	public static string GameSettings_ShaderPackManagementSearchEmptyMessage => Get("GameSettings_ShaderPackManagementSearchEmptyMessage");

	public static string GameSettings_DropImportModsMessage => Get("GameSettings_DropImportModsMessage");

	public static string GameSettings_DropImportSavesMessage => Get("GameSettings_DropImportSavesMessage");

	public static string GameSettings_DropImportResourcePacksMessage => Get("GameSettings_DropImportResourcePacksMessage");

	public static string GameSettings_DropImportShaderPacksMessage => Get("GameSettings_DropImportShaderPacksMessage");

	public static string GameSettings_DropReleaseToImportMessage => Get("GameSettings_DropReleaseToImportMessage");

	public static string GameSettings_DropUnsupportedFileMessage => Get("GameSettings_DropUnsupportedFileMessage");

	public static string GameSettings_DropModsOnlyMessage => Get("GameSettings_DropModsOnlyMessage");

	public static string GameSettings_DropSaveArchivesOnlyMessage => Get("GameSettings_DropSaveArchivesOnlyMessage");

	public static string GameSettings_DropResourcePackArchivesOnlyMessage => Get("GameSettings_DropResourcePackArchivesOnlyMessage");

	public static string GameSettings_DropShaderPackArchivesOnlyMessage => Get("GameSettings_DropShaderPackArchivesOnlyMessage");

	public static string GameSettings_DropFoldersUnsupportedMessage => Get("GameSettings_DropFoldersUnsupportedMessage");

	public static string GameSettings_LaunchSettingsSection => Get("GameSettings_LaunchSettingsSection");

	public static string GameSettings_LaunchSettingsModeLabel => Get("GameSettings_LaunchSettingsModeLabel");

	public static string GameSettings_LaunchSettingsModeUseGlobal => Get("GameSettings_LaunchSettingsModeUseGlobal");

	public static string GameSettings_LaunchSettingsModePerInstance => Get("GameSettings_LaunchSettingsModePerInstance");

	public static string GameSettings_LaunchCheckFilesOption => Get("GameSettings_LaunchCheckFilesOption");

	public static string GameSettings_LaunchAutoRepairOption => Get("GameSettings_LaunchAutoRepairOption");

	public static string GameSettings_LaunchMinimizeLauncherOption => Get("GameSettings_LaunchMinimizeLauncherOption");

	public static string GameSettings_LaunchFullScreenOption => Get("GameSettings_LaunchFullScreenOption");

	public static string GameSettings_LaunchAutoJoinServerOption => Get("GameSettings_LaunchAutoJoinServerOption");

	public static string GameSettings_AdvancedLaunchSettingsSection => Get("GameSettings_AdvancedLaunchSettingsSection");

	public static string GameSettings_PreLaunchCommandOption => Get("GameSettings_PreLaunchCommandOption");

	public static string GameSettings_WaitForPreLaunchCommandOption => Get("GameSettings_WaitForPreLaunchCommandOption");

	public static string GameSettings_PostExitCommandOption => Get("GameSettings_PostExitCommandOption");

	public static string GameSettings_CustomJvmArgumentsOption => Get("GameSettings_CustomJvmArgumentsOption");

	public static string GameSettings_CustomGameArgumentsOption => Get("GameSettings_CustomGameArgumentsOption");

	public static string GameSettings_InstanceNameLabel => Get("GameSettings_InstanceNameLabel");

	public static string GameSettings_InstanceIconLabel => Get("GameSettings_InstanceIconLabel");

	public static string GameSettings_InstanceNameValidation => Get("GameSettings_InstanceNameValidation");

	public static string GameSettings_IconGrassBlock => Get("GameSettings_IconGrassBlock");

	public static string GameSettings_IconDirtBlock => Get("GameSettings_IconDirtBlock");

	public static string GameSettings_IconCraftingTable => Get("GameSettings_IconCraftingTable");

	public static string GameSettings_IconStoneBlock => Get("GameSettings_IconStoneBlock");

	public static string GameSettings_IconFabric => Get("GameSettings_IconFabric");

	public static string GameSettings_IconAnvil => Get("GameSettings_IconAnvil");

	public static string GameSettings_IconNeoForge => Get("GameSettings_IconNeoForge");

	public static string GameSettings_IconQuilt => Get("GameSettings_IconQuilt");

	public static string GameSettings_IconDiamondBlock => Get("GameSettings_IconDiamondBlock");

	public static string GameSettings_IconBeacon => Get("GameSettings_IconBeacon");

	public static string GameSettings_IconFurnace => Get("GameSettings_IconFurnace");

	public static string GameSettings_IconTnt => Get("GameSettings_IconTnt");

	public static string GameSettings_InstanceSubtitleFormat => Get("GameSettings_InstanceSubtitleFormat");

	public static string GameSettings_InstanceSubtitleVanillaFormat => Get("GameSettings_InstanceSubtitleVanillaFormat");

	public static string GameSettings_InstanceSubtitleLoaderWithoutVersionFormat => Get("GameSettings_InstanceSubtitleLoaderWithoutVersionFormat");

	public static string GameSettings_InstanceSubtitleLoaderFormat => Get("GameSettings_InstanceSubtitleLoaderFormat");

	public static string GameSettings_DeleteInstanceTooltip => Get("GameSettings_DeleteInstanceTooltip");

	public static string GameSettings_OpenInstanceFolderTooltip => Get("GameSettings_OpenInstanceFolderTooltip");

	public static string GameSettings_OpenResourceDetailsTooltip => Get("GameSettings_OpenResourceDetailsTooltip");

	public static string GameSettings_SelectInstanceAndGoHomeTooltip => Get("GameSettings_SelectInstanceAndGoHomeTooltip");

	public static string Nav_GameInstanceList => Get("Nav_GameInstanceList");

	public static string GameSettings_SwitchMinecraftDirectoryButton => Get("GameSettings_SwitchMinecraftDirectoryButton");

	public static string Dialog_SwitchMinecraftDirectoryTitle => Get("Dialog_SwitchMinecraftDirectoryTitle");

	public static string Nav_JavaMemory => Get("Nav_JavaMemory");

	public static string Nav_DirectoryManagement => Get("Nav_DirectoryManagement");

	public static string Nav_Mod => Get("Nav_Mod");

	public static string Nav_ResourcePacks => Get("Nav_ResourcePacks");

	public static string Nav_ShaderPacks => Get("Nav_ShaderPacks");

	public static string Nav_Worlds => Get("Nav_Worlds");

	public static string Nav_Modpacks => Get("Nav_Modpacks");

	public static string Nav_Shaders => Get("Nav_Shaders");

	public static string Nav_Maps => Get("Nav_Maps");

	public static string Nav_AppearanceTheme => Get("Nav_AppearanceTheme");

	public static string Nav_DefaultSettings => Get("Nav_DefaultSettings");

	public static string Nav_About => Get("Nav_About");

	public static string Home_ChooseOrCreateAccount => Get("Home_ChooseOrCreateAccount");

	public static string Home_GameSettingsButton => Get("Home_GameSettingsButton");

	public static string Home_NoAccountSelected => Get("Home_NoAccountSelected");

	public static string Home_NoVersionSelected => Get("Home_NoVersionSelected");

	public static string Home_LaunchInstanceMenuTitle => Get("Home_LaunchInstanceMenuTitle");

	public static string Home_PinLaunchMenuTooltip => Get("Home_PinLaunchMenuTooltip");

	public static string Home_UnpinLaunchMenuTooltip => Get("Home_UnpinLaunchMenuTooltip");

	public static string Home_LaunchInstanceSubtitleFormat => Get("Home_LaunchInstanceSubtitleFormat");

	public static string Home_NoLaunchInstances => Get("Home_NoLaunchInstances");

	public static string Launch_Button => Get("Launch_Button");

	public static string Account_ListTitle => Get("Account_ListTitle");

	public static string Account_AddButton => Get("Account_AddButton");

	public static string Account_BuyMinecraftButton => Get("Account_BuyMinecraftButton");

	public static string Account_EmptySelection => Get("Account_EmptySelection");

	public static string Account_SkinHeader => Get("Account_SkinHeader");

	public static string Account_CapeHeader => Get("Account_CapeHeader");

	public static string Account_ChangeSkinButton => Get("Account_ChangeSkinButton");

	public static string Account_AddSkinButton => Get("Account_AddSkinButton");

	public static string Account_ManageSkinButton => Get("Account_ManageSkinButton");

	public static string Account_ChangeSkinModelButton => Get("Account_ChangeSkinModelButton");

	public static string Account_SkinActiveState => Get("Account_SkinActiveState");

	public static string Account_SkinPreviewEmpty => Get("Account_SkinPreviewEmpty");

	public static string Account_AppearanceOfflineUnsupported => Get("Account_AppearanceOfflineUnsupported");

	public static string Account_SkinModelClassicTitle => Get("Account_SkinModelClassicTitle");

	public static string Account_SkinModelClassicDescription => Get("Account_SkinModelClassicDescription");

	public static string Account_SkinModelSlimTitle => Get("Account_SkinModelSlimTitle");

	public static string Account_SkinModelSlimDescription => Get("Account_SkinModelSlimDescription");

	public static string Account_UuidHeader => Get("Account_UuidHeader");

	public static string Account_OfflineUuidModeLabel => Get("Account_OfflineUuidModeLabel");

	public static string Dialog_OfflineUuidModeChangeTitle => Get("Dialog_OfflineUuidModeChangeTitle");

	public static string Dialog_OfflineUuidModeChangeMessageFormat => Get("Dialog_OfflineUuidModeChangeMessageFormat");

	public static string Account_OfflineUuidModeChangeButton => Get("Account_OfflineUuidModeChangeButton");

	public static string Account_OfflineUuidStandardTitle => Get("Account_OfflineUuidStandardTitle");

	public static string Account_OfflineUuidStandardDescription => Get("Account_OfflineUuidStandardDescription");

	public static string Account_OfflineUuidManualTitle => Get("Account_OfflineUuidManualTitle");

	public static string Account_OfflineUuidManualDescription => Get("Account_OfflineUuidManualDescription");

	public static string Account_OfflineUuidInvalid => Get("Account_OfflineUuidInvalid");

	public static string Account_NoneValue => Get("Account_NoneValue");

	public static string Account_UsernameLabel => Get("Account_UsernameLabel");

	public static string Account_NewUsernameLabel => Get("Account_NewUsernameLabel");

	public static string Account_UsernameValidation => Get("Account_UsernameValidation");

	public static string Account_TypeOfflineTitle => Get("Account_TypeOfflineTitle");

	public static string Account_TypeOfflineDescription => Get("Account_TypeOfflineDescription");

	public static string Account_TypeSteamTitle => Get("Account_TypeSteamTitle");

	public static string Account_TypeSteamDescription => Get("Account_TypeSteamDescription");

	public static string Status_SteamDetecting => Get("Status_SteamDetecting");

	public static string Status_SteamNotDetected => Get("Status_SteamNotDetected");

	public static string Status_SteamAccountAddedFormat => Get("Status_SteamAccountAddedFormat");

	public static string Status_SteamNoBeamng => Get("Status_SteamNoBeamng");

	public static string Status_SteamBeamngFoundFormat => Get("Status_SteamBeamngFoundFormat");

	public static string Account_TypeMicrosoftTitle => Get("Account_TypeMicrosoftTitle");

	public static string Account_TypeMicrosoftDescription => Get("Account_TypeMicrosoftDescription");

	public static string Account_TypeThirdPartyTitle => Get("Account_TypeThirdPartyTitle");

	public static string Account_TypeThirdPartyDescription => Get("Account_TypeThirdPartyDescription");

	public static string Account_AuthenticationServerLabel => Get("Account_AuthenticationServerLabel");

	public static string Account_AuthenticationServerPlaceholder => Get("Account_AuthenticationServerPlaceholder");

	public static string Account_UsernameOrEmailLabel => Get("Account_UsernameOrEmailLabel");

	public static string Account_PasswordLabel => Get("Account_PasswordLabel");

	public static string Account_ThirdPartyServerRequired => Get("Account_ThirdPartyServerRequired");

	public static string Account_ThirdPartyUsernameRequired => Get("Account_ThirdPartyUsernameRequired");

	public static string Account_ThirdPartyPasswordRequired => Get("Account_ThirdPartyPasswordRequired");

	public static string Account_ThirdPartyServerInvalid => Get("Account_ThirdPartyServerInvalid");

	public static string Account_ThirdPartyServerHttpsRequired => Get("Account_ThirdPartyServerHttpsRequired");

	public static string Account_ThirdPartyServerUnavailable => Get("Account_ThirdPartyServerUnavailable");

	public static string Account_ThirdPartyServerInvalidResponse => Get("Account_ThirdPartyServerInvalidResponse");

	public static string Account_ThirdPartyUsernameUnsupported => Get("Account_ThirdPartyUsernameUnsupported");

	public static string Account_ThirdPartyProfileMissing => Get("Account_ThirdPartyProfileMissing");

	public static string Account_ThirdPartyInvalidCredentials => Get("Account_ThirdPartyInvalidCredentials");

	public static string Account_ThirdPartyCredentialStorageFailed => Get("Account_ThirdPartyCredentialStorageFailed");

	public static string Account_ThirdPartyLoginFailed => Get("Account_ThirdPartyLoginFailed");

	public static string Account_ThirdPartyDropReleaseToAdd => Get("Account_ThirdPartyDropReleaseToAdd");

	public static string Account_ThirdPartyDropInvalidServer => Get("Account_ThirdPartyDropInvalidServer");

	public static string Account_ThirdPartyDropDialogBusy => Get("Account_ThirdPartyDropDialogBusy");

	public static string Account_ProfileOfflineUnsupported => Get("Account_ProfileOfflineUnsupported");

	public static string Account_ProfileNoCapes => Get("Account_ProfileNoCapes");

	public static string Account_ProfileLoaded => Get("Account_ProfileLoaded");

	public static string Account_ProfileCacheLoaded => Get("Account_ProfileCacheLoaded");

	public static string Account_ProfileRefreshHint => Get("Account_ProfileRefreshHint");

	public static string Cape_NoneState => Get("Cape_NoneState");

	public static string Cape_ActiveState => Get("Cape_ActiveState");

	public static string Cape_AvailableState => Get("Cape_AvailableState");

	public static string Cape_UnnamedDisplayName => Get("Cape_UnnamedDisplayName");

	public static string Dialog_AddAccountTitle => Get("Dialog_AddAccountTitle");

	public static string Dialog_AddOfflineAccountTitle => Get("Dialog_AddOfflineAccountTitle");

	public static string Dialog_AddMicrosoftAccountTitle => Get("Dialog_AddMicrosoftAccountTitle");

	public static string Dialog_AddThirdPartyAccountTitle => Get("Dialog_AddThirdPartyAccountTitle");

	public static string Dialog_ReauthenticateThirdPartyAccountTitle => Get("Dialog_ReauthenticateThirdPartyAccountTitle");

	public static string Dialog_ReauthenticateMicrosoftAccountTitle => Get("Dialog_ReauthenticateMicrosoftAccountTitle");

	public static string Dialog_ReauthenticateMicrosoftAccountSubtitle => Get("Dialog_ReauthenticateMicrosoftAccountSubtitle");

	public static string Dialog_MicrosoftAccountExpiredTitle => Get("Dialog_MicrosoftAccountExpiredTitle");

	public static string Dialog_MicrosoftAccountExpiredSubtitle => Get("Dialog_MicrosoftAccountExpiredSubtitle");

	public static string Dialog_MicrosoftAccountExpiredMessageFormat => Get("Dialog_MicrosoftAccountExpiredMessageFormat");

	public static string Dialog_AddAccountAlreadyExistsTitle => Get("Dialog_AddAccountAlreadyExistsTitle");

	public static string Dialog_LoginSuccessTitle => Get("Dialog_LoginSuccessTitle");

	public static string Dialog_LoginIncompleteTitle => Get("Dialog_LoginIncompleteTitle");

	public static string Dialog_AddAccountSubtitle => Get("Dialog_AddAccountSubtitle");

	public static string Dialog_AddOfflineAccountSubtitle => Get("Dialog_AddOfflineAccountSubtitle");

	public static string Dialog_AddMicrosoftAccountSubtitle => Get("Dialog_AddMicrosoftAccountSubtitle");

	public static string Dialog_AddThirdPartyAccountSubtitle => Get("Dialog_AddThirdPartyAccountSubtitle");

	public static string Dialog_ReauthenticateThirdPartyAccountSubtitle => Get("Dialog_ReauthenticateThirdPartyAccountSubtitle");

	public static string Dialog_AddAccountResultSubtitle => Get("Dialog_AddAccountResultSubtitle");

	public static string Dialog_ModpackManualDownloadsTitle => Get("Dialog_ModpackManualDownloadsTitle");

	public static string Dialog_ModpackManualDownloadsMessageFormat => Get("Dialog_ModpackManualDownloadsMessageFormat");

	public static string Dialog_ModpackManualDownloadsHint => Get("Dialog_ModpackManualDownloadsHint");

	public static string Dialog_OpenFileButton => Get("Dialog_OpenFileButton");

	public static string Dialog_DeleteInstanceTitle => Get("Dialog_DeleteInstanceTitle");

	public static string Dialog_DeleteInstanceMessageFormat => Get("Dialog_DeleteInstanceMessageFormat");

	public static string Dialog_DeleteInstanceBusyTitle => Get("Dialog_DeleteInstanceBusyTitle");

	public static string Dialog_DeleteInstanceBusyMessageFormat => Get("Dialog_DeleteInstanceBusyMessageFormat");

	public static string Dialog_DeleteInstanceFailedTitle => Get("Dialog_DeleteInstanceFailedTitle");

	public static string Dialog_DeleteModsTitle => Get("Dialog_DeleteModsTitle");

	public static string Dialog_DeleteSingleModMessageFormat => Get("Dialog_DeleteSingleModMessageFormat");

	public static string Dialog_DeleteMultipleModsMessageFormat => Get("Dialog_DeleteMultipleModsMessageFormat");

	public static string Dialog_DeleteSavesTitle => Get("Dialog_DeleteSavesTitle");

	public static string Dialog_DeleteSingleSaveMessageFormat => Get("Dialog_DeleteSingleSaveMessageFormat");

	public static string Dialog_DeleteMultipleSavesMessageFormat => Get("Dialog_DeleteMultipleSavesMessageFormat");

	public static string Dialog_DeleteResourcePacksTitle => Get("Dialog_DeleteResourcePacksTitle");

	public static string Dialog_DeleteSingleResourcePackMessageFormat => Get("Dialog_DeleteSingleResourcePackMessageFormat");

	public static string Dialog_DeleteMultipleResourcePacksMessageFormat => Get("Dialog_DeleteMultipleResourcePacksMessageFormat");

	public static string Dialog_DeleteShaderPacksTitle => Get("Dialog_DeleteShaderPacksTitle");

	public static string Dialog_DeleteSingleShaderPackMessageFormat => Get("Dialog_DeleteSingleShaderPackMessageFormat");

	public static string Dialog_DeleteMultipleShaderPacksMessageFormat => Get("Dialog_DeleteMultipleShaderPacksMessageFormat");

	public static string Dialog_InvalidSaveImportTitle => Get("Dialog_InvalidSaveImportTitle");

	public static string Dialog_InvalidSaveArchiveMessage => Get("Dialog_InvalidSaveArchiveMessage");

	public static string Dialog_UnsupportedSaveArchiveMessage => Get("Dialog_UnsupportedSaveArchiveMessage");

	public static string Dialog_InvalidResourcePackImportTitle => Get("Dialog_InvalidResourcePackImportTitle");

	public static string Dialog_UnsupportedResourcePackArchiveMessage => Get("Dialog_UnsupportedResourcePackArchiveMessage");

	public static string Dialog_InvalidShaderPackImportTitle => Get("Dialog_InvalidShaderPackImportTitle");

	public static string Dialog_UnsupportedShaderPackArchiveMessage => Get("Dialog_UnsupportedShaderPackArchiveMessage");

	public static string Dialog_ReplaceModImportTitle => Get("Dialog_ReplaceModImportTitle");

	public static string Dialog_ReplaceModImportMessageFormat => Get("Dialog_ReplaceModImportMessageFormat");

	public static string Dialog_Replace_Button => Get("Dialog_Replace_Button");

	public static string Dialog_RenameInstanceTitle => Get("Dialog_RenameInstanceTitle");

	public static string Dialog_RenameInstanceSubtitle => Get("Dialog_RenameInstanceSubtitle");

	public static string Dialog_RenameInstanceBusyTitle => Get("Dialog_RenameInstanceBusyTitle");

	public static string Dialog_RenameInstanceBusySubtitle => Get("Dialog_RenameInstanceBusySubtitle");

	public static string Dialog_RenameInstanceSuccessTitle => Get("Dialog_RenameInstanceSuccessTitle");

	public static string Dialog_RenameInstanceFailedTitle => Get("Dialog_RenameInstanceFailedTitle");

	public static string Dialog_RenameInstanceResultSubtitle => Get("Dialog_RenameInstanceResultSubtitle");

	public static string Dialog_CreateBackupTitle => Get("Dialog_CreateBackupTitle");

	public static string Dialog_CreateBackupSubtitle => Get("Dialog_CreateBackupSubtitle");

	public static string Dialog_CreateBackupNameLabel => Get("Dialog_CreateBackupNameLabel");

	public static string Dialog_CreateBackupNameValidation => Get("Dialog_CreateBackupNameValidation");

	public static string Dialog_BackupCreateFailedTitle => Get("Dialog_BackupCreateFailedTitle");

	public static string Dialog_BackupCreateFailedMessageFormat => Get("Dialog_BackupCreateFailedMessageFormat");

	public static string Dialog_DeleteBackupTitle => Get("Dialog_DeleteBackupTitle");

	public static string Dialog_DeleteBackupMessageFormat => Get("Dialog_DeleteBackupMessageFormat");

	public static string Dialog_DeleteMultipleBackupsMessageFormat => Get("Dialog_DeleteMultipleBackupsMessageFormat");

	public static string Dialog_RestoreBackupTitle => Get("Dialog_RestoreBackupTitle");

	public static string Dialog_RestoreBackupMessageFormat => Get("Dialog_RestoreBackupMessageFormat");

	public static string BackupFailure_BackupDirectoryInsideInstance => Get("BackupFailure_BackupDirectoryInsideInstance");

	public static string BackupFailure_InstanceDirectoryMissing => Get("BackupFailure_InstanceDirectoryMissing");

	public static string BackupFailure_Generic => Get("BackupFailure_Generic");

	public static string Dialog_DeleteAccountTitle => Get("Dialog_DeleteAccountTitle");

	public static string Dialog_DeleteAccountMessage => Get("Dialog_DeleteAccountMessage");

	public static string Dialog_JavaRequirementNotMetTitle => Get("Dialog_JavaRequirementNotMetTitle");

	public static string Dialog_JavaRequirementNotMetMessage => Get("Dialog_JavaRequirementNotMetMessage");

	public static string Dialog_JavaRequirementNotMetMessageFormat => Get("Dialog_JavaRequirementNotMetMessageFormat");

	public static string Dialog_JavaRuntimeMissingTitle => Get("Dialog_JavaRuntimeMissingTitle");

	public static string Dialog_JavaRuntimeMissingMessage => Get("Dialog_JavaRuntimeMissingMessage");

	public static string Dialog_JavaRuntimeMissingMessageFormat => Get("Dialog_JavaRuntimeMissingMessageFormat");

	public static string Dialog_JavaManualVersionTooLowTitle => Get("Dialog_JavaManualVersionTooLowTitle");

	public static string Dialog_JavaManualVersionTooLowMessageFormat => Get("Dialog_JavaManualVersionTooLowMessageFormat");

	public static string Dialog_JavaManualVersionIncompatibleTitle => Get("Dialog_JavaManualVersionIncompatibleTitle");

	public static string Dialog_JavaManualVersionIncompatibleMessageFormat => Get("Dialog_JavaManualVersionIncompatibleMessageFormat");

	public static string Dialog_JavaCompatibilityNotMetMessageFormat => Get("Dialog_JavaCompatibilityNotMetMessageFormat");

	public static string Dialog_GoToSettingsButton => Get("Dialog_GoToSettingsButton");

	public static string Dialog_ForceLaunchButton => Get("Dialog_ForceLaunchButton");

	public static string Dialog_CancelLaunchButton => Get("Dialog_CancelLaunchButton");

	public static string Dialog_RenameAccountTitle => Get("Dialog_RenameAccountTitle");

	public static string Dialog_RenameAccountBusyTitle => Get("Dialog_RenameAccountBusyTitle");

	public static string Dialog_RenameAccountSuccessTitle => Get("Dialog_RenameAccountSuccessTitle");

	public static string Dialog_RenameAccountFailedTitle => Get("Dialog_RenameAccountFailedTitle");

	public static string Dialog_RenameAccountBusySubtitle => Get("Dialog_RenameAccountBusySubtitle");

	public static string Dialog_RenameAccountResultSubtitle => Get("Dialog_RenameAccountResultSubtitle");

	public static string Dialog_RenameMicrosoftAccountSubtitle => Get("Dialog_RenameMicrosoftAccountSubtitle");

	public static string Dialog_RenameOfflineAccountSubtitle => Get("Dialog_RenameOfflineAccountSubtitle");

	public static string Dialog_SkinModelTitle => Get("Dialog_SkinModelTitle");

	public static string Dialog_SkinModelSubtitle => Get("Dialog_SkinModelSubtitle");

	public static string Dialog_SkinManagerTitle => Get("Dialog_SkinManagerTitle");

	public static string Dialog_SkinManagerEmpty => Get("Dialog_SkinManagerEmpty");

	public static string Dialog_SkinFormatErrorTitle => Get("Dialog_SkinFormatErrorTitle");

	public static string Dialog_SkinFormatErrorSubtitle => Get("Dialog_SkinFormatErrorSubtitle");

	public static string Download_VersionListTitle => Get("Download_VersionListTitle");

	public static string Download_LoadingVersions => Get("Download_LoadingVersions");

	public static string Download_InstanceNameLabel => Get("Download_InstanceNameLabel");

	public static string Download_LoaderLabel => Get("Download_LoaderLabel");

	public static string Download_InstallButton => Get("Download_InstallButton");

	public static string Download_ReleaseCategory => Get("Download_ReleaseCategory");

	public static string Download_SnapshotCategory => Get("Download_SnapshotCategory");

	public static string Download_AprilFoolsCategory => Get("Download_AprilFoolsCategory");

	public static string Download_AncientCategory => Get("Download_AncientCategory");

	public static string Download_BetaCategory => Get("Download_BetaCategory");

	public static string Download_AlphaCategory => Get("Download_AlphaCategory");

	public static string Download_LocalImportCategory => Get("Download_LocalImportCategory");

	public static string Download_LocalImportDialogTitle => Get("Download_LocalImportDialogTitle");

	public static string Download_LocalImportDialogSubtitle => Get("Download_LocalImportDialogSubtitle");

	public static string Download_LocalImportDropZoneTitle => Get("Download_LocalImportDropZoneTitle");

	public static string Download_LocalImportDropZoneSubtitle => Get("Download_LocalImportDropZoneSubtitle");

	public static string Download_LocalImportSelectedFileLabel => Get("Download_LocalImportSelectedFileLabel");

	public static string Download_LocalImportNoFileSelected => Get("Download_LocalImportNoFileSelected");

	public static string Download_LocalImportUnrecognizedMessage => Get("Download_LocalImportUnrecognizedMessage");

	public static string Download_LocalImportSelectFileButton => Get("Download_LocalImportSelectFileButton");

	public static string Download_LocalImportTaskTitle => Get("Download_LocalImportTaskTitle");

	public static string Download_VanillaLoaderTitle => Get("Download_VanillaLoaderTitle");

	public static string Download_FabricLoaderTitle => Get("Download_FabricLoaderTitle");

	public static string Download_ForgeLoaderTitle => Get("Download_ForgeLoaderTitle");

	public static string Download_NeoForgeLoaderTitle => Get("Download_NeoForgeLoaderTitle");

	public static string Download_QuiltLoaderTitle => Get("Download_QuiltLoaderTitle");

	public static string Download_VanillaLoaderSubtitle => Get("Download_VanillaLoaderSubtitle");

	public static string Download_FabricLoaderSubtitle => Get("Download_FabricLoaderSubtitle");

	public static string Download_ForgeLoaderSubtitle => Get("Download_ForgeLoaderSubtitle");

	public static string Download_NeoForgeLoaderSubtitle => Get("Download_NeoForgeLoaderSubtitle");

	public static string Download_QuiltLoaderSubtitle => Get("Download_QuiltLoaderSubtitle");

	public static string Download_FabricLoaderVersionLabel => Get("Download_FabricLoaderVersionLabel");

	public static string Download_FabricLoaderVersionEmpty => Get("Download_FabricLoaderVersionEmpty");

	public static string Download_LoaderVersionEmptyFormat => Get("Download_LoaderVersionEmptyFormat");

	public static string Download_LoaderVersionStableTag => Get("Download_LoaderVersionStableTag");

	public static string Download_LoaderVersionPreviewTag => Get("Download_LoaderVersionPreviewTag");

	public static string Download_AddonLibraryNone => Get("Download_AddonLibraryNone");

	public static string Download_AddonLibraryLatestTag => Get("Download_AddonLibraryLatestTag");

	public static string Download_AddonLibraryLoading => Get("Download_AddonLibraryLoading");

	public static string Download_AddonLibraryEmpty => Get("Download_AddonLibraryEmpty");

	public static string Download_AddonLibraryLoadFailedShort => Get("Download_AddonLibraryLoadFailedShort");

	public static string Download_FabricApiLabel => Get("Download_FabricApiLabel");

	public static string Download_QuiltLibraryLabel => Get("Download_QuiltLibraryLabel");

	public static string Download_QuiltLibraryNone => Get("Download_QuiltLibraryNone");

	public static string Download_QuiltLibraryLatestTag => Get("Download_QuiltLibraryLatestTag");

	public static string Download_QuiltLibraryLoading => Get("Download_QuiltLibraryLoading");

	public static string Download_QuiltLibraryEmpty => Get("Download_QuiltLibraryEmpty");

	public static string Download_QuiltLibraryLoadFailedShort => Get("Download_QuiltLibraryLoadFailedShort");

	public static string Download_SelectVersionPlaceholder => Get("Download_SelectVersionPlaceholder");

	public static string Download_LoaderVersionLoadFailedShort => Get("Download_LoaderVersionLoadFailedShort");

	public static string Download_LoaderPendingSubtitle => Get("Download_LoaderPendingSubtitle");

	public static string Install_Empty => Get("Install_Empty");

	public static string DownloadTask_Preparing => Get("DownloadTask_Preparing");

	public static string DownloadTask_Completed => Get("DownloadTask_Completed");

	public static string DownloadTask_Failed => Get("DownloadTask_Failed");

	public static string DownloadTask_Running => Get("DownloadTask_Running");

	public static string Status_Ready => Get("Status_Ready");

	public static string Status_NoLaunchableInstance => Get("Status_NoLaunchableInstance");

	public static string Status_LaunchPreparing => Get("Status_LaunchPreparing");

	public static string Status_LaunchCheckingInstance => Get("Status_LaunchCheckingInstance");

	public static string Status_LaunchRepairingMetadata => Get("Status_LaunchRepairingMetadata");

	public static string Status_LaunchRepairingLoaderInstaller => Get("Status_LaunchRepairingLoaderInstaller");

	public static string Status_LaunchRunningLoaderInstaller => Get("Status_LaunchRunningLoaderInstaller");

	public static string Status_LaunchFinalizingLoaderVersion => Get("Status_LaunchFinalizingLoaderVersion");

	public static string Status_LaunchPublishingLoaderArtifacts => Get("Status_LaunchPublishingLoaderArtifacts");

	public static string Status_LaunchRevalidatingFiles => Get("Status_LaunchRevalidatingFiles");

	public static string Status_LaunchRepairingJar => Get("Status_LaunchRepairingJar");

	public static string Status_LaunchRepairingLibraries => Get("Status_LaunchRepairingLibraries");

	public static string Status_LaunchRepairingAssets => Get("Status_LaunchRepairingAssets");

	public static string Status_LaunchRepairingLogging => Get("Status_LaunchRepairingLogging");

	public static string Status_LaunchCheckingJava => Get("Status_LaunchCheckingJava");

	public static string Status_InstallDownloadingJava => Get("Status_InstallDownloadingJava");

	public static string Status_LaunchRunningPreLaunchCommand => Get("Status_LaunchRunningPreLaunchCommand");

	public static string Status_LaunchPreparingProcess => Get("Status_LaunchPreparingProcess");

	public static string Status_LaunchPreparingOfflineSkin => Get("Status_LaunchPreparingOfflineSkin");

	public static string Status_LaunchStartingProcess => Get("Status_LaunchStartingProcess");

	public static string Status_LaunchCheckingFiles => Get("Status_LaunchCheckingFiles");

	public static string Status_LaunchDownloadingFiles => Get("Status_LaunchDownloadingFiles");

	public static string Status_LaunchCanceled => Get("Status_LaunchCanceled");

	public static string Status_LaunchAccountUnavailable => Get("Status_LaunchAccountUnavailable");

	public static string Status_ThirdPartyReauthenticationRequired => Get("Status_ThirdPartyReauthenticationRequired");

	public static string Account_ThirdPartyPlatformFormat => Get("Account_ThirdPartyPlatformFormat");

	public static string Status_MicrosoftReauthenticationRequired => Get("Status_MicrosoftReauthenticationRequired");

	public static string Status_MicrosoftReauthenticationSuccessful => Get("Status_MicrosoftReauthenticationSuccessful");

	public static string Status_MicrosoftReauthenticationAccountMismatch => Get("Status_MicrosoftReauthenticationAccountMismatch");

	public static string Status_MicrosoftCredentialStorageFailed => Get("Status_MicrosoftCredentialStorageFailed");

	public static string Status_MicrosoftLoginNotConfigured => Get("Status_MicrosoftLoginNotConfigured");

	public static string Status_MicrosoftApplicationNotAuthorized => Get("Status_MicrosoftApplicationNotAuthorized");

	public static string Status_MicrosoftAuthenticationServerUnavailable => Get("Status_MicrosoftAuthenticationServerUnavailable");

	public static string Status_MicrosoftAuthenticationTimedOut => Get("Status_MicrosoftAuthenticationTimedOut");

	public static string Status_MinecraftJavaOwnershipRequired => Get("Status_MinecraftJavaOwnershipRequired");

	public static string SelectAll_Button => Get("SelectAll_Button");

	public static string Retry_Button => Get("Retry_Button");

	public static string Dialog_ThirdPartyProfileSelectionTitle => Get("Dialog_ThirdPartyProfileSelectionTitle");

	public static string Dialog_ThirdPartyProfileSelectionSubtitle => Get("Dialog_ThirdPartyProfileSelectionSubtitle");

	public static string Dialog_ThirdPartyImportProgressTitle => Get("Dialog_ThirdPartyImportProgressTitle");

	public static string Dialog_ThirdPartyImportProgressSubtitle => Get("Dialog_ThirdPartyImportProgressSubtitle");

	public static string Dialog_ThirdPartyImportResultTitle => Get("Dialog_ThirdPartyImportResultTitle");

	public static string Dialog_ThirdPartyImportResultSubtitle => Get("Dialog_ThirdPartyImportResultSubtitle");

	public static string Dialog_ThirdPartyImportProgressFormat => Get("Dialog_ThirdPartyImportProgressFormat");

	public static string Dialog_ThirdPartyImportFailureFormat => Get("Dialog_ThirdPartyImportFailureFormat");

	public static string Status_LaunchExitedQuickly => Get("Status_LaunchExitedQuickly");

	public static string Status_LaunchProcessExited => Get("Status_LaunchProcessExited");

	public static string Status_LaunchAbnormalExit => Get("Status_LaunchAbnormalExit");

	public static string Status_LaunchRuntimeAbnormalExit => Get("Status_LaunchRuntimeAbnormalExit");

	public static string Status_LaunchFailed => Get("Status_LaunchFailed");

	public static string Status_LaunchOfflineSkinUnavailable => Get("Status_LaunchOfflineSkinUnavailable");

	public static string Status_LaunchInstanceRepairFailed => Get("Status_LaunchInstanceRepairFailed");

	public static string Status_LaunchInstanceSelectedFormat => Get("Status_LaunchInstanceSelectedFormat");

	public static string Status_LaunchInstanceSelectionFailed => Get("Status_LaunchInstanceSelectionFailed");

	public static string Status_LoadingVersions => Get("Status_LoadingVersions");

	public static string Status_VersionsLoadedFormat => Get("Status_VersionsLoadedFormat");

	public static string Status_LoaderVersionsPendingFormat => Get("Status_LoaderVersionsPendingFormat");

	public static string Status_LoadingLoaderVersionsFormat => Get("Status_LoadingLoaderVersionsFormat");

	public static string Status_LoaderVersionsLoadedFormat => Get("Status_LoaderVersionsLoadedFormat");

	public static string Status_SelectMinecraftVersionFirst => Get("Status_SelectMinecraftVersionFirst");

	public static string Status_InstanceCreatedFormat => Get("Status_InstanceCreatedFormat");

	public static string Status_LocalModImported => Get("Status_LocalModImported");

	public static string Status_LocalModImportFileNotFound => Get("Status_LocalModImportFileNotFound");

	public static string Status_LocalModImportFailed => Get("Status_LocalModImportFailed");

	public static string Status_LocalModsImportedFormat => Get("Status_LocalModsImportedFormat");

	public static string Status_LocalSaveImported => Get("Status_LocalSaveImported");

	public static string Status_LocalSaveImportFileNotFound => Get("Status_LocalSaveImportFileNotFound");

	public static string Status_LocalSaveImportFailed => Get("Status_LocalSaveImportFailed");

	public static string Status_LocalSavesImportedFormat => Get("Status_LocalSavesImportedFormat");

	public static string Status_LocalResourcePackImported => Get("Status_LocalResourcePackImported");

	public static string Status_LocalResourcePackImportFileNotFound => Get("Status_LocalResourcePackImportFileNotFound");

	public static string Status_LocalResourcePackImportFailed => Get("Status_LocalResourcePackImportFailed");

	public static string Status_LocalResourcePacksImportedFormat => Get("Status_LocalResourcePacksImportedFormat");

	public static string Status_LocalShaderPackImported => Get("Status_LocalShaderPackImported");

	public static string Status_LocalShaderPackImportFileNotFound => Get("Status_LocalShaderPackImportFileNotFound");

	public static string Status_LocalShaderPackImportFailed => Get("Status_LocalShaderPackImportFailed");

	public static string Status_LocalShaderPacksImportedFormat => Get("Status_LocalShaderPacksImportedFormat");

	public static string Status_ModEnabledStateTargetExistsFormat => Get("Status_ModEnabledStateTargetExistsFormat");

	public static string Status_SelectedModsEnabledFormat => Get("Status_SelectedModsEnabledFormat");

	public static string Status_SelectedModsEnablePartialFailedFormat => Get("Status_SelectedModsEnablePartialFailedFormat");

	public static string Status_SelectedModsEnableFailed => Get("Status_SelectedModsEnableFailed");

	public static string Status_SelectedModsDisabledFormat => Get("Status_SelectedModsDisabledFormat");

	public static string Status_SelectedModsDisablePartialFailedFormat => Get("Status_SelectedModsDisablePartialFailedFormat");

	public static string Status_SelectedModsDisableFailed => Get("Status_SelectedModsDisableFailed");

	public static string Status_SelectedModsDeletedFormat => Get("Status_SelectedModsDeletedFormat");

	public static string Status_SelectedModsDeletePartialFailedFormat => Get("Status_SelectedModsDeletePartialFailedFormat");

	public static string Status_SelectedModsDeleteFailed => Get("Status_SelectedModsDeleteFailed");

	public static string Status_SelectedSavesDeletedFormat => Get("Status_SelectedSavesDeletedFormat");

	public static string Status_SelectedSavesDeletePartialFailedFormat => Get("Status_SelectedSavesDeletePartialFailedFormat");

	public static string Status_SelectedSavesDeleteFailed => Get("Status_SelectedSavesDeleteFailed");

	public static string Status_SelectedResourcePacksDeletedFormat => Get("Status_SelectedResourcePacksDeletedFormat");

	public static string Status_SelectedResourcePacksDeletePartialFailedFormat => Get("Status_SelectedResourcePacksDeletePartialFailedFormat");

	public static string Status_SelectedResourcePacksDeleteFailed => Get("Status_SelectedResourcePacksDeleteFailed");

	public static string Status_SelectedShaderPacksDeletedFormat => Get("Status_SelectedShaderPacksDeletedFormat");

	public static string Status_SelectedShaderPacksDeletePartialFailedFormat => Get("Status_SelectedShaderPacksDeletePartialFailedFormat");

	public static string Status_SelectedShaderPacksDeleteFailed => Get("Status_SelectedShaderPacksDeleteFailed");

	public static string Status_OpenModFileLocationFailed => Get("Status_OpenModFileLocationFailed");

	public static string Status_OpenLocalSaveFolderFailed => Get("Status_OpenLocalSaveFolderFailed");

	public static string Status_OpenLocalResourcePackFolderFailed => Get("Status_OpenLocalResourcePackFolderFailed");

	public static string Status_OpenLocalResourcePackLocationFailed => Get("Status_OpenLocalResourcePackLocationFailed");

	public static string Status_OpenLocalShaderPackFolderFailed => Get("Status_OpenLocalShaderPackFolderFailed");

	public static string Status_OpenLocalShaderPackLocationFailed => Get("Status_OpenLocalShaderPackLocationFailed");

	public static string Status_SelectInstanceFirst => Get("Status_SelectInstanceFirst");

	public static string Status_SearchingModrinth => Get("Status_SearchingModrinth");

	public static string Status_LoadLocalModsFailed => Get("Status_LoadLocalModsFailed");

	public static string Status_LoadLocalSavesFailed => Get("Status_LoadLocalSavesFailed");

	public static string Status_LoadLocalResourcePacksFailed => Get("Status_LoadLocalResourcePacksFailed");

	public static string Status_LoadLocalShaderPacksFailed => Get("Status_LoadLocalShaderPacksFailed");

	public static string Status_ModDownloading => Get("Status_ModDownloading");

	public static string Status_ModDownloadingFormat => Get("Status_ModDownloadingFormat");

	public static string Status_ModRequiredDependencyInstallingFormat => Get("Status_ModRequiredDependencyInstallingFormat");

	public static string Status_ModRequiredDependenciesAutoInstallFailedFormat => Get("Status_ModRequiredDependenciesAutoInstallFailedFormat");

	public static string Status_ModCompatibleFileNotFound => Get("Status_ModCompatibleFileNotFound");

	public static string Status_ModrinthResultsFoundFormat => Get("Status_ModrinthResultsFoundFormat");

	public static string Status_ModInstalledFormat => Get("Status_ModInstalledFormat");

	public static string Status_ModInstallFailed => Get("Status_ModInstallFailed");

	public static string Status_ModDownloadedFormat => Get("Status_ModDownloadedFormat");

	public static string Status_ModDownloadFailed => Get("Status_ModDownloadFailed");

	public static string Status_ResourcePackDownloading => Get("Status_ResourcePackDownloading");

	public static string Status_ResourcePackDownloadingFormat => Get("Status_ResourcePackDownloadingFormat");

	public static string Status_ResourcePackInstalledFormat => Get("Status_ResourcePackInstalledFormat");

	public static string Status_ResourcePackInstallFailed => Get("Status_ResourcePackInstallFailed");

	public static string Status_ResourcePackDownloadedFormat => Get("Status_ResourcePackDownloadedFormat");

	public static string Status_ResourcePackDownloadFailed => Get("Status_ResourcePackDownloadFailed");

	public static string Status_ShaderPackDownloading => Get("Status_ShaderPackDownloading");

	public static string Status_ShaderPackDownloadingFormat => Get("Status_ShaderPackDownloadingFormat");

	public static string Status_ShaderPackInstalledFormat => Get("Status_ShaderPackInstalledFormat");

	public static string Status_ShaderPackInstallFailed => Get("Status_ShaderPackInstallFailed");

	public static string Status_ShaderPackDownloadedFormat => Get("Status_ShaderPackDownloadedFormat");

	public static string Status_ShaderPackDownloadFailed => Get("Status_ShaderPackDownloadFailed");

	public static string Status_WorldDownloading => Get("Status_WorldDownloading");

	public static string Status_WorldDownloadingFormat => Get("Status_WorldDownloadingFormat");

	public static string Status_WorldInstalledFormat => Get("Status_WorldInstalledFormat");

	public static string Status_WorldInstallFailed => Get("Status_WorldInstallFailed");

	public static string Status_WorldDownloadedFormat => Get("Status_WorldDownloadedFormat");

	public static string Status_WorldDownloadFailed => Get("Status_WorldDownloadFailed");

	public static string Status_SettingsSaved => Get("Status_SettingsSaved");

	public static string Status_SettingsSaveFailed => Get("Status_SettingsSaveFailed");

	public static string Status_JavaScanFailed => Get("Status_JavaScanFailed");

	public static string Status_JavaImported => Get("Status_JavaImported");

	public static string Status_JavaAlreadyExists => Get("Status_JavaAlreadyExists");

	public static string Status_JavaImportFailed => Get("Status_JavaImportFailed");

	public static string Status_JavaSelectionFailed => Get("Status_JavaSelectionFailed");

	public static string Status_InstanceSettingsSaved => Get("Status_InstanceSettingsSaved");

	public static string Status_DefaultInstanceSetFormat => Get("Status_DefaultInstanceSetFormat");

	public static string Status_InstanceDeletedFormat => Get("Status_InstanceDeletedFormat");

	public static string Status_DeleteInstanceFailed => Get("Status_DeleteInstanceFailed");

	public static string Status_InstanceFolderNotFound => Get("Status_InstanceFolderNotFound");

	public static string Status_OpenInstanceFolderFailed => Get("Status_OpenInstanceFolderFailed");

	public static string Status_OpenMinecraftDirectoryFailed => Get("Status_OpenMinecraftDirectoryFailed");

	public static string Status_MinecraftDirectoryChanged => Get("Status_MinecraftDirectoryChanged");

	public static string Status_MinecraftDirectoryAdded => Get("Status_MinecraftDirectoryAdded");

	public static string Status_AddMinecraftDirectoryFailed => Get("Status_AddMinecraftDirectoryFailed");

	public static string Status_MinecraftDirectoryUnavailable => Get("Status_MinecraftDirectoryUnavailable");

	public static string Status_MinecraftDirectorySwitchFailed => Get("Status_MinecraftDirectorySwitchFailed");

	public static string Status_MinecraftDirectoryRemovedFromList => Get("Status_MinecraftDirectoryRemovedFromList");

	public static string Status_RemoveMinecraftDirectoryFromListFailed => Get("Status_RemoveMinecraftDirectoryFromListFailed");

	public static string Status_MinecraftDirectoryRenamed => Get("Status_MinecraftDirectoryRenamed");

	public static string Status_RenameMinecraftDirectoryFailed => Get("Status_RenameMinecraftDirectoryFailed");

	public static string Status_MinecraftDirectoryChangeFailed => Get("Status_MinecraftDirectoryChangeFailed");

	public static string Status_OpenBackupDirectoryFailed => Get("Status_OpenBackupDirectoryFailed");

	public static string Status_BackupDirectoryChanged => Get("Status_BackupDirectoryChanged");

	public static string Status_BackupDirectoryChangeFailed => Get("Status_BackupDirectoryChangeFailed");

	public static string Status_BackupCreating => Get("Status_BackupCreating");

	public static string Status_BackupCreated => Get("Status_BackupCreated");

	public static string Status_LoadBackupsFailed => Get("Status_LoadBackupsFailed");

	public static string Status_OpenBackupLocationFailed => Get("Status_OpenBackupLocationFailed");

	public static string Status_BackupDeleted => Get("Status_BackupDeleted");

	public static string Status_BackupDeleteFailed => Get("Status_BackupDeleteFailed");

	public static string Status_SelectedBackupsDeletedFormat => Get("Status_SelectedBackupsDeletedFormat");

	public static string Status_BackupRestoring => Get("Status_BackupRestoring");

	public static string Status_BackupRestored => Get("Status_BackupRestored");

	public static string Status_BackupRestoreFailed => Get("Status_BackupRestoreFailed");

	public static string Status_LaunchReportExportSucceededFormat => Get("Status_LaunchReportExportSucceededFormat");

	public static string Status_LaunchReportExportPartialFormat => Get("Status_LaunchReportExportPartialFormat");

	public static string Status_LaunchReportExportNoReadableFiles => Get("Status_LaunchReportExportNoReadableFiles");

	public static string Status_LaunchReportExportFailed => Get("Status_LaunchReportExportFailed");

	public static string Status_OpenLaunchLogFolderFailed => Get("Status_OpenLaunchLogFolderFailed");

	public static string Status_LauncherLogsClearedFormat => Get("Status_LauncherLogsClearedFormat");

	public static string Status_NoLauncherLogsToClear => Get("Status_NoLauncherLogsToClear");

	public static string Status_ClearLauncherLogsFailed => Get("Status_ClearLauncherLogsFailed");

	public static string Status_OpenLaunchReportFailed => Get("Status_OpenLaunchReportFailed");

	public static string Status_OpenGithubRepositoryFailed => Get("Status_OpenGithubRepositoryFailed");

	public static string Status_OpenFeedbackPageFailed => Get("Status_OpenFeedbackPageFailed");

	public static string Status_OpenMinecraftPurchasePageFailed => Get("Status_OpenMinecraftPurchasePageFailed");

	public static string Status_OpenReferenceProjectFailed => Get("Status_OpenReferenceProjectFailed");

	public static string Status_OpenRelatedWebsiteFailed => Get("Status_OpenRelatedWebsiteFailed");

	public static string Status_OpenResourceDetailsFailed => Get("Status_OpenResourceDetailsFailed");

	public static string Status_UpdateCheckUnavailable => Get("Status_UpdateCheckUnavailable");

	public static string Status_CheckingUpdates => Get("Status_CheckingUpdates");

	public static string Status_LauncherAlreadyLatest => Get("Status_LauncherAlreadyLatest");

	public static string Status_CheckUpdatesFailed => Get("Status_CheckUpdatesFailed");

	public static string Status_OpenUpdatePageFailed => Get("Status_OpenUpdatePageFailed");

	public static string Status_DownloadingLauncherUpdate => Get("Status_DownloadingLauncherUpdate");

	public static string Status_UpdateAutoInstallPackageNotFound => Get("Status_UpdateAutoInstallPackageNotFound");

	public static string Status_LauncherUpdateStartFailed => Get("Status_LauncherUpdateStartFailed");

	public static string Status_LauncherUpdateRestarting => Get("Status_LauncherUpdateRestarting");

	public static string Status_RenamingInstance => Get("Status_RenamingInstance");

	public static string Status_InstanceRenamedFormat => Get("Status_InstanceRenamedFormat");

	public static string Status_InstanceRenameResultFormat => Get("Status_InstanceRenameResultFormat");

	public static string Status_InstanceRenameFailed => Get("Status_InstanceRenameFailed");

	public static string Status_InstanceRenameUnchanged => Get("Status_InstanceRenameUnchanged");

	public static string Status_InstanceSettingsSaveFailed => Get("Status_InstanceSettingsSaveFailed");

	public static string Status_LoadInstancesFailed => Get("Status_LoadInstancesFailed");

	public static string Status_LoadVersionsFailed => Get("Status_LoadVersionsFailed");

	public static string Status_UnimplementedCategory => Get("Status_UnimplementedCategory");

	public static string Status_NoCategoryVersionsFormat => Get("Status_NoCategoryVersionsFormat");

	public static string Status_NoMatchingVersions => Get("Status_NoMatchingVersions");

	public static string Status_DuplicateInstanceName => Get("Status_DuplicateInstanceName");

	public static string Status_InstallQueued => Get("Status_InstallQueued");

	public static string Status_InstallStartingDownload => Get("Status_InstallStartingDownload");

	public static string Status_InstallPreparing => Get("Status_InstallPreparing");

	public static string Status_InstallCheckingFiles => Get("Status_InstallCheckingFiles");

	public static string Status_InstallDownloadingFiles => Get("Status_InstallDownloadingFiles");

	public static string Status_InstallDownloadingLoaderInstaller => Get("Status_InstallDownloadingLoaderInstaller");

	public static string Status_InstallRunningLoaderInstaller => Get("Status_InstallRunningLoaderInstaller");

	public static string Status_InstallFinalizingVersion => Get("Status_InstallFinalizingVersion");

	public static string Status_InstallCompletingFiles => Get("Status_InstallCompletingFiles");

	public static string Status_InstallingVanillaFormat => Get("Status_InstallingVanillaFormat");

	public static string Status_InstallingLoaderFormat => Get("Status_InstallingLoaderFormat");

	public static string Status_InstanceInstalledFormat => Get("Status_InstanceInstalledFormat");

	public static string Status_InstallFailed => Get("Status_InstallFailed");

	public static string Status_ModpackPreparingArchive => Get("Status_ModpackPreparingArchive");

	public static string Status_ModpackParsingManifest => Get("Status_ModpackParsingManifest");

	public static string Status_ModpackResolvingFiles => Get("Status_ModpackResolvingFiles");

	public static string Status_ModpackResolvingFilesFormat => Get("Status_ModpackResolvingFilesFormat");

	public static string Status_ModpackCreatingInstance => Get("Status_ModpackCreatingInstance");

	public static string Status_ModpackInstallingMinecraftBase => Get("Status_ModpackInstallingMinecraftBase");

	public static string Status_ModpackInstallingLoader => Get("Status_ModpackInstallingLoader");

	public static string Status_ModpackDownloadingFiles => Get("Status_ModpackDownloadingFiles");

	public static string Status_ModpackDownloadingFileFormat => Get("Status_ModpackDownloadingFileFormat");

	public static string Status_ModpackProcessingFiles => Get("Status_ModpackProcessingFiles");

	public static string Status_ModpackDownloading => Get("Status_ModpackDownloading");

	public static string Status_ModpackDownloadingFormat => Get("Status_ModpackDownloadingFormat");

	public static string Status_ModpackDownloadedFormat => Get("Status_ModpackDownloadedFormat");

	public static string Status_ModpackDownloadFailed => Get("Status_ModpackDownloadFailed");

	public static string Status_ModpackCopyingOverrides => Get("Status_ModpackCopyingOverrides");

	public static string Status_ModpackCleaningUp => Get("Status_ModpackCleaningUp");

	public static string Status_ModpackImportedFormat => Get("Status_ModpackImportedFormat");

	public static string Status_ModpackImportedWithManualDownloadsFormat => Get("Status_ModpackImportedWithManualDownloadsFormat");

	public static string Status_ModpackInvalidArchive => Get("Status_ModpackInvalidArchive");

	public static string Status_ModpackArchiveTooLarge => Get("Status_ModpackArchiveTooLarge");

	public static string Status_ModpackInsufficientDiskSpace => Get("Status_ModpackInsufficientDiskSpace");

	public static string Status_ModpackUnsupportedLoader => Get("Status_ModpackUnsupportedLoader");

	public static string Status_ModpackMissingCurseForgeApiKey => Get("Status_ModpackMissingCurseForgeApiKey");

	public static string Status_ModpackHashMismatch => Get("Status_ModpackHashMismatch");

	public static string Status_ResourceProjectIntegrityFailed => Get("Status_ResourceProjectIntegrityFailed");

	public static string Status_ResourceProjectDownloadAlreadyRunning => Get("Status_ResourceProjectDownloadAlreadyRunning");

	public static string Status_ResourceProjectDestinationConflictFormat => Get("Status_ResourceProjectDestinationConflictFormat");

	public static string Status_ResourceProjectInstanceDestinationInvalid => Get("Status_ResourceProjectInstanceDestinationInvalid");

	public static string Status_ModpackExporting => Get("Status_ModpackExporting");

	public static string Status_ModpackExported => Get("Status_ModpackExported");

	public static string Status_ModpackExportedFormat => Get("Status_ModpackExportedFormat");

	public static string Status_ModpackExportFailed => Get("Status_ModpackExportFailed");

	public static string Status_ModpackExportMissingCurseForgeApiKey => Get("Status_ModpackExportMissingCurseForgeApiKey");

	public static string Status_ModpackExportMissingLoaderVersion => Get("Status_ModpackExportMissingLoaderVersion");

	public static string Status_ModpackExportCurseForgeApiFailed => Get("Status_ModpackExportCurseForgeApiFailed");

	public static string Status_ModpackExportModrinthApiFailed => Get("Status_ModpackExportModrinthApiFailed");

	public static string Status_ModpackExportInvalidRequest => Get("Status_ModpackExportInvalidRequest");

	public static string Status_ModpackExportFileSystemFailed => Get("Status_ModpackExportFileSystemFailed");

	public static string Status_ModrinthExportUnsupported => Get("Status_ModrinthExportUnsupported");

	public static string Status_ModpackInstalling => Get("Status_ModpackInstalling");

	public static string Status_ModpackImportFailed => Get("Status_ModpackImportFailed");

	public static string Status_ServerDeploying => Get("Status_ServerDeploying");

	public static string Status_ServerDeployedFormat => Get("Status_ServerDeployedFormat");

	public static string Status_ServerDeployFailed => Get("Status_ServerDeployFailed");

	public static string Status_ServerDirectoryExistsFormat => Get("Status_ServerDirectoryExistsFormat");

	public static string Status_ServerDistributionRestricted => Get("Status_ServerDistributionRestricted");

	public static string Status_OpenModpackManualDownloadsFileFailed => Get("Status_OpenModpackManualDownloadsFileFailed");

	public static string Status_LoaderInstallPending => Get("Status_LoaderInstallPending");

	public static string Status_FabricLoaderVersionsLoadFailed => Get("Status_FabricLoaderVersionsLoadFailed");

	public static string Status_LoaderVersionsLoadFailedFormat => Get("Status_LoaderVersionsLoadFailedFormat");

	public static string Status_FabricApiVersionsLoadFailed => Get("Status_FabricApiVersionsLoadFailed");

	public static string Status_QuiltLibraryVersionsLoadFailed => Get("Status_QuiltLibraryVersionsLoadFailed");

	public static string Status_OpeningMicrosoftLogin => Get("Status_OpeningMicrosoftLogin");

	public static string Status_LoginMicrosoftActive => Get("Status_LoginMicrosoftActive");

	public static string Status_LoginMissingProfile => Get("Status_LoginMissingProfile");

	public static string Status_LoginAccountAlreadyAddedFormat => Get("Status_LoginAccountAlreadyAddedFormat");

	public static string Status_LoginAccountAddedFormat => Get("Status_LoginAccountAddedFormat");

	public static string Status_LoginCanceled => Get("Status_LoginCanceled");

	public static string Status_LoginFailed => Get("Status_LoginFailed");

	public static string Status_OfflineAccountAddedFormat => Get("Status_OfflineAccountAddedFormat");

	public static string Status_OfflineUuidModeChangedFormat => Get("Status_OfflineUuidModeChangedFormat");

	public static string Status_OfflineUuidModeChangeFailed => Get("Status_OfflineUuidModeChangeFailed");

	public static string Status_OfflineUuidInvalid => Get("Status_OfflineUuidInvalid");

	public static string Status_OfflineUuidApplied => Get("Status_OfflineUuidApplied");

	public static string Status_AccountDeletedFormat => Get("Status_AccountDeletedFormat");

	public static string Status_AccountDeletedCacheCleanupFailedFormat => Get("Status_AccountDeletedCacheCleanupFailedFormat");

	public static string Status_AccountNameUnchanged => Get("Status_AccountNameUnchanged");

	public static string Status_SavingOfflineAccountName => Get("Status_SavingOfflineAccountName");

	public static string Status_ChangingMicrosoftAccountName => Get("Status_ChangingMicrosoftAccountName");

	public static string Status_AccountRenamedFormat => Get("Status_AccountRenamedFormat");

	public static string Status_AccountRenameResultFormat => Get("Status_AccountRenameResultFormat");

	public static string Status_AccountRenameFailed => Get("Status_AccountRenameFailed");

	public static string Status_AccountRenameFailedDuplicateName => Get("Status_AccountRenameFailedDuplicateName");

	public static string Status_AccountRenameFailedNotAllowed => Get("Status_AccountRenameFailedNotAllowed");

	public static string Status_AccountRenameFailedInvalidName => Get("Status_AccountRenameFailedInvalidName");

	public static string Status_RefreshingAccountProfile => Get("Status_RefreshingAccountProfile");

	public static string Status_AccountProfileRefreshed => Get("Status_AccountProfileRefreshed");

	public static string Status_AccountProfileRefreshFailed => Get("Status_AccountProfileRefreshFailed");

	public static string Status_AccountProfileRefreshTooFrequent => Get("Status_AccountProfileRefreshTooFrequent");

	public static string Status_AccountProfileRefreshOfflineUnsupported => Get("Status_AccountProfileRefreshOfflineUnsupported");

	public static string Status_ErrorCodeFormat => Get("Status_ErrorCodeFormat");

	public static string Status_SkinOfflineUnsupported => Get("Status_SkinOfflineUnsupported");

	public static string Status_AddingSkin => Get("Status_AddingSkin");

	public static string Status_SkinAdded => Get("Status_SkinAdded");

	public static string Status_UploadingSkin => Get("Status_UploadingSkin");

	public static string Status_SkinUpdated => Get("Status_SkinUpdated");

	public static string Status_SkinModelChanged => Get("Status_SkinModelChanged");

	public static string Status_SkinDeleted => Get("Status_SkinDeleted");

	public static string Status_SkinDeleteFailed => Get("Status_SkinDeleteFailed");

	public static string Status_SkinUpdateFailed => Get("Status_SkinUpdateFailed");

	public static string Status_CapeOfflineUnsupported => Get("Status_CapeOfflineUnsupported");

	public static string Status_ChangingCape => Get("Status_ChangingCape");

	public static string Status_CapeRemoved => Get("Status_CapeRemoved");

	public static string Status_CapeChangedFormat => Get("Status_CapeChangedFormat");

	public static string Status_CapeChangeFailed => Get("Status_CapeChangeFailed");

	public static string Status_LoadingAccountProfile => Get("Status_LoadingAccountProfile");

	public static string Status_LoadAccountProfileFailed => Get("Status_LoadAccountProfileFailed");

	public static string FilePicker_MinecraftSkinTitle => Get("FilePicker_MinecraftSkinTitle");

	public static string FilePicker_MinecraftSkinFilter => Get("FilePicker_MinecraftSkinFilter");

	public static string FilePicker_JavaExecutableTitle => Get("FilePicker_JavaExecutableTitle");

	public static string FilePicker_JavaExecutableFilter => Get("FilePicker_JavaExecutableFilter");

	public static string FilePicker_LocalImportFileTitle => Get("FilePicker_LocalImportFileTitle");

	public static string FilePicker_LocalImportFileFilter => Get("FilePicker_LocalImportFileFilter");

	public static string FilePicker_ModFileTitle => Get("FilePicker_ModFileTitle");

	public static string FilePicker_ModFileFilter => Get("FilePicker_ModFileFilter");

	public static string FilePicker_ModDownloadDirectoryTitle => Get("FilePicker_ModDownloadDirectoryTitle");

	public static string FilePicker_SaveArchiveTitle => Get("FilePicker_SaveArchiveTitle");

	public static string FilePicker_SaveArchiveFilter => Get("FilePicker_SaveArchiveFilter");

	public static string FilePicker_ResourcePackArchiveTitle => Get("FilePicker_ResourcePackArchiveTitle");

	public static string FilePicker_ResourcePackArchiveFilter => Get("FilePicker_ResourcePackArchiveFilter");

	public static string FilePicker_ResourcePackDownloadDirectoryTitle => Get("FilePicker_ResourcePackDownloadDirectoryTitle");

	public static string FilePicker_ShaderPackArchiveTitle => Get("FilePicker_ShaderPackArchiveTitle");

	public static string FilePicker_ShaderPackArchiveFilter => Get("FilePicker_ShaderPackArchiveFilter");

	public static string FilePicker_ShaderPackDownloadDirectoryTitle => Get("FilePicker_ShaderPackDownloadDirectoryTitle");

	public static string FilePicker_WorldDownloadDirectoryTitle => Get("FilePicker_WorldDownloadDirectoryTitle");

	public static string FilePicker_ModpackDownloadDirectoryTitle => Get("FilePicker_ModpackDownloadDirectoryTitle");

	public static string FilePicker_ServerInstallDirectoryTitle => Get("FilePicker_ServerInstallDirectoryTitle");

	public static string FilePicker_ModpackExportArchiveTitle => Get("FilePicker_ModpackExportArchiveTitle");

	public static string FilePicker_ModpackExportArchiveFilter => Get("FilePicker_ModpackExportArchiveFilter");

	public static string FilePicker_LaunchDiagnosticExportTitle => Get("FilePicker_LaunchDiagnosticExportTitle");

	public static string FilePicker_LaunchDiagnosticExportFilter => Get("FilePicker_LaunchDiagnosticExportFilter");

	public static string FilePicker_ModrinthModpackExportArchiveTitle => Get("FilePicker_ModrinthModpackExportArchiveTitle");

	public static string FilePicker_ModrinthModpackExportArchiveFilter => Get("FilePicker_ModrinthModpackExportArchiveFilter");

	public static string FilePicker_MinecraftDirectoryTitle => Get("FilePicker_MinecraftDirectoryTitle");

	public static string FilePicker_BackupDirectoryTitle => Get("FilePicker_BackupDirectoryTitle");

	public static string Dialog_LaunchStatusFailedTitle => Get("Dialog_LaunchStatusFailedTitle");

	public static string Dialog_LaunchStatusExitedTitle => Get("Dialog_LaunchStatusExitedTitle");

	public static string Dialog_LaunchStatusInitializationFailedTitle => Get("Dialog_LaunchStatusInitializationFailedTitle");

	public static string Dialog_LaunchStatusRuntimeFailedTitle => Get("Dialog_LaunchStatusRuntimeFailedTitle");

	public static string Dialog_LaunchStatusMessageFormat => Get("Dialog_LaunchStatusMessageFormat");

	public static string Dialog_LaunchStatusStartupFailedMessage => Get("Dialog_LaunchStatusStartupFailedMessage");

	public static string Dialog_LaunchStatusStartupExitedMessage => Get("Dialog_LaunchStatusStartupExitedMessage");

	public static string Dialog_LaunchStatusStartupAbnormalMessage => Get("Dialog_LaunchStatusStartupAbnormalMessage");

	public static string Dialog_LaunchStatusRuntimeAbnormalMessage => Get("Dialog_LaunchStatusRuntimeAbnormalMessage");

	public static string Dialog_LaunchStatusUnknownExitCode => Get("Dialog_LaunchStatusUnknownExitCode");

	public static string Dialog_LaunchStatusDiagnosticFileHint => Get("Dialog_LaunchStatusDiagnosticFileHint");

	public static string Dialog_LaunchStatusDiagnosticDirectoryHint => Get("Dialog_LaunchStatusDiagnosticDirectoryHint");

	public static string Dialog_ExportLaunchReportButton => Get("Dialog_ExportLaunchReportButton");

	public static string Dialog_ViewLaunchReportButton => Get("Dialog_ViewLaunchReportButton");

	public static string Dialog_LaunchAnalysisCurrentInstance => Get("Dialog_LaunchAnalysisCurrentInstance");

	public static string Dialog_LaunchAnalysisUnknownTitle => Get("Dialog_LaunchAnalysisUnknownTitle");

	public static string Dialog_LaunchAnalysisUnknownDetail => Get("Dialog_LaunchAnalysisUnknownDetail");

	public static string Dialog_LaunchAnalysisUnknownRecommendation => Get("Dialog_LaunchAnalysisUnknownRecommendation");

	public static string Dialog_LaunchAnalysisJavaVersionTitle => Get("Dialog_LaunchAnalysisJavaVersionTitle");

	public static string Dialog_LaunchAnalysisJavaVersionDetailFormat => Get("Dialog_LaunchAnalysisJavaVersionDetailFormat");

	public static string Dialog_LaunchAnalysisJavaVersionRecommendationFormat => Get("Dialog_LaunchAnalysisJavaVersionRecommendationFormat");

	public static string Dialog_LaunchAnalysisModDependencyTitle => Get("Dialog_LaunchAnalysisModDependencyTitle");

	public static string Dialog_LaunchAnalysisModDependencyDetailFormat => Get("Dialog_LaunchAnalysisModDependencyDetailFormat");

	public static string Dialog_LaunchAnalysisModDependencyDetail => Get("Dialog_LaunchAnalysisModDependencyDetail");

	public static string Dialog_LaunchAnalysisModDependencyRecommendation => Get("Dialog_LaunchAnalysisModDependencyRecommendation");

	public static string Dialog_LaunchAnalysisModVersionTitle => Get("Dialog_LaunchAnalysisModVersionTitle");

	public static string Dialog_LaunchAnalysisModVersionDetail => Get("Dialog_LaunchAnalysisModVersionDetail");

	public static string Dialog_LaunchAnalysisModVersionRecommendation => Get("Dialog_LaunchAnalysisModVersionRecommendation");

	public static string Dialog_LaunchAnalysisDetailMissingDependencyFormat => Get("Dialog_LaunchAnalysisDetailMissingDependencyFormat");

	public static string Dialog_LaunchAnalysisDetailWrongVersionFormat => Get("Dialog_LaunchAnalysisDetailWrongVersionFormat");

	public static string Dialog_LaunchAnalysisDetailConflictFormat => Get("Dialog_LaunchAnalysisDetailConflictFormat");

	public static string Dialog_LaunchAnalysisOriginalReasonLabel => Get("Dialog_LaunchAnalysisOriginalReasonLabel");

	public static string Dialog_LaunchAnalysisOriginalSuggestionLabel => Get("Dialog_LaunchAnalysisOriginalSuggestionLabel");

	public static string Dialog_LaunchAnalysisAdditionalDetailsFormat => Get("Dialog_LaunchAnalysisAdditionalDetailsFormat");

	public static string Dialog_LaunchAnalysisMissingFilesTitle => Get("Dialog_LaunchAnalysisMissingFilesTitle");

	public static string Dialog_LaunchAnalysisMissingFilesDetail => Get("Dialog_LaunchAnalysisMissingFilesDetail");

	public static string Dialog_LaunchAnalysisMissingClasspathEntryDetailFormat => Get("Dialog_LaunchAnalysisMissingClasspathEntryDetailFormat");

	public static string Dialog_LaunchAnalysisMissingClientJarDetailFormat => Get("Dialog_LaunchAnalysisMissingClientJarDetailFormat");

	public static string Dialog_LaunchAnalysisMissingFilesRecommendation => Get("Dialog_LaunchAnalysisMissingFilesRecommendation");

	public static string Dialog_LaunchAnalysisGameFileIntegrityTitle => Get("Dialog_LaunchAnalysisGameFileIntegrityTitle");

	public static string Dialog_LaunchAnalysisGameFileIntegrityMissingDetail => Get("Dialog_LaunchAnalysisGameFileIntegrityMissingDetail");

	public static string Dialog_LaunchAnalysisGameFileIntegrityMissingDetailFormat => Get("Dialog_LaunchAnalysisGameFileIntegrityMissingDetailFormat");

	public static string Dialog_LaunchAnalysisGameFileIntegrityCorruptedDetail => Get("Dialog_LaunchAnalysisGameFileIntegrityCorruptedDetail");

	public static string Dialog_LaunchAnalysisGameFileIntegrityCorruptedDetailFormat => Get("Dialog_LaunchAnalysisGameFileIntegrityCorruptedDetailFormat");

	public static string Dialog_LaunchAnalysisGameFileIntegrityMetadataIncompleteDetail => Get("Dialog_LaunchAnalysisGameFileIntegrityMetadataIncompleteDetail");

	public static string Dialog_LaunchAnalysisGameFileIntegrityDownloadFailedDetail => Get("Dialog_LaunchAnalysisGameFileIntegrityDownloadFailedDetail");

	public static string Dialog_LaunchAnalysisGameFileIntegrityProcessorRegenerationFailedDetail => Get("Dialog_LaunchAnalysisGameFileIntegrityProcessorRegenerationFailedDetail");

	public static string Dialog_LaunchAnalysisGameFileIntegrityPublicationFailedDetail => Get("Dialog_LaunchAnalysisGameFileIntegrityPublicationFailedDetail");

	public static string Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidDetail => Get("Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidDetail");

	public static string Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidDetailFormat => Get("Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidDetailFormat");

	public static string Dialog_LaunchAnalysisGameFileIntegrityEnableAutoRepairRecommendation => Get("Dialog_LaunchAnalysisGameFileIntegrityEnableAutoRepairRecommendation");

	public static string Dialog_LaunchAnalysisGameFileIntegrityRepairFailedRecommendation => Get("Dialog_LaunchAnalysisGameFileIntegrityRepairFailedRecommendation");

	public static string Dialog_LaunchAnalysisGameFileIntegrityMetadataIncompleteRecommendation => Get("Dialog_LaunchAnalysisGameFileIntegrityMetadataIncompleteRecommendation");

	public static string Dialog_LaunchAnalysisGameFileIntegrityDownloadFailedRecommendation => Get("Dialog_LaunchAnalysisGameFileIntegrityDownloadFailedRecommendation");

	public static string Dialog_LaunchAnalysisGameFileIntegrityProcessorRegenerationFailedRecommendation => Get("Dialog_LaunchAnalysisGameFileIntegrityProcessorRegenerationFailedRecommendation");

	public static string Dialog_LaunchAnalysisGameFileIntegrityPublicationFailedRecommendation => Get("Dialog_LaunchAnalysisGameFileIntegrityPublicationFailedRecommendation");

	public static string Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidRecommendation => Get("Dialog_LaunchAnalysisGameFileIntegrityFinalLaunchPlanInvalidRecommendation");

	public static string Dialog_LaunchAnalysisOutOfMemoryTitle => Get("Dialog_LaunchAnalysisOutOfMemoryTitle");

	public static string Dialog_LaunchAnalysisOutOfMemoryDetail => Get("Dialog_LaunchAnalysisOutOfMemoryDetail");

	public static string Dialog_LaunchAnalysisOutOfMemoryRecommendation => Get("Dialog_LaunchAnalysisOutOfMemoryRecommendation");

	private static string Get(string name)
	{
		return ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
	}

	private static string Get(string name, CultureInfo culture)
	{
		return ResourceManager.GetString(name, culture) ?? name;
	}

	public static string GetLanguageRestartNotice(CultureInfo culture)
	{
		return Get("Settings_LanguageRestartNotice", culture);
	}
	public static string Settings_UpdateChannelOnlyReleaseTitle => Get("Settings_UpdateChannelOnlyReleaseTitle");

	public static string Settings_UpdateChannelOnlyReleaseDescription => Get("Settings_UpdateChannelOnlyReleaseDescription");

	public static string Settings_UpdateSectionDescription => Get("Settings_UpdateSectionDescription");

	public static string Settings_GameInstallSection => Get("Settings_GameInstallSection");

	public static string Settings_GameInstallLabel => Get("Settings_GameInstallLabel");

	public static string Settings_GameInstallItemCurrent => Get("Settings_GameInstallItemCurrent");

	public static string Settings_GameInstallItemCandidate => Get("Settings_GameInstallItemCandidate");

	public static string Settings_GameInstallAutoDetectButton => Get("Settings_GameInstallAutoDetectButton");

	public static string Settings_GameInstallBrowseButton => Get("Settings_GameInstallBrowseButton");

	public static string Settings_GameInstallOpenButton => Get("Settings_GameInstallOpenButton");

	public static string Settings_GameInstallFoundFormat => Get("Settings_GameInstallFoundFormat");

	public static string Settings_GameInstallCurrentFormat => Get("Settings_GameInstallCurrentFormat");

	public static string Settings_GameInstallMissing => Get("Settings_GameInstallMissing");

	public static string Settings_GameInstallInvalid => Get("Settings_GameInstallInvalid");

	public static string Settings_GameInstallAppliedFormat => Get("Settings_GameInstallAppliedFormat");

	public static string Settings_GameInstallDetectFailed => Get("Settings_GameInstallDetectFailed");

	public static string Settings_GameInstallVersionFormat => Get("Settings_GameInstallVersionFormat");

	public static string Settings_GameInstallUnknownVersion => Get("Settings_GameInstallUnknownVersion");

	public static string Settings_GameInstallDetecting => Get("Settings_GameInstallDetecting");

	public static string Settings_GameConfigSection => Get("Settings_GameConfigSection");

	public static string Settings_GameConfigDescription => Get("Settings_GameConfigDescription");

	public static string Settings_GameConfigCheckButton => Get("Settings_GameConfigCheckButton");

	public static string Settings_GameConfigRepairing => Get("Settings_GameConfigRepairing");

	public static string Settings_GameConfigAllOk => Get("Settings_GameConfigAllOk");

	public static string Settings_GameConfigRepairedFormat => Get("Settings_GameConfigRepairedFormat");

	public static string Settings_GameConfigCheckedFormat => Get("Settings_GameConfigCheckedFormat");

	public static string Settings_GameConfigFailedFormat => Get("Settings_GameConfigFailedFormat");

	public static string Settings_GameConfigAutoRepairLabel => Get("Settings_GameConfigAutoRepairLabel");

	public static string Settings_GameConfigListLabel => Get("Settings_GameConfigListLabel");

	public static string Settings_GameConfigStatusOk => Get("Settings_GameConfigStatusOk");

	public static string Settings_GameConfigStatusMissing => Get("Settings_GameConfigStatusMissing");

	public static string Settings_GameConfigStatusBroken => Get("Settings_GameConfigStatusBroken");

	public static string Settings_GameConfigStatusRepaired => Get("Settings_GameConfigStatusRepaired");

	public static string Settings_GameConfigStatusOffline => Get("Settings_GameConfigStatusOffline");

	public static string Settings_GameConfigServerMissing => Get("Settings_GameConfigServerMissing");

	public static string Settings_GameConfigPathLabel => Get("Settings_GameConfigPathLabel");

	public static string Settings_GameConfigOpenFolderButton => Get("Settings_GameConfigOpenFolderButton");

	public static string Settings_GameConfigStartupRepairedFormat => Get("Settings_GameConfigStartupRepairedFormat");

	public static string Settings_GameConfigNotCheckedYet => Get("Settings_GameConfigNotCheckedYet");

	public static string Settings_MemoryHardwareSection => Get("Settings_MemoryHardwareSection");

	public static string Settings_MemorySlotSummaryFormat => Get("Settings_MemorySlotSummaryFormat");

	public static string Settings_MemoryModuleFormat => Get("Settings_MemoryModuleFormat");

	public static string Settings_MemoryNoModule => Get("Settings_MemoryNoModule");

	public static string Settings_MemoryRefreshButton => Get("Settings_MemoryRefreshButton");

	public static string Settings_MemoryLimitLabel => Get("Settings_MemoryLimitLabel");

	public static string Settings_MemoryLimitDescription => Get("Settings_MemoryLimitDescription");

	public static string Settings_MemoryLimitAppliedFormat => Get("Settings_MemoryLimitAppliedFormat");

	public static string Settings_MemoryLimitFailedFormat => Get("Settings_MemoryLimitFailedFormat");

	public static string Settings_MemoryTotalSummaryFormat => Get("Settings_MemoryTotalSummaryFormat");

	public static string Settings_MemoryModuleDetailFormat => Get("Settings_MemoryModuleDetailFormat");

	public static string Settings_MemoryUnknownValue => Get("Settings_MemoryUnknownValue");
	public static string Home_VersionOfficialSuffix => Get("Home_VersionOfficialSuffix");

	public static string GameSettings_SpecialVersionUnsupportedNotice => Get("GameSettings_SpecialVersionUnsupportedNotice");

	public static string Settings_GameHealthRepairing => Get("Settings_GameHealthRepairing");

	public static string Settings_GameHealthCheckedFormat => Get("Settings_GameHealthCheckedFormat");

	public static string Settings_GameHealthProblemFormat => Get("Settings_GameHealthProblemFormat");

	public static string Settings_GameHealthStatusFixed => Get("Settings_GameHealthStatusFixed");

	public static string Settings_GameHealthStatusNeedSteam => Get("Settings_GameHealthStatusNeedSteam");

	public static string Settings_GameHealthStatusNotApplicable => Get("Settings_GameHealthStatusNotApplicable");

	public static string Settings_GameHealthStatusFailed => Get("Settings_GameHealthStatusFailed");

	public static string Settings_RuntimeSection => Get("Settings_RuntimeSection");

	public static string Settings_RuntimeDescription => Get("Settings_RuntimeDescription");

	public static string Settings_RuntimeGameStatusLabel => Get("Settings_RuntimeGameStatusLabel");

	public static string Settings_RuntimeRunningFormat => Get("Settings_RuntimeRunningFormat");

	public static string Settings_RuntimeNotRunning => Get("Settings_RuntimeNotRunning");

	public static string Settings_RuntimePlaytimeLabel => Get("Settings_RuntimePlaytimeLabel");

	public static string Settings_RuntimePlaytimeFormat => Get("Settings_RuntimePlaytimeFormat");

	public static string Settings_RuntimePlaytimeNone => Get("Settings_RuntimePlaytimeNone");

	public static string Settings_RuntimeRefreshButton => Get("Settings_RuntimeRefreshButton");

	public static string Settings_RuntimeEndGameButton => Get("Settings_RuntimeEndGameButton");

	public static string Settings_RuntimeEndGameConfirmButton => Get("Settings_RuntimeEndGameConfirmButton");

	public static string Settings_RuntimeEndGameWarning => Get("Settings_RuntimeEndGameWarning");

	public static string Settings_RuntimeEndedFormat => Get("Settings_RuntimeEndedFormat");

	public static string Settings_RuntimeNothingToEnd => Get("Settings_RuntimeNothingToEnd");

	public static string Settings_RuntimeEndFailedFormat => Get("Settings_RuntimeEndFailedFormat");

	public static string Settings_RuntimeGameNotFound => Get("Settings_RuntimeGameNotFound");

	public static string Settings_DiagnosticsSection => Get("Settings_DiagnosticsSection");

	public static string Settings_DiagnosticsDescription => Get("Settings_DiagnosticsDescription");

	public static string Settings_DiagnosticsExportButton => Get("Settings_DiagnosticsExportButton");

	public static string Settings_DiagnosticsOpenFolderButton => Get("Settings_DiagnosticsOpenFolderButton");

	public static string Settings_DiagnosticsExporting => Get("Settings_DiagnosticsExporting");

	public static string Settings_DiagnosticsNotExportedYet => Get("Settings_DiagnosticsNotExportedYet");

	public static string Settings_DiagnosticsLastExportFormat => Get("Settings_DiagnosticsLastExportFormat");

	public static string Settings_DiagnosticsExportedFormat => Get("Settings_DiagnosticsExportedFormat");

	public static string Settings_DiagnosticsFailedFormat => Get("Settings_DiagnosticsFailedFormat");

	public static string Home_EndGameButton => Get("Home_EndGameButton");

	public static string Home_EndGameConfirmButton => Get("Home_EndGameConfirmButton");

	public static string Home_GameRunningFormat => Get("Home_GameRunningFormat");

	public static string Home_GameRunningOnly => Get("Home_GameRunningOnly");

	public static string Home_PlaytimeSummaryFormat => Get("Home_PlaytimeSummaryFormat");

	public static string Time_HoursMinutesFormat => Get("Time_HoursMinutesFormat");

	public static string Time_MinutesFormat => Get("Time_MinutesFormat");

	public static string Time_SecondsFormat => Get("Time_SecondsFormat");

	public static string GameSettings_IconBeamNgLogo => Get("GameSettings_IconBeamNgLogo");

	public static string GameSettings_IconStartRideLogo => Get("GameSettings_IconStartRideLogo");

	public static string Settings_LaunchBehaviorSection => Get("Settings_LaunchBehaviorSection");

	public static string Settings_LaunchBehaviorDescription => Get("Settings_LaunchBehaviorDescription");

	public static string Settings_LaunchCheckFilesLabel => Get("Settings_LaunchCheckFilesLabel");

	public static string Settings_LaunchSkipMenuLabel => Get("Settings_LaunchSkipMenuLabel");

	public static string Settings_LaunchSkipMenuHint => Get("Settings_LaunchSkipMenuHint");

	public static string Settings_LaunchFullScreenLabel => Get("Settings_LaunchFullScreenLabel");

	public static string Settings_LaunchFullScreenHint => Get("Settings_LaunchFullScreenHint");

	public static string Settings_LaunchMinimizeLauncherLabel => Get("Settings_LaunchMinimizeLauncherLabel");

	public static string Settings_LaunchCloseToTrayLabel => Get("Settings_LaunchCloseToTrayLabel");

	public static string Settings_LaunchCloseToTrayHint => Get("Settings_LaunchCloseToTrayHint");

	public static string Settings_LaunchAutoInstallModLabel => Get("Settings_LaunchAutoInstallModLabel");

	public static string Settings_LaunchForceGpuLabel => Get("Settings_LaunchForceGpuLabel");

	public static string Settings_LaunchForceGpuHint => Get("Settings_LaunchForceGpuHint");

	public static string Settings_LaunchGfxLabel => Get("Settings_LaunchGfxLabel");

	public static string Settings_LaunchGfxDefault => Get("Settings_LaunchGfxDefault");

	public static string Settings_LaunchGfxDx11 => Get("Settings_LaunchGfxDx11");

	public static string Settings_LaunchGfxD3D12 => Get("Settings_LaunchGfxD3D12");

	public static string Settings_LaunchGfxVulkan => Get("Settings_LaunchGfxVulkan");

	public static string Settings_LaunchPhysicsFpsLabel => Get("Settings_LaunchPhysicsFpsLabel");

	public static string Settings_LaunchPhysicsFpsHint => Get("Settings_LaunchPhysicsFpsHint");

	public static string Settings_LaunchExtraArgsLabel => Get("Settings_LaunchExtraArgsLabel");

	public static string Settings_LaunchExtraArgsHint => Get("Settings_LaunchExtraArgsHint");

	public static string Settings_LaunchArgsPreviewLabel => Get("Settings_LaunchArgsPreviewLabel");

	public static string Settings_LaunchArgsPreviewNone => Get("Settings_LaunchArgsPreviewNone");

	public static string Settings_LaunchArgsRefreshButton => Get("Settings_LaunchArgsRefreshButton");

	public static string Settings_LaunchPreLaunchCommandLabel => Get("Settings_LaunchPreLaunchCommandLabel");

	public static string Settings_LaunchPreLaunchCommandHint => Get("Settings_LaunchPreLaunchCommandHint");

	public static string Settings_LaunchWaitPreLaunchLabel => Get("Settings_LaunchWaitPreLaunchLabel");

	public static string Settings_LaunchPostExitCommandLabel => Get("Settings_LaunchPostExitCommandLabel");

	public static string Settings_LaunchPostExitCommandHint => Get("Settings_LaunchPostExitCommandHint");

	public static string Settings_StartupSection => Get("Settings_StartupSection");

	public static string Settings_StartupDescription => Get("Settings_StartupDescription");

	public static string Settings_StartupAutoDetectLabel => Get("Settings_StartupAutoDetectLabel");

	public static string Settings_StartupAutoUpdateLabel => Get("Settings_StartupAutoUpdateLabel");

	public static string Settings_StartupAutoCheckVehicleModsLabel => Get("Settings_StartupAutoCheckVehicleModsLabel");

	public static string Settings_GameLogSection => Get("Settings_GameLogSection");

	public static string Settings_GameLogDescription => Get("Settings_GameLogDescription");

	public static string Settings_GameLogNotScanned => Get("Settings_GameLogNotScanned");

	public static string Settings_GameLogNotFound => Get("Settings_GameLogNotFound");

	public static string Settings_GameLogSummaryFormat => Get("Settings_GameLogSummaryFormat");

	public static string Settings_GameLogDetailFormat => Get("Settings_GameLogDetailFormat");

	public static string Settings_GameLogFailedFormat => Get("Settings_GameLogFailedFormat");

	public static string Settings_GameLogRefreshButton => Get("Settings_GameLogRefreshButton");

	public static string Settings_GameLogOpenFolderButton => Get("Settings_GameLogOpenFolderButton");

	public static string Settings_GameLogOpenFileButton => Get("Settings_GameLogOpenFileButton");

	public static string Settings_GameLogCopyButton => Get("Settings_GameLogCopyButton");

	public static string Settings_GameLogNoProblemToCopy => Get("Settings_GameLogNoProblemToCopy");

	public static string Settings_GameLogCopiedFormat => Get("Settings_GameLogCopiedFormat");

	public static string Settings_GameLogCopyFailed => Get("Settings_GameLogCopyFailed");

	public static string Settings_BackupSection => Get("Settings_BackupSection");

	public static string Settings_BackupDescription => Get("Settings_BackupDescription");

	public static string Settings_BackupCreateButton => Get("Settings_BackupCreateButton");

	public static string Settings_BackupRestoreButton => Get("Settings_BackupRestoreButton");

	public static string Settings_BackupDeleteButton => Get("Settings_BackupDeleteButton");

	public static string Settings_BackupRefreshButton => Get("Settings_BackupRefreshButton");

	public static string Settings_BackupOpenFolderButton => Get("Settings_BackupOpenFolderButton");

	public static string Settings_BackupNotCreatedYet => Get("Settings_BackupNotCreatedYet");

	public static string Settings_BackupWorking => Get("Settings_BackupWorking");

	public static string Settings_BackupRestoring => Get("Settings_BackupRestoring");

	public static string Settings_BackupCountFormat => Get("Settings_BackupCountFormat");

	public static string Settings_BackupItemFormat => Get("Settings_BackupItemFormat");

	public static string Settings_BackupCreatedFormat => Get("Settings_BackupCreatedFormat");

	public static string Settings_BackupFailedFormat => Get("Settings_BackupFailedFormat");

	public static string Settings_BackupDeleted => Get("Settings_BackupDeleted");

	public static string Settings_BackupDeleteFailed => Get("Settings_BackupDeleteFailed");

	public static string Settings_BackupRestoreConfirmTitle => Get("Settings_BackupRestoreConfirmTitle");

	public static string Settings_BackupRestoreConfirmMessage => Get("Settings_BackupRestoreConfirmMessage");

	public static string Settings_BackupDeleteConfirmTitle => Get("Settings_BackupDeleteConfirmTitle");

	public static string Settings_BackupDeleteConfirmMessage => Get("Settings_BackupDeleteConfirmMessage");

	public static string Settings_StorageSection => Get("Settings_StorageSection");

	public static string Settings_StorageDescription => Get("Settings_StorageDescription");

	public static string Settings_StorageNotScanned => Get("Settings_StorageNotScanned");

	public static string Settings_StorageSummaryFormat => Get("Settings_StorageSummaryFormat");

	public static string Settings_StorageFailedFormat => Get("Settings_StorageFailedFormat");

	public static string Settings_StorageScanButton => Get("Settings_StorageScanButton");

	public static string Settings_StorageCleanButton => Get("Settings_StorageCleanButton");

	public static string Settings_StorageCleanLogsButton => Get("Settings_StorageCleanLogsButton");

	public static string Settings_StorageOpenFolderButton => Get("Settings_StorageOpenFolderButton");

	public static string Settings_StorageFolderMissing => Get("Settings_StorageFolderMissing");

	public static string Settings_StorageCleanConfirmTitle => Get("Settings_StorageCleanConfirmTitle");

	public static string Settings_StorageCleanConfirmMessage => Get("Settings_StorageCleanConfirmMessage");

	public static string Settings_RelaySection => Get("Settings_RelaySection");

	public static string Settings_RelayDescription => Get("Settings_RelayDescription");

	public static string Settings_RelayNotTested => Get("Settings_RelayNotTested");

	public static string Settings_RelayTesting => Get("Settings_RelayTesting");

	public static string Settings_RelayTestButton => Get("Settings_RelayTestButton");

	public static string Settings_RelayResultFormat => Get("Settings_RelayResultFormat");

	public static string Settings_RelayAllUnreachable => Get("Settings_RelayAllUnreachable");

	public static string Settings_RelayFailedFormat => Get("Settings_RelayFailedFormat");

	public static string Settings_RelayLastResultFormat => Get("Settings_RelayLastResultFormat");
	public static string Settings_DeveloperLabel => Get("Settings_DeveloperLabel");
	public static string Settings_DeveloperValue => Get("Settings_DeveloperValue");
	public static string Settings_ProjectHomeLabel => Get("Settings_ProjectHomeLabel");
	public static string Settings_ProjectHomeValue => Get("Settings_ProjectHomeValue");
	public static string Settings_ViewProjectHomeButton => Get("Settings_ViewProjectHomeButton");
	public static string Settings_DerivationNotice => Get("Settings_DerivationNotice");
	public static string Settings_RuntimeSectionHint => Get("Settings_RuntimeSectionHint");
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using StartRide.Core;

namespace StartRide.App.Services;

public sealed class StartRideLobbyService : IMultiplayerLobbyService, IDisposable
{
	private readonly AppSettings settings = AppState.Current.Settings;

	private readonly ApiService api = new();

	private readonly MultiplayerSession session;

	private readonly IUiDispatcher uiDispatcher;

	private readonly Timer heartbeatTimer;

	private MultiplayerLobbySnapshot? current;

	private string playerName = "Player";

	private string roomName = "";

	private bool isHost;

	private bool disposed;

	private const int DisconnectGraceSeconds = 30;

	private Timer? disconnectGraceTimer;

	private volatile bool inDisconnectGrace;

	public MultiplayerLobbySnapshot? Current => current;

	public event Action<MultiplayerLobbySnapshot>? SnapshotChanged;

	public event Action<MultiplayerLobbyStopped>? Stopped;

	public StartRideLobbyService(IUiDispatcher uiDispatcher)
	{
		this.uiDispatcher = uiDispatcher;

		session = new MultiplayerSession(settings);
		session.StateChanged += OnSessionStateChanged;
		session.Notice += OnSessionNotice;
		session.Log += PluginLog;

		try
		{
			session.StartBridge();
		}
		catch (Exception exception)
		{
			PluginLog("本地桥启动失败：" + exception.Message);
		}

		heartbeatTimer = new Timer(OnHeartbeat, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
	}

	public async Task<MultiplayerLobbySnapshot> CreateHostAsync(string hostName, CancellationToken cancellationToken = default)
	{
		playerName = CleanName(hostName);
		isHost = true;
		roomName = playerName + " 的房间";

		// 房主在建房页选的地图和出生点：地图进房间元数据（其他人加入时据此跟着进同一张图），
		// 出生点只发给本地模组（每个人落点可以不同，没必要也不应该让别人的出生点被别人决定）。
		string mapId = ResolveMapId(settings.LobbyMapId);
		string spawnPoint = ResolveSpawnPoint(mapId, settings.LobbySpawnPoint);

		var code = NewRoomCode();
		var room = new Room
		{
			Id = code,
			Name = roomName,
			Host = playerName,
			Map = mapId,
			Mode = "freeroam",
			Capacity = 8,
			Players = 1,
			Live = true,
		};

		cancellationToken.ThrowIfCancellationRequested();

		await EnsureGameReadyAsync().ConfigureAwait(false);

		cancellationToken.ThrowIfCancellationRequested();

		// 必须在入房之前设定：入房成功时那条桥消息就带着地图与出生点一起发出去
		session.ConfigureRoom(mapId, spawnPoint, push: false);

		bool connected = await session.CreateRoomAsync(room, playerName).ConfigureAwait(false);
		if (!connected)
		{
			throw new MultiplayerLobbyCreationException(
				MultiplayerLobbyCreationFailure.RoomConnectionFailed,
				"连不上联机中继（" + DescribeError(session.State.LastError) + "）");
		}

		PersistLobbyChoice(mapId, spawnPoint);
		MultiplayerSession.WriteGameBootstrap(playerName, code, roomName, mapId, spawnPoint);
		StartRideMultiplayerRuntime.RoomMapId = mapId;

		PluginLog($"房主已选地图 {mapId}（{BeamNgLevelCatalog.DisplayName(settings, mapId)}），出生点 {(spawnPoint.Length > 0 ? spawnPoint : "游戏默认")}");

		try
		{
			await api.CreateRoomAsync(room).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			PluginLog("房间登记到大厅失败（不影响房间码联机）：" + exception.Message);
		}

		await LaunchGameIfNeededAsync(mapId).ConfigureAwait(false);

		return Publish(MultiplayerLobbyState.Active);
	}

	public async Task<MultiplayerLobbySnapshot> JoinAsync(string roomCode, string playerName, CancellationToken cancellationToken = default)
	{
		this.playerName = CleanName(playerName);
		isHost = false;

		string code = NormalizeRoomCode(roomCode);
		if (code.Length < 4)
		{
			throw new MultiplayerLobbyCreationException(
				MultiplayerLobbyCreationFailure.InvalidRoomCode, "房间码格式不正确。");
		}

		await EnsureGameReadyAsync().ConfigureAwait(false);

		cancellationToken.ThrowIfCancellationRequested();

		var room = new Room { Id = code, Name = code, Capacity = 8 };

		// 房主选的地图只存在大厅列表里（中继不带 map），所以这里读不到就等于不知道该进哪张图。
		// GetRoomsAsync 出错时会静默返回空列表，因此多试两次，失败才退化。
		try
		{
			room = await LookupRoomAsync(code).ConfigureAwait(false) ?? room;
		}
		catch (Exception exception)
		{
			PluginLog("读取大厅房间列表失败，改为凭房间码直连：" + exception.Message);
		}

		// 地图以房主那张为准（大厅列表里读到的 room.Map）；出生点是本机自己选的，
		// 只要这张图里确实有它就用，没有就退回该图默认出生点。
		string mapId = ResolveMapId(room.Map);
		string spawnPoint = ResolveSpawnPoint(mapId, settings.LobbySpawnPoint);

		session.ConfigureRoom(mapId, spawnPoint, push: false);

		bool connected = await session.JoinRoomAsync(room, this.playerName).ConfigureAwait(false);
		if (!connected)
		{
			throw new MultiplayerLobbyCreationException(
				MultiplayerLobbyCreationFailure.RoomConnectionFailed,
				"连不上联机中继（" + DescribeError(session.State.LastError) + "）");
		}

		roomName = room.Name;

		PersistLobbyChoice(mapId, spawnPoint);
		MultiplayerSession.WriteGameBootstrap(this.playerName, room.Id, roomName, mapId, spawnPoint);
		StartRideMultiplayerRuntime.RoomMapId = mapId;

		PluginLog($"已进入房主的图 {mapId}（{BeamNgLevelCatalog.DisplayName(settings, mapId)}），我的出生点 {(spawnPoint.Length > 0 ? spawnPoint : "游戏默认")}");

		try
		{
			await api.JoinRoomAsync(room.Id, this.playerName).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			PluginLog("大厅登记加入失败（不影响联机）：" + exception.Message);
		}

		await LaunchGameIfNeededAsync(mapId).ConfigureAwait(false);

		return Publish(MultiplayerLobbyState.Active);
	}

	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		string code = current?.RoomCode ?? "";
		if (isHost && code.Length > 0)
		{

			try
			{
				session.CloseRoomOnRelay();
			}
			catch (Exception exception2)
			{
				PluginLog("通知中继关房失败（不影响本机退出）：" + exception2.Message);
			}

			try
			{
				await api.CloseRoomAsync(code, playerName).ConfigureAwait(false);
			}
			catch
			{
			}
		}
		else if (code.Length > 0)
		{
			try
			{
				await api.LeaveRoomAsync(code, playerName).ConfigureAwait(false);
			}
			catch
			{
			}
		}

		await session.LeaveRoomAsync().ConfigureAwait(false);
		isHost = false;
		current = null;

		CancelDisconnectGrace();
		StartRideMultiplayerRuntime.IsInRoom = false;
		StartRideMultiplayerRuntime.RoomMapId = "";
		MultiplayerSession.ClearGameBootstrap();
		RemoveModIfNeeded();
		RaiseStopped(MultiplayerLobbyStopReason.UserRequested);
	}

	private async Task EnsureGameReadyAsync()
	{
		await RepairGameConfigAsync().ConfigureAwait(false);

		if (string.IsNullOrWhiteSpace(settings.GameDirectory) || !Directory.Exists(settings.GameDirectory))
		{
			string detected = AppSettings.DetectGameDirectory();
			if (detected.Length > 0)
			{
				settings.GameDirectory = detected;
				settings.Save();
				PluginLog("自动检测到 BeamNG.drive：" + detected);
			}
		}

		if (!AppState.Current.Launcher.IsInstalled)
		{
			throw new MultiplayerLobbyCreationException(
				MultiplayerLobbyCreationFailure.MinecraftWorldUnavailable,
				"未找到 BeamNG.drive 安装");
		}

		ModInstaller installer = AppState.Current.ModInstaller;
		bool wasInstalled = installer.IsInstalled;
		string? error = installer.Install();
		if (error != null)
		{
			throw new MultiplayerLobbyCreationException(
				MultiplayerLobbyCreationFailure.TerracottaProtocolFailed,
				"安装联机模组失败：" + error);
		}

		if (!wasInstalled && ModInstaller.IsGameRunning())
		{
			PluginLog("联机模组是在游戏启动之后才装入的，本局需要重启游戏才会加载");
			try
			{
				AppState.Current.Notify("联机模组已安装，但游戏已经开着 —— 请退出游戏重进一次，否则看不到其他玩家。");
			}
			catch
			{
			}
		}
	}

	private async Task RepairGameConfigAsync()
	{
		try
		{
			if (!settings.AutoRepairGameConfig)
			{
				return;
			}
			var service = new ConfigRepairService(settings);
			var result = await service.RepairAsync(new ApiService(), PluginLog).ConfigureAwait(false);
			if (result.Repaired > 0)
			{
				PluginLog("联机前已补全 " + result.Repaired + " 个游戏配置文件");
			}
			else if (result.Error != null)
			{
				PluginLog("配置文件检查跳过：" + result.Error);
			}
		}
		catch (Exception exception)
		{
			PluginLog("配置文件检查异常：" + exception.Message);
		}
	}

	private async Task LaunchGameIfNeededAsync(string? mapId)
	{
		if (!settings.AutoLaunchGameOnLobby)
		{
			return;
		}

		await RepairGameConfigAsync().ConfigureAwait(false);

		try
		{
			GameLauncher launcher = AppState.Current.Launcher;
			if (launcher.IsRunning)
			{
				PluginLog("BeamNG.drive 已在运行，跳过自动启动");
				return;
			}

			string? error = launcher.Launch(withMod: false, levelId: mapId);
			PluginLog(error == null
				? "已自动启动 BeamNG.drive 并直接进入房间地图"
				: "自动启动游戏失败：" + error);
		}
		catch (Exception exception)
		{
			PluginLog("自动启动游戏异常：" + exception.Message);
		}
	}

	/// <summary>
	/// 按房间码在大厅列表里找房间（要拿到房主那张图）。
	/// 只找「已经带上 map」的房间：中途刚建好的房间可能 map 还是空的，等一小会儿再试。
	/// 试满 3 次仍拿不到就返回 null，交给 ResolveMapId 用它自己的兜底。
	/// </summary>
	private async Task<Room?> LookupRoomAsync(string code)
	{
		Room? best = null;
		for (int attempt = 0; attempt < 3; attempt++)
		{
			if (attempt > 0)
			{
				await Task.Delay(400 * attempt).ConfigureAwait(false);
			}

			var rooms = await api.GetRoomsAsync().ConfigureAwait(false);
			var hit = rooms.FirstOrDefault(r => string.Equals(r.Id, code, StringComparison.OrdinalIgnoreCase));
			if (hit == null)
			{
				continue;
			}

			best = hit;
			if (!string.IsNullOrWhiteSpace(hit.Map))
			{
				return hit;
			}
		}

		if (best != null)
		{
			PluginLog("大厅里读到了房间 " + code + "，但它没有带地图信息");
		}
		else
		{
			PluginLog("大厅列表里没有房间 " + code + "（房主可能没登记成功）");
		}
		return best;
	}

	/// <summary>
	/// 地图 id 兜底：空值或游戏目录里根本没有这张图时，退回一张确实存在的图，
	/// 否则 -level 会把游戏带到一个不存在的关卡上（黑屏/回主菜单）。
	/// </summary>
	private string ResolveMapId(string? wanted)
	{
		string id = (wanted ?? "").Trim();
		if (id.Length > 0 && BeamNgLevelCatalog.Find(settings, id) != null)
		{
			return id;
		}

		if (id.Length > 0)
		{
			PluginLog("地图 " + id + " 在本机找不到，改用默认地图");
		}

		var fallback = BeamNgLevelCatalog.Find(settings, "west_coast_usa")
			?? BeamNgLevelCatalog.Load(settings).FirstOrDefault(l => l.SpawnPoints.Count > 0);
		return fallback?.Id ?? "west_coast_usa";
	}

	/// <summary>出生点兜底：不在本图里就用该图的默认出生点（空 = 交给游戏自己决定）。</summary>
	private string ResolveSpawnPoint(string mapId, string? wanted)
	{
		string sp = (wanted ?? "").Trim();
		if (sp.Length > 0 && BeamNgLevelCatalog.HasSpawnPoint(settings, mapId, sp))
		{
			return sp;
		}

		var level = BeamNgLevelCatalog.Find(settings, mapId);
		if (level == null || level.SpawnPoints.Count == 0)
		{
			return "";
		}

		var pick = level.SpawnPoints.FirstOrDefault(s => s.IsDefault) ?? level.SpawnPoints[0];
		return pick.ObjectName;
	}

	/// <summary>把这一局选的地图/出生点记到设置里，下次建房直接沿用。</summary>
	private void PersistLobbyChoice(string mapId, string spawnPoint)
	{
		try
		{
			settings.LobbyMapId = mapId;
			settings.LobbySpawnPoint = spawnPoint;
			settings.Save();
		}
		catch (Exception exception)
		{
			PluginLog("记住上次的联机地图选择失败：" + exception.Message);
		}
	}

	private void RemoveModIfNeeded()
	{
		try
		{
			AppState.Current.ModInstaller.RemoveAfterSession();
		}
		catch (Exception exception)
		{
			PluginLog("移除联机模组失败（不影响退出）：" + exception.Message);
		}
	}

	private void OnSessionStateChanged()
	{
		if (disposed)
		{
			return;
		}

		var state = session.State;
		WriteStateFile();

		StartRideMultiplayerRuntime.IsInRoom = state.Connected || inDisconnectGrace;

		if (!state.Connected)
		{

			if (current != null)
			{
				BeginDisconnectGrace();
			}
			return;
		}

		CancelDisconnectGrace();

		if (current == null)
		{
			return;
		}

		var snapshot = Publish(MultiplayerLobbyState.Active);
		RaiseSnapshotChanged(snapshot);
	}

	private void BeginDisconnectGrace()
	{
		if (inDisconnectGrace)
		{
			return;
		}
		inDisconnectGrace = true;
		PluginLog($"中继断开，{DisconnectGraceSeconds} 秒内未恢复才判定房间结束");
		try
		{
			disconnectGraceTimer ??= new Timer(_ => OnDisconnectGraceExpired(), null, Timeout.Infinite, Timeout.Infinite);
			disconnectGraceTimer.Change(TimeSpan.FromSeconds(DisconnectGraceSeconds), Timeout.InfiniteTimeSpan);
		}
		catch (Exception exception)
		{
			PluginLog("断线宽限计时器启动失败：" + exception.Message);
		}
	}

	private void CancelDisconnectGrace()
	{
		if (!inDisconnectGrace)
		{
			return;
		}
		inDisconnectGrace = false;
		try
		{
			disconnectGraceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
		}
		catch
		{
		}
		PluginLog("中继已恢复，房间继续");
	}

	private void OnDisconnectGraceExpired()
	{
		if (disposed || !inDisconnectGrace)
		{
			return;
		}
		inDisconnectGrace = false;

		if (current == null)
		{
			return;
		}

		PluginLog($"中继 {DisconnectGraceSeconds} 秒内未恢复，判定房间已结束");
		current = null;
		RemoveModIfNeeded();
		_ = session.LeaveRoomAsync();
		StartRideMultiplayerRuntime.IsInRoom = false;
		StartRideMultiplayerRuntime.RoomMapId = "";
		MultiplayerSession.ClearGameBootstrap();
		RaiseStopped(MultiplayerLobbyStopReason.TerracottaServiceFailed);
	}

	private void OnSessionNotice(string level, string text)
	{
		PluginLog($"[{level}] {text}");
	}

	private void OnHeartbeat(object? _)
	{
		string code = current?.RoomCode ?? "";
		if (!isHost || code.Length == 0)
		{
			return;
		}

		_ = Task.Run(async () =>
		{
			try
			{
				await api.RoomHeartbeatAsync(code).ConfigureAwait(false);
			}
			catch
			{
			}
		});
	}

	private MultiplayerLobbySnapshot Publish(MultiplayerLobbyState state)
	{
		var state2 = session.State;
		var names = state2.Players.Count > 0
			? new List<string>(state2.Players)
			: new List<string> { playerName };

		var players = new List<MultiplayerLobbyPlayer>(names.Count);
		foreach (var name in names)
		{
			bool isRoomHost = state2.Host.Length > 0
				? string.Equals(name, state2.Host, StringComparison.OrdinalIgnoreCase)
				: name == names[0];

			players.Add(new MultiplayerLobbyPlayer(
				name,
				name,
				"StartRide",
				isRoomHost ? MultiplayerLobbyPlayerKind.Host : MultiplayerLobbyPlayerKind.Guest,
				null,
				string.Equals(name, playerName, StringComparison.OrdinalIgnoreCase)));
		}

		if (players.Count == 0)
		{
			players.Add(new MultiplayerLobbyPlayer(
				playerName, playerName, "StartRide",
				MultiplayerLobbyPlayerKind.Host, null, true));
		}

		string roomCode = state2.RoomId.Length > 0 ? state2.RoomId : (current?.RoomCode ?? "");
		if (string.IsNullOrEmpty(roomName) && state2.RoomName.Length > 0)
		{
			roomName = state2.RoomName;
		}

		current = new MultiplayerLobbySnapshot(roomCode, state, players);
		WriteStateFile();
		return current;
	}

	private void RaiseSnapshotChanged(MultiplayerLobbySnapshot snapshot)
	{
		if (uiDispatcher.HasAccess)
		{
			SnapshotChanged?.Invoke(snapshot);
		}
		else
		{
			uiDispatcher.Post(() => SnapshotChanged?.Invoke(snapshot));
		}
	}

	private void RaiseStopped(MultiplayerLobbyStopReason reason)
	{
		var stopped = new MultiplayerLobbyStopped(reason);
		if (uiDispatcher.HasAccess)
		{
			Stopped?.Invoke(stopped);
		}
		else
		{
			uiDispatcher.Post(() => Stopped?.Invoke(stopped));
		}
	}

	private static string NewRoomCode()
	{
		const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		var chars = new char[4];
		for (int i = 0; i < chars.Length; i++)
		{
			chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
		}
		return "SR" + new string(chars);
	}

	private static string NormalizeRoomCode(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return "";
		}

		var buf = new System.Text.StringBuilder();
		foreach (char ch in raw)
		{
			if (char.IsLetterOrDigit(ch))
			{
				buf.Append(char.ToUpperInvariant(ch));
			}
			else if (buf.Length > 0)
			{
				break;
			}
		}

		return buf.ToString();
	}

	private static string CleanName(string? name) =>
		string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();

	private static string DescribeError(string error) =>
		string.IsNullOrWhiteSpace(error) ? "无响应" : error;

	private static void PluginLog(string message)
	{
		try
		{
			System.Diagnostics.Debug.WriteLine("[StartRide] " + message);
		}
		catch
		{
		}

		try
		{
			AppState.Current.Log("[联机] " + message);
		}
		catch
		{
		}
	}

	private static readonly string StateFilePath = System.IO.Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"StartRide", "lobby-state.json");

	private void WriteStateFile()
	{
		try
		{
			var state = session.State;
			var payload = new
			{
				roomId = state.RoomId,
				roomName = state.RoomName,
				host = state.Host,
				connected = state.Connected,
				transport = state.Transport,
				gameConnected = state.GameConnected,
				playerCount = state.PlayerCount,
				capacity = state.Capacity,
				players = state.Players,
				remoteVehicles = state.RemoteVehicles,
				vehiclePackets = state.VehiclePackets,
				lastError = state.LastError,
				map = session.MapId,
				spawnPoint = session.SpawnPoint,
				updatedAt = DateTimeOffset.Now.ToString("o"),
			};

			string dir = System.IO.Path.GetDirectoryName(StateFilePath)!;
			System.IO.Directory.CreateDirectory(dir);
			System.IO.File.WriteAllText(
				StateFilePath,
				System.Text.Json.JsonSerializer.Serialize(payload,
					new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		inDisconnectGrace = false;
		heartbeatTimer.Dispose();
		try
		{
			disconnectGraceTimer?.Dispose();
		}
		catch
		{
		}
		session.StateChanged -= OnSessionStateChanged;
		session.Notice -= OnSessionNotice;
		session.Dispose();
	}
}

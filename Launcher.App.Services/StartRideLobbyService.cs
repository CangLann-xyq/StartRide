using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using StartRide.Core;

namespace Launcher.App.Services;

/// <summary>
/// 把 StartRide 联机页（创建房间 / 加入房间 / 房间码 / 玩家列表）接到 StartRide 的联机后端。
///
/// StartRide 原版的 IMultiplayerLobbyService 是 Terracotta（Minecraft 局域网穿透），
/// 这里换成基于自建中继的实现：
///   创建房间 -> 生成本地房间码 -> 连中继 -> 顺带把房间登记到大厅（失败不阻断）
///   加入房间 -> 凭房间码直连中继（不依赖大厅）
/// 因此即使 HTTP 后端不可用，只要中继活着，房间码联机依然可用。
///
/// 界面层（MultiplayerPageView / MultiplayerPageViewModel）与 StartRide 原版完全一致，
/// 只换这一个实现即可，不需要改动任何 XAML。
/// </summary>
public sealed class StartRideLobbyService : IMultiplayerLobbyService, IDisposable
{
	/// <summary>与全局单例共用同一份设置：联机流程里自动探测到的游戏目录要立刻对界面生效。</summary>
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

	public MultiplayerLobbySnapshot? Current => current;

	public event Action<MultiplayerLobbySnapshot>? SnapshotChanged;

	public event Action<MultiplayerLobbyStopped>? Stopped;

	public StartRideLobbyService(IUiDispatcher uiDispatcher)
	{
		this.uiDispatcher = uiDispatcher;

		session = new MultiplayerSession(settings);
		session.StateChanged += OnSessionStateChanged;
		session.Notice += OnSessionNotice;

		// 启动本地桥：游戏内模组走 127.0.0.1:4444 接到这里，再经中继转发出去。
		try
		{
			session.StartBridge();
		}
		catch (Exception exception)
		{
			PluginLog("本地桥启动失败：" + exception.Message);
		}

		// 房主定期向大厅续期，避免房间被后端判定为离线。
		heartbeatTimer = new Timer(OnHeartbeat, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
	}

	// ==================== IMultiplayerLobbyService ====================

	public async Task<MultiplayerLobbySnapshot> CreateHostAsync(string hostName, CancellationToken cancellationToken = default)
	{
		playerName = CleanName(hostName);
		isHost = true;
		roomName = playerName + " 的房间";

		var code = NewRoomCode();
		var room = new Room
		{
			Id = code,
			Name = roomName,
			Host = playerName,
			Map = "west_coast_usa",
			Mode = "freeroam",
			Capacity = 8,
			Players = 1,
			Live = true,
		};

		cancellationToken.ThrowIfCancellationRequested();

		// 中继是联机唯一必要条件。
		bool connected = await session.CreateRoomAsync(room, playerName).ConfigureAwait(false);
		if (!connected)
		{
			throw new MultiplayerLobbyCreationException(
				MultiplayerLobbyCreationFailure.RoomConnectionFailed,
				"连不上联机中继（" + DescribeError(session.State.LastError) + "）");
		}

		// 大厅登记是锦上添花，失败不影响联机。
		try
		{
			await api.CreateRoomAsync(room).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			PluginLog("房间登记到大厅失败（不影响房间码联机）：" + exception.Message);
		}

		// 房间已就绪，直接把游戏拉起来进房（可在设置里关闭）。
		await LaunchGameIfNeededAsync().ConfigureAwait(false);

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

		// 加入方同样要先备好游戏环境（装联机模组），否则进了房间也看不到别人的车。
		await EnsureGameReadyAsync().ConfigureAwait(false);

		cancellationToken.ThrowIfCancellationRequested();

		// 先尝试从大厅补全房间信息（房主名 / 容量 / 地图）；取不到也能凭码直连。
		var room = new Room { Id = code, Name = code, Capacity = 8 };
		try
		{
			var rooms = await api.GetRoomsAsync().ConfigureAwait(false);
			var hit = rooms.FirstOrDefault(r => string.Equals(r.Id, code, StringComparison.OrdinalIgnoreCase));
			if (hit != null)
			{
				room = hit;
			}
		}
		catch (Exception exception)
		{
			PluginLog("读取大厅房间列表失败，改为凭房间码直连：" + exception.Message);
		}

		bool connected = await session.JoinRoomAsync(room, this.playerName).ConfigureAwait(false);
		if (!connected)
		{
			throw new MultiplayerLobbyCreationException(
				MultiplayerLobbyCreationFailure.RoomConnectionFailed,
				"连不上联机中继（" + DescribeError(session.State.LastError) + "）");
		}

		roomName = room.Name;

		// 同步大厅人数（失败不影响联机：中继才是唯一的必要条件）。
		try
		{
			await api.JoinRoomAsync(room.Id, this.playerName).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			PluginLog("大厅登记加入失败（不影响联机）：" + exception.Message);
		}

		// 已连上中继，自动拉起游戏进房（可在设置里关闭）。
		await LaunchGameIfNeededAsync().ConfigureAwait(false);

		return Publish(MultiplayerLobbyState.Active);
	}

	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		string code = current?.RoomCode ?? "";
		if (isHost && code.Length > 0)
		{
			try
			{
				await api.CloseRoomAsync(code, playerName).ConfigureAwait(false);
			}
			catch
			{
				// 关房广播失败无所谓，中继侧断开即可。
			}
		}
		else if (code.Length > 0)
		{
			// 非房主退出：只把自己从大厅人数里摘掉，房间留给房主继续。
			try
			{
				await api.LeaveRoomAsync(code, playerName).ConfigureAwait(false);
			}
			catch
			{
				// 大厅同步失败不影响本地断开
			}
		}

		await session.LeaveRoomAsync().ConfigureAwait(false);
		isHost = false;
		current = null;
		RaiseStopped(MultiplayerLobbyStopReason.UserRequested);
	}

	// ==================== 一站式自动配置 ====================

	/// <summary>
	/// 创建 / 加入房间前的自动配置：自动探测 BeamNG.drive 安装目录，并把联机模组装进游戏。
	///
	/// 这是「一键联机」的前提：游戏 mods 目录里没有 startride.zip，
	/// 本地桥和中继就算连上了，游戏里也收发不到任何车辆数据。
	/// 失败时抛出带原因的异常，由界面翻译成用户能照做的提示。
	/// </summary>
	private async Task EnsureGameReadyAsync()
	{
		// 0) 必需配置文件：损坏/丢失会让游戏起不来，先从云端模板补全（失败不阻断联机）。
		await RepairGameConfigAsync().ConfigureAwait(false);

		// 1) 游戏目录：未设置或已失效时重新探测一遍（覆盖 Steam 库 / 常见安装位置）。
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

		// 2) 联机模组：关掉自动安装时也至少保证已有一份，否则联机不成立。
		ModInstaller installer = AppState.Current.ModInstaller;
		if (settings.AutoInstallMod || !installer.IsInstalled)
		{
			string? error = installer.Install();
			if (error != null)
			{
				throw new MultiplayerLobbyCreationException(
					MultiplayerLobbyCreationFailure.TerracottaProtocolFailed,
					"安装联机模组失败：" + error);
			}
		}
	}

	/// <summary>
	/// 检查并补全游戏必需配置文件（settings.json / 画质 / 在线 / 键位）。
	/// 纯尽力而为：网络不通或服务器没有模板时只留日志，不让联机流程失败。
	/// </summary>
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

	/// <summary>
	/// 房间就绪后自动把游戏拉起来（<see cref="AppSettings.AutoLaunchGameOnLobby"/> 控制）。
	/// 游戏内模组会自动连上本地桥 → 中继，玩家不需要再手动做任何配置。
	/// 启动失败只在日志里留痕，不打断已经建好的房间。
	/// </summary>
	private async Task LaunchGameIfNeededAsync()
	{
		if (!settings.AutoLaunchGameOnLobby)
		{
			return;
		}

		// 起游戏前再确认一次必需配置文件完整（房主物理在本地跑，配置坏了直接开不了）
		await RepairGameConfigAsync().ConfigureAwait(false);

		try
		{
			GameLauncher launcher = AppState.Current.Launcher;
			if (launcher.IsRunning)
			{
				PluginLog("BeamNG.drive 已在运行，跳过自动启动");
				return;
			}

			string? error = launcher.Launch(withMod: settings.AutoInstallMod);
			PluginLog(error == null ? "已自动启动 BeamNG.drive 进入房间" : "自动启动游戏失败：" + error);
		}
		catch (Exception exception)
		{
			PluginLog("自动启动游戏异常：" + exception.Message);
		}
	}

	// ==================== 会话事件 -> 界面快照 ====================

	private void OnSessionStateChanged()
	{
		if (disposed)
		{
			return;
		}

		var state = session.State;
		WriteStateFile();

		if (!state.Connected)
		{
			// 之前是通的、现在断了 —— 通知界面房间已结束。
			if (current != null)
			{
				current = null;
				RaiseStopped(MultiplayerLobbyStopReason.TerracottaServiceFailed);
			}
			return;
		}

		if (current == null)
		{
			return;
		}

		var snapshot = Publish(MultiplayerLobbyState.Active);
		RaiseSnapshotChanged(snapshot);
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
				// 心跳失败忽略
			}
		});
	}

	// ==================== 组装快照 ====================

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

	/// <summary>
	/// 事件必须在 UI 线程触发：ViewModel 会在回调里直接改 ObservableCollection，
	/// 从接收线程触发会抛跨线程异常（这正是自绘版曾经崩过的原因）。
	/// </summary>
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

	// ==================== 小工具 ====================

	/// <summary>生成房间码：SR + 4 位易读字符（去掉 0/O/1/I 这类混淆字符）。</summary>
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

	/// <summary>容忍用户粘贴整段文本或带分隔符的房间码。</summary>
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
				break;   // 遇到分隔符就截断，取前面那段
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
			// 日志失败不影响流程
		}
	}

	/// <summary>
	/// 联机状态旁路文件：%AppData%\StartRide\lobby-state.json
	///
	/// 界面之外的一个只读快照，供排障与自动化回归测试读取
	/// （界面上的房间码没法从进程外拿到）。写失败不影响联机。
	/// </summary>
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
			// 旁路文件写失败不影响联机
		}
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		heartbeatTimer.Dispose();
		session.StateChanged -= OnSessionStateChanged;
		session.Notice -= OnSessionNotice;
		session.Dispose();
	}
}

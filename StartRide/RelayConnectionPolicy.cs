using System;
using System.Collections.Generic;

namespace StartRide.Core
{

    /// <summary>
    /// 一条联机数据通道的走法。
    /// 参考 BeamLink 的 connection-policy（direct / relay / connecting），
    /// 但这里是 StartRide 自己的语义：
    ///   <see cref="Direct"/>   —— 直连中继的 TCP 端口，延时最低，但需要该端口对公网放开。
    ///   <see cref="Relay"/>    —— 走 HTTP 升级的 WebSocket 隧道，穿任何只开 80/443 的网络。
    ///   <see cref="Unknown"/>  —— 还没连上，或本地探测不出走的是哪条（老中继不回报）。
    /// </summary>
    public enum RelayPath
    {
        Unknown,
        Direct,
        Relay,
    }

    /// <summary>
    /// 一次连接所处的阶段。给界面用来区分「正在连」和「连上了但走的是备用路」，
    /// 而不是只有「连上 / 没连上」两种状态。
    /// </summary>
    public enum RelayConnectStage
    {
        Idle,
        Connecting,
        Direct,
        Relay,
        Failed,
    }

    /// <summary>
    /// 一条候选通道：地址 + 端口 + 是否要求加密。
    /// 只描述「怎么连」，不负责「怎么聊」，聊天协议仍归 <see cref="RelayClient"/>。
    /// </summary>
    public sealed class RelayEndpoint
    {
        public RelayEndpoint(RelayPath path, string host, int port, string wsPath = "", bool prefersTls = false)
        {
            Path = path;
            Host = host ?? "";
            Port = port;
            WebSocketPath = wsPath ?? "";
            PrefersTls = prefersTls;
        }

        public RelayPath Path { get; }
        public string Host { get; }
        public int Port { get; }
        public string WebSocketPath { get; }
        public bool PrefersTls { get; }

        /// <summary>给人看的地址串，形如 43.138.224.197:80/relay-ws。</summary>
        public string Display =>
            string.IsNullOrEmpty(WebSocketPath) ? $"{Host}:{Port}" : $"{Host}:{Port}{WebSocketPath}";
    }

    /// <summary>
    /// 连接策略：决定「先试哪条、退了试哪条」，以及一条地址该不该被信任。
    ///
    /// 从 BeamLink 借来的是思路，不是代码：
    ///   1. 显式地把通道分成「直连」和「中继（隧道）」两类，并让上层能看见当前走的是哪类；
    ///   2. 维护一份可信节点白名单，避免被一个手改过的配置文件把玩家引到别人的服务器上；
    ///   3. 需要 TLS 的节点配一份固定证书（pinned CA）的钩子，不信任系统根证书链。
    ///
    /// 这里是纯策略、无副作用：不 new 任何网络对象，方便单测。
    /// </summary>
    public static class RelayConnectionPolicy
    {

        /// <summary>
        /// 官方中继节点白名单。和 AppSettings 的默认中继保持一致。
        /// 含义不是「只能连这些」，而是「这些是官方节点，连它们时允许使用直连 + 跳证书校验」。
        /// 玩家自己填的第三方地址一律按「不受信」处理，只允许走中继隧道。
        /// </summary>
        private static readonly HashSet<string> TrustedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "43.138.224.197",
            "startride.top",
        };

        /// <summary>固定证书表：host:port → 证书路径（相对程序目录）。暂时为空，留钩子。</summary>
        private static readonly Dictionary<string, string> PinnedCertificates = new(StringComparer.OrdinalIgnoreCase)
        {
            // 将来给 official 节点上自签证书时，在这里登记：
            // { "43.138.224.197:4443", "certs/relay-node-ca.crt" },
        };

        /// <summary>这条主机是不是官方节点。第三方地址一律 false。</summary>
        public static bool IsTrustedHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            return TrustedHosts.Contains(host.Trim());
        }

        /// <summary>
        /// 该地址要不要固定证书。返回证书文件的相对路径；不要求则返回 null。
        /// 供将来 WebSocketTransport 走 TLS 时取用。
        /// </summary>
        public static string? PinnedCertificateFor(string? host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            return PinnedCertificates.TryGetValue($"{host.Trim()}:{port}", out var p) ? p : null;
        }

        /// <summary>
        /// 一条地址能不能直连。
        ///
        /// 规则（照 BeamLink「gameControlUrlsFor」的精神）：
        ///   - 官方节点：允许直连（玩家能直连上说明他网络条件好，此时直连延时最优）；
        ///   - 第三方地址：不允许直连，只能走中继隧道 —— 直连要求你把一个任意端口对公网放开，
        ///     不该为来路不明的服务器开放。
        /// </summary>
        public static bool AllowsDirectPath(string? host) => IsTrustedHost(host);

        /// <summary>
        /// 按「网络条件」排出候选顺序。
        ///
        /// <paramref name="preferWebSocket"/> 是玩家在设置里的偏好（原来的 PreferWebSocket）；
        /// 这里把它和「能不能直连」两条信息合起来给出最终顺序 —— 原来的代码是硬编码两条
        /// 分支，现在收拢到一处，好加规则也好测。
        /// </summary>
        public static IReadOnlyList<RelayPath> OrderPaths(bool preferWebSocket, string? host, bool directPortKnownOpen)
        {
            bool directAllowed = AllowsDirectPath(host) && directPortKnownOpen;

            // 连直连都不允许时，只剩中继一条路。
            if (!directAllowed)
                return new[] { RelayPath.Relay };

            // 允许直连时按玩家偏好排；偏好只是顺序，不改变「两条都试」。
            return preferWebSocket
                ? new[] { RelayPath.Relay, RelayPath.Direct }
                : new[] { RelayPath.Direct, RelayPath.Relay };
        }

        /// <summary>把一条通道枚举翻译成界面文案用的短名。</summary>
        public static string DisplayName(RelayPath path) => path switch
        {
            RelayPath.Direct => "直连",
            RelayPath.Relay => "中继",
            _ => "未知",
        };

        /// <summary>
        /// 从连接的自述字段还原走的是哪条路。
        /// 中继在 join 回包里回报 transport（老的、没这个字段的中继返回空串）。
        /// </summary>
        public static RelayPath ParseTransport(string? transport) =>
            (transport ?? "").Trim().ToLowerInvariant() switch
            {
                "tcp" => RelayPath.Direct,
                "websocket" or "ws" => RelayPath.Relay,
                _ => RelayPath.Unknown,
            };

        /// <summary>
        /// 连接阶段 → 界面短名。给状态条/浮层用。
        /// </summary>
        public static string StageText(RelayConnectStage stage) => stage switch
        {
            RelayConnectStage.Connecting => "正在连接",
            RelayConnectStage.Direct => "直连",
            RelayConnectStage.Relay => "中继",
            RelayConnectStage.Failed => "连接失败",
            _ => "未连接",
        };
    }
}

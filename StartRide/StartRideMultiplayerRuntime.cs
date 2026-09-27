namespace StartRide.Core
{
    /// <summary>
    /// 联机运行期的全局标记，给「窗口关闭行为」用。
    ///
    /// 为什么必须有这个东西：
    ///   启动器本身就是联机的**本地桥**（游戏内模组连 127.0.0.1:4444 → 启动器 → 中继）。
    ///   用户进房后会自然而然地点窗口右上角的 X（它挡着游戏），而
    ///   <c>AppSettings.CloseToTray</c> 默认是 false → 进程真的退了 → 本地桥消失 →
    ///   游戏侧每 8 秒报一次 `连接断开: connect timeout`，双方谁也看不到谁的车。
    ///   已实测过一次：2026-09-18 那局游戏跑了 4 分半、游戏侧连了 30+ 次全部超时，
    ///   而当天本机一份 launcher-*.log 都没有 —— 启动器全程不在运行。
    ///
    /// 这里刻意做成静态字段而不是走 DI：关闭窗口的处理在退出路径上，
    /// 越少依赖容器越不容易在关窗时出问题。
    /// </summary>
    public static class StartRideMultiplayerRuntime
    {
        /// <summary>当前是否处于「已进房间」的联机中（由 StartRideLobbyService 维护）。</summary>
        public static volatile bool IsInRoom;
    }
}

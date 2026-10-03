namespace StartRide.Core
{

    public static class StartRideMultiplayerRuntime
    {
        public static volatile bool IsInRoom;

        /// <summary>
        /// 当前房间要进的关卡 id。在房里时从主页/托盘启动游戏要直接进这张图，
        /// 否则「关掉自动启动」的用户会停在主菜单、和房里其他人不在一个地图上。
        /// </summary>
        public static volatile string RoomMapId = "";

        /// <summary>在房里且有指定地图时返回它，否则返回 null（= 走普通启动，进主菜单）。</summary>
        public static string? LobbyLevelOrNull() =>
            IsInRoom && RoomMapId.Length > 0 ? RoomMapId : null;
    }
}

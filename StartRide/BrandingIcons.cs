using System;

namespace StartRide.Core
{
    /// <summary>
    /// 随 EXE 打包的品牌图标资源（WPF pack URI，Image / IconSource 都能直接吃）。
    ///
    /// 反编译过来的界面里图标路径有两种写法，这里统一成能用的那一种：
    ///   ✅ "/StartRide;component/Assets/Branding/xxx.png" —— 明确指向程序集资源，一定能加载
    ///   ❌ "assets/branding/xxx.png" —— 相对路径，IconSourceImageLoader 会当成无基准的相对 URI，
    ///      加载失败后图标位就是空白（首页启动卡片曾因此没有图标）
    ///
    /// 另：StartRide 的 Minecraft 方块图标（/Assets/Icons/block/*.png）在 StartRide 里已经不该出现，
    /// 所有"这个游戏"的图标位统一走这里的 BeamNgLogo。
    /// </summary>
    public static class BrandingIcons
    {
        private const string ComponentPrefix = "/StartRide;component/";

        /// <summary>BeamNG.drive 官方 logo（256 方形高清版，橙节点 + 深色连杆）。</summary>
        public const string BeamNgLogo = ComponentPrefix + "Assets/Branding/beamng_logo_256.png";

        /// <summary>BeamNG.drive 官方 logo（游戏原图 50x50，需要小尺寸时用）。</summary>
        public const string BeamNgLogoSmall = ComponentPrefix + "Assets/Branding/beamng_logo_50x50.png";

        /// <summary>StartRide 启动器自己的标志。</summary>
        public const string StartRideIcon = ComponentPrefix + "Assets/Branding/startride_icon_256.png";

        /// <summary>SvgIcon 用的 BeamNG logo 图标键（单色版，跟随主题前景色）。</summary>
        public const string BeamNgSvgKey = "beamng/beamng";

        /// <summary>把任意图标路径规范化成可直接加载的 pack URI（空值/相对路径都能救回来）。</summary>
        public static string Normalize(string? source, string fallback = BeamNgLogo)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return fallback;
            }

            string value = source!.Trim().Trim('"').Replace('\\', '/');
            if (value.StartsWith(ComponentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            if (value.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            if (value.StartsWith("/", StringComparison.Ordinal))
            {
                return ComponentPrefix + value.TrimStart('/');
            }
            if (value.IndexOf(':') > 0)
            {
                // 本地绝对路径（C:\... 或 Steam 头像下载缓存）——原样返回
                return source!.Trim();
            }
            // "assets/branding/xxx.png" 这类相对路径：补上程序集前缀
            return ComponentPrefix + value;
        }
    }
}

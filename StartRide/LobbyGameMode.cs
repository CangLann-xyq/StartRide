using System;
using System.Collections.Generic;
using System.Linq;

namespace StartRide.Core
{
    /// <summary>
    /// 一个联机玩法模式的定义。
    ///
    /// 为什么要有这一层：以前 StartRide 的房间只有「地图 + 出生点 + 人数上限」，
    /// 玩法完全靠玩家自觉。参考 BeamLink 的做法给房间加一个明确宣告的玩法，
    /// 让加入者在进房前就知道这局是干什么的，模组也可以按玩法决定自己的行为。
    ///
    /// ⚠️ 这里只描述**机制**（id / 显示名 / 规则开关 / 对模组的提示），
    /// 不带任何界面资源。UI 一律用 StartRide 自己的样式渲染。
    /// </summary>
    public sealed class LobbyGameMode
    {
        public LobbyGameMode(
            string id,
            string title,
            string summary,
            string detail,
            string iconKey,
            bool spawnsApart = false,
            bool teams = false,
            bool rounds = false,
            bool traffic = false)
        {
            Id = id;
            Title = title;
            Summary = summary;
            Detail = detail;
            IconKey = iconKey;
            SpawnsApart = spawnsApart;
            Teams = teams;
            Rounds = rounds;
            Traffic = traffic;
        }

        /// <summary>线上标识符。发到中继 / 写进 bootstrap 用的就是它，必须稳定不变。</summary>
        public string Id { get; }

        /// <summary>显示名。</summary>
        public string Title { get; }

        /// <summary>一句话说明，列表副标题用。</summary>
        public string Summary { get; }

        /// <summary>展开后的完整规则说明，详情区用。</summary>
        public string Detail { get; }

        /// <summary>图标键（走既有的 SvgIcon 资源，不引入新图）。</summary>
        public string IconKey { get; }

        /// <summary>是否要求玩家分散出生（捉迷藏/警匪这类不该在同一落点开局）。</summary>
        public bool SpawnsApart { get; }

        /// <summary>是否分队（警匪、赛道日）。</summary>
        public bool Teams { get; }

        /// <summary>是否按回合制进行（德比、赛道日）。</summary>
        public bool Rounds { get; }

        /// <summary>是否需要 AI 车流（只对支持的模式开放，参考 BeamLink 只给捉迷藏开车流）。</summary>
        public bool Traffic { get; }

        /// <summary>玩法自身的规则版本号。规则改了要一起改它，方便两端判断是否同一套规则。</summary>
        public int Revision { get; set; } = 1;

        /// <summary>列表项里挂的小胶囊（例如「分队」「回合制」）。</summary>
        public IReadOnlyList<string> Tags
        {
            get
            {
                var tags = new List<string>();
                if (Teams) tags.Add("分队");
                if (Rounds) tags.Add("回合制");
                if (Traffic) tags.Add("支持车流");
                return tags;
            }
        }

        public bool HasTags => Tags.Count > 0;
    }

    /// <summary>
    /// 玩法模式目录 + 线上值的规范化。
    /// 这是「玩法」这件事在启动器侧的唯一定义处，其它地方一律走这里取。
    /// </summary>
    public static class LobbyGameModeCatalog
    {
        /// <summary>没选玩法时的兜底。</summary>
        public const string DefaultId = "free_drive";

        private static readonly LobbyGameMode[] Modes =
        {
            new LobbyGameMode(
                "free_drive",
                "自由驾驶",
                "没有规则，想去哪去哪",
                "不设目标也不分队。地图上的路、赛道、越野场地全部开放，"
                + "适合随便开开、看风景、试车或几个人约着跑一段。"
                + "新人第一次联机建议先从这个模式开始。",
                "beamng/route",
                spawnsApart: false),

            new LobbyGameMode(
                "hide_seek",
                "捉迷藏",
                "一人抓，其他人藏",
                "开局指定一名搜索者，其余人分散藏进地图各处。搜索者要在限定时间内把人找出来，"
                + "被找到的人转为搜索者一起抓。躲藏方全程可以被看见小地图标记。"
                + "推荐配合车流使用 —— 混在 AI 车里更难被发现。",
                "general/general_pin",
                spawnsApart: true,
                traffic: true),

            new LobbyGameMode(
                "cops_robber",
                "警察抓强盗",
                "分队对抗，追捕与逃逸",
                "分警察与强盗两队。强盗要在地图上完成目标并甩掉追捕，警察负责拦截与逼停。"
                + "双方车辆不限，撞毁即为出局，队伍全员出局或时间耗尽分出胜负。",
                "main_menu_multiplayer",
                spawnsApart: true,
                teams: true),

            new LobbyGameMode(
                "derby",
                "德比",
                "最后站着的人赢",
                "所有人集中到一个封闭场地里互相撞击，车辆损毁即出局，最后剩下的一辆获胜。"
                + "按回合进行，每回合结束后自动复位重新开始。",
                "beamng/vehicle",
                spawnsApart: false,
                rounds: true),

            new LobbyGameMode(
                "track_day",
                "赛道日",
                "刷圈速，比谁更快",
                "在赛道图上按回合刷单圈成绩，每人依次发车、计时取最快圈。"
                + "适合正经比一比谁开得快，也可以当练车用。",
                "beamng/route",
                spawnsApart: true,
                teams: false,
                rounds: true)
        };

        private static readonly Dictionary<string, LobbyGameMode> Lookup =
            Modes.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<LobbyGameMode> All => Modes;

        /// <summary>默认玩法。</summary>
        public static LobbyGameMode Default => Lookup[DefaultId];

        /// <summary>
        /// 规范化一个线上传来的玩法 id。
        /// 认不出来的一律退回默认玩法 —— 旧版本客户端、手改过的配置、
        /// 或者对端用了本版还没有的新玩法，都走这一条，绝不抛异常。
        /// </summary>
        public static LobbyGameMode Normalize(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Default;
            return Lookup.TryGetValue(id.Trim(), out var hit) ? hit : Default;
        }

        /// <summary>这个 id 是不是本版认识的玩法。</summary>
        public static bool IsKnown(string? id) =>
            !string.IsNullOrWhiteSpace(id) && Lookup.ContainsKey(id.Trim());

        /// <summary>取显示名；认不出来时退回默认玩法的名字。</summary>
        public static string DisplayName(string? id) => Normalize(id).Title;
    }
}

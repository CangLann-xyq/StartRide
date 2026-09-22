"""把 Strings.resx 的 Minecraft / Terracotta 语境改写为 StartRide / BeamNG.drive。

两层策略：
  1. 术语级替换（broad）：Minecraft->BeamNG.drive、.minecraft 目录->游戏目录、Terracotta->StartRide 中继 等
  2. 关键项精写（OVERRIDES）：品牌、联机步骤、主页等需要人话的条目

用法: python retheme.py [--apply] [--diff]
"""
import re
import sys

RESX = r"D:\code\all\_bh_xaml\Launcher.App.Resources.Strings.resx"

# ---------- 第一层：术语替换（按顺序，先长后短） ----------
TERMS = [
    # 联机后端
    ("Terracotta | 陶瓦联机", "StartRide 中继"),
    ("Terracotta 联机模块", "联机中继"),
    ("Terracotta 联机服务", "联机中继"),
    ("Terracotta 正被其他启动器使用", "联机服务正忙"),
    ("Terracotta", "StartRide 中继"),
    ("陶瓦联机", "StartRide 中继"),
    ("EasyTier", "StartRide 中继"),
    # Minecraft 目录/版本
    (".minecraft 目录", "游戏目录"),
    (".minecraft", "游戏目录"),
    ("Minecraft 目录", "游戏目录"),
    ("Minecraft 版本", "游戏版本"),
    ("Minecraft 皮肤", "车辆涂装"),
    ("Minecraft 存档", "游戏存档"),
    ("Minecraft 规则", "账户规则"),
    ("Minecraft 登录服务", "账户登录服务"),
    # 泛化
    ("Minecraft", "BeamNG.drive"),
    # 局域网世界（Minecraft 说法）→ BeamNG 说法
    ("创建局域网世界", "进入联机模式"),
    ("局域网世界", "联机对局"),
    ("游戏世界", "游戏"),
]

# ---------- 第二层：关键项精写 ----------
OVERRIDES = {
    # 品牌
    "App_Title": "StartRide 启动器",

    # 联机页
    "Multiplayer_LobbyPoweredByTerracotta": "联机服务由 StartRide 自建中继提供",
    "Multiplayer_Create_TerracottaAttributionLinkText": "StartRide 中继",
    "Multiplayer_Create_StepOpenToLan": "第一步：启动 BeamNG.drive 并进入任意地图。",
    "Multiplayer_Create_StepSelectInstance": "第二步：确认联机模组已随游戏启动（启动器会自动安装）。",
    "Multiplayer_Create_StepCreateLobby": "第三步：返回启动器，点击“创建房间”生成房间码，分享给好友即可。",
    "Multiplayer_Create_WorldUnavailable": "未检测到运行中的游戏，请先启动 BeamNG.drive 后重试。",
    "Multiplayer_LobbyWorldClosed": "游戏已退出，房间已自动解散。",
    "Multiplayer_LobbyTerracottaExited": "与联机中继的连接已断开，房间已自动解散。",
    "Multiplayer_LobbyTerracottaServiceFailed": "联机中继连接异常，房间已自动解散。",
    "Multiplayer_Create_TerracottaUnavailable": "联机服务不可用，请重新进入联机页面后重试。",
    "Multiplayer_Create_TerracottaBusy": "联机服务正忙，请先结束其他联机房间后重试。",
    "Multiplayer_Create_TerracottaProtocolFailed": "联机中继连接异常，请稍后重试。",
    "Multiplayer_LobbyGameWorldPlaceholder": "地图名字",
    "Multiplayer_LobbyRoomCodePlaceholder": "SRXXXX",
    "Multiplayer_LobbyClientIdPlaceholderFormat": "{0}",

    # 主页
    "Home_LaunchInstanceSubtitleFormat": "BeamNG.drive {0} · {1}",
    "Home_NoVersionSelected": "未检测到 BeamNG.drive",
    "Home_NoLaunchInstances": "还没有检测到 BeamNG.drive。",
    "Home_ChooseOrCreateAccount": "设置一个昵称即可联机，无需正版账号。",

    # 账户
    "Account_BuyMinecraftButton": "了解 BeamNG.drive",
    "Dialog_AddOfflineAccountSubtitle": "输入一个昵称即可联机，StartRide 不需要正版账号。",
    "Status_OpenMinecraftPurchasePageFailed": "无法打开 BeamNG.drive 页面，请稍后重试。",
    "Status_MinecraftJavaOwnershipRequired": "此账户不可用，StartRide 联机不需要正版账号。",
    "Status_LoginMissingProfile": "登录失败：未获取到账户资料",

    # 第三方协议框（现已被桩服务跳过，但仍需文案正确）
    "Dialog_TerracottaAgreementTitle": "联机功能使用须知",
    "Dialog_TerracottaAgreementMessage": "联机功能由 StartRide 自建中继提供。使用联机功能时，您必须遵守中国大陆相关法律法规，不得用于违法用途。",
    "Status_OpenTerracottaProjectFailed": "无法打开联机中继页面，请检查系统浏览器设置后重试。",
    "Dialog_MultiplayerLanWorldDetectionTitle": "正在创建房间",

    # 全局设置里的目录措辞
    "Settings_MinecraftDirectoryLabel": "游戏目录",
    "Settings_MinecraftDirectoryListLabel": "游戏目录列表",
    "FilePicker_MinecraftDirectoryTitle": "选择 BeamNG.drive 目录",
    "Status_MinecraftDirectoryChanged": "游戏目录已更新。",
    "Status_MinecraftDirectoryUnavailable": "该游戏目录不存在或无法访问。",
    "Status_MinecraftDirectoryAdded": "游戏目录已添加并切换。",
    "Status_AddMinecraftDirectoryFailed": "添加游戏目录失败，请确认目录存在且可以访问。",
    "Status_RenameMinecraftDirectoryFailed": "更改游戏目录名称失败，请稍后重试。",
    "Status_RemoveMinecraftDirectoryFromListFailed": "从列表中移除游戏目录失败，请稍后重试。",
    "Dialog_SwitchMinecraftDirectoryTitle": "切换游戏目录",
    "Dialog_RenameMinecraftDirectoryNameTitle": "重命名游戏目录",
    "Dialog_AddMinecraftDirectoryNameTitle": "命名游戏目录",
    "Dialog_RemoveMinecraftDirectoryTitle": "移除游戏目录",
    "Dialog_MinecraftDirectoryStartupRecoveryTitle": "当前游戏目录不可用",
    "Dialog_MinecraftDirectoryStartupRecoveryFailedTitle": "无法恢复游戏目录",
    "Status_SelectMinecraftVersionFirst": "请先选择游戏版本",
    "GameSettings_UnknownMinecraftVersion": "未知版本",
}


def load_text():
    with open(RESX, "r", encoding="utf-8") as f:
        return f.read()


def apply_terms(value: str) -> str:
    for old, new in TERMS:
        value = value.replace(old, new)
    # 术语替换会在中文字符之间留下空格（如「选择 车辆涂装」），这里收掉
    return re.sub(r"(?<=[\u4e00-\u9fff])\s+(?=[\u4e00-\u9fff])", "", value)


def main():
    do_apply = "--apply" in sys.argv
    show_diff = "--diff" in sys.argv

    text = load_text()
    changes = []

    def repl(match):
        head, key, body, tail = match.group(1), match.group(2), match.group(3), match.group(4)
        original = body
        new = OVERRIDES.get(key, apply_terms(body))
        if new != original:
            changes.append((key, original, new))
        return head + new + tail

    pattern = re.compile(
        r'(\s*<data name="([^"]+)"[^>]*>\s*<value>)(.*?)(</value>)',
        re.DOTALL,
    )
    new_text = pattern.sub(repl, text)

    print(f"改写 {len(changes)} 条\n")
    if show_diff:
        for key, old, new in changes:
            print(f"[{key}]\n  - {old}\n  + {new}")
    else:
        for key, old, new in changes[:60]:
            print(f"{key}: {old!r} -> {new!r}")
        if len(changes) > 60:
            print(f"... 另有 {len(changes) - 60} 条")

    if do_apply:
        with open(RESX, "w", encoding="utf-8", newline="\n") as f:
            f.write(new_text)
        print("\n已写入 " + RESX)
    else:
        print("\n(试运行，加 --apply 生效)")


if __name__ == "__main__":
    main()

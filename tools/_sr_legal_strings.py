# -*- coding: utf-8 -*-
"""给四套 Strings.resx 补法律文件相关的文案键，并同步 Strings.cs 的属性。

纪律（踩过坑）：
  · 读进来先归一到 \n 再匹配，写回时保持原文件的行尾风格 —— 否则按原样匹配会静默 0 命中；
  · 每条替换"命中数必须恰好 1"，否则不写文件、直接报错退出；
  · 新增键做成幂等（已存在就跳过），脚本可以重复跑。
"""
import io
import os
import re
import sys

ROOT = r"D:\code\all\_bh_xaml"
RESX = os.path.join(ROOT, "StartRide.App.Resources.Strings.resx")
RESX_EN = os.path.join(ROOT, "StartRide.App.Resources.Strings.en.resx")
RESX_JA = os.path.join(ROOT, "StartRide.App.Resources.Strings.ja-JP.resx")
RESX_HANT = os.path.join(ROOT, "StartRide.App.Resources.Strings.zh-Hant.resx")
STRINGS_CS = os.path.join(ROOT, "StartRide.App.Resources", "Strings.cs")

# ── 需要替换（键已存在）的文案 ──────────────────────────────────────────
REPLACE = {
    RESX: {
        "Dialog_UserAgreementTitle": "使用条款与说明",
        "Dialog_UserAgreementMessage": "欢迎使用 StartRide 启动器。请先阅读下列文件；点击「同意并继续」即表示你已阅读、理解并同意全部内容。",
        "Dialog_UserAgreementAgreeButton": "同意并继续",
    },
    RESX_EN: {
        "Dialog_UserAgreementTitle": "Terms and Notices",
        "Dialog_UserAgreementMessage": "Welcome to StartRide. Please read the documents below first. Clicking \u201cAgree and continue\u201d means you have read, understood and accepted all of them.",
        "Dialog_UserAgreementAgreeButton": "Agree and continue",
    },
    RESX_JA: {
        "Dialog_UserAgreementTitle": "利用規約とご案内",
        "Dialog_UserAgreementMessage": "StartRide へようこそ。まず以下の文書をお読みください。「同意して続行」をクリックすると、すべての内容を読み、理解し、同意したものとみなされます。",
        "Dialog_UserAgreementAgreeButton": "同意して続行",
    },
    RESX_HANT: {
        "Dialog_UserAgreementTitle": "使用條款與說明",
        "Dialog_UserAgreementMessage": "歡迎使用 StartRide 啟動器。請先閱讀下列文件；點選「同意並繼續」即表示你已閱讀、理解並同意全部內容。",
        "Dialog_UserAgreementAgreeButton": "同意並繼續",
    },
}

# ── 需要新增的键 ────────────────────────────────────────────────────────
ADD = {
    RESX: {
        "Dialog_UserAgreementHint": "首次使用需要确认。不同意时请点击「不同意并退出」。",
        "Legal_Doc_UserAgreement_Title": "用户服务协议",
        "Legal_Doc_UserAgreement_Description": "使用本软件时你与我们之间的完整约定",
        "Legal_Doc_PrivacyPolicy_Title": "隐私政策",
        "Legal_Doc_PrivacyPolicy_Description": "我们收集哪些信息、用途是什么、你能怎么管理",
        "Legal_Doc_MinorProtection_Title": "未成年人个人信息保护规则",
        "Legal_Doc_MinorProtection_Description": "面向未成年人与监护人的专门规则",
        "Legal_Doc_Disclaimer_Title": "免责声明与风险提示",
        "Legal_Doc_Disclaimer_Description": "使用模组、联机与备份之前务必了解的风险",
        "Legal_Doc_MultiplayerConduct_Title": "联机服务使用规范",
        "Legal_Doc_MultiplayerConduct_Description": "房间名、聊天与评论中必须遵守的行为准则",
        "Legal_Doc_ThirdPartyNotices_Title": "第三方组件与开源许可声明",
        "Legal_Doc_ThirdPartyNotices_Description": "随本软件分发的开源组件及其许可协议",
        "Legal_Doc_Copyright_Title": "版权声明",
        "Legal_Doc_Copyright_Description": "Copyright \u00a9 2026 肖又祺 \u00b7 StartRide",
        "Legal_Doc_License_Title": "开源协议",
        "Legal_Doc_License_Description": "本软件依据 GNU 通用公共许可证第 3 版（GPL-3.0）发布",
        "Dialog_UserAgreementIndexLink": "查看全部条款与说明 ↓",
    },
    RESX_EN: {
        "Dialog_UserAgreementHint": "Confirmation is required on first launch. If you disagree, click \u201cDisagree and exit\u201d.",
        "Legal_Doc_UserAgreement_Title": "Terms of Service",
        "Legal_Doc_UserAgreement_Description": "The complete agreement between you and us for using this software",
        "Legal_Doc_PrivacyPolicy_Title": "Privacy Policy",
        "Legal_Doc_PrivacyPolicy_Description": "What we collect, why we collect it, and how you can manage it",
        "Legal_Doc_MinorProtection_Title": "Minors' Personal Information Protection Rules",
        "Legal_Doc_MinorProtection_Description": "Dedicated rules for minors and their guardians",
        "Legal_Doc_Disclaimer_Title": "Disclaimer and Risk Notice",
        "Legal_Doc_Disclaimer_Description": "Risks to understand before using mods, multiplayer or backups",
        "Legal_Doc_MultiplayerConduct_Title": "Multiplayer Service Conduct Rules",
        "Legal_Doc_MultiplayerConduct_Description": "Rules that apply to room names, chat and comments",
        "Legal_Doc_ThirdPartyNotices_Title": "Third-Party Components and Open Source Notices",
        "Legal_Doc_ThirdPartyNotices_Description": "Open source components shipped with this software and their licenses",
        "Legal_Doc_Copyright_Title": "Copyright Notice",
        "Legal_Doc_Copyright_Description": "Copyright \u00a9 2026 肖又祺 \u00b7 StartRide",
        "Legal_Doc_License_Title": "Open Source License",
        "Legal_Doc_License_Description": "Released under the GNU General Public License v3.0 (GPL-3.0)",
        "Dialog_UserAgreementIndexLink": "View all terms and notices ↓",
    },
    RESX_JA: {
        "Dialog_UserAgreementHint": "初回起動時は確認が必要です。同意しない場合は「同意しないで終了」をクリックしてください。",
        "Legal_Doc_UserAgreement_Title": "利用規約",
        "Legal_Doc_UserAgreement_Description": "本ソフトウェアの利用に関する、あなたと当方との完全な合意",
        "Legal_Doc_PrivacyPolicy_Title": "プライバシーポリシー",
        "Legal_Doc_PrivacyPolicy_Description": "取得する情報、利用目的、そして管理の方法",
        "Legal_Doc_MinorProtection_Title": "未成年者の個人情報保護規則",
        "Legal_Doc_MinorProtection_Description": "未成年者と保護者のための専用ルール",
        "Legal_Doc_Disclaimer_Title": "免責事項とリスクのご案内",
        "Legal_Doc_Disclaimer_Description": "MOD・マルチプレイ・バックアップの前に知っておくべきリスク",
        "Legal_Doc_MultiplayerConduct_Title": "マルチプレイサービス利用規範",
        "Legal_Doc_MultiplayerConduct_Description": "ルーム名・チャット・コメントで守るべきルール",
        "Legal_Doc_ThirdPartyNotices_Title": "サードパーティコンポーネントおよびオープンソースライセンス表示",
        "Legal_Doc_ThirdPartyNotices_Description": "同梱されるオープンソースコンポーネントとそのライセンス",
        "Legal_Doc_Copyright_Title": "著作権表示",
        "Legal_Doc_Copyright_Description": "Copyright \u00a9 2026 肖又祺 \u00b7 StartRide",
        "Legal_Doc_License_Title": "オープンソースライセンス",
        "Legal_Doc_License_Description": "GNU General Public License v3.0（GPL-3.0）に基づいて公開されています",
        "Dialog_UserAgreementIndexLink": "すべての規約とご案内を見る ↓",
    },
    RESX_HANT: {
        "Dialog_UserAgreementHint": "首次使用需要確認。不同意時請點選「不同意並退出」。",
        "Legal_Doc_UserAgreement_Title": "使用者服務協議",
        "Legal_Doc_UserAgreement_Description": "使用本軟體時你與我們之間的完整約定",
        "Legal_Doc_PrivacyPolicy_Title": "隱私政策",
        "Legal_Doc_PrivacyPolicy_Description": "我們蒐集哪些資訊、用途為何、你能如何管理",
        "Legal_Doc_MinorProtection_Title": "未成年人個人資料保護規則",
        "Legal_Doc_MinorProtection_Description": "面向未成年人與監護人的專門規則",
        "Legal_Doc_Disclaimer_Title": "免責聲明與風險提示",
        "Legal_Doc_Disclaimer_Description": "使用模組、連線與備份之前務必了解的風險",
        "Legal_Doc_MultiplayerConduct_Title": "連線服務使用規範",
        "Legal_Doc_MultiplayerConduct_Description": "房間名稱、聊天與評論中必須遵守的行為準則",
        "Legal_Doc_ThirdPartyNotices_Title": "第三方元件與開源授權聲明",
        "Legal_Doc_ThirdPartyNotices_Description": "隨本軟體散布的開源元件及其授權條款",
        "Legal_Doc_Copyright_Title": "版權聲明",
        "Legal_Doc_Copyright_Description": "Copyright \u00a9 2026 肖又祺 \u00b7 StartRide",
        "Legal_Doc_License_Title": "開源授權",
        "Legal_Doc_License_Description": "本軟體依 GNU 通用公共授權條款第 3 版（GPL-3.0）發布",
        "Dialog_UserAgreementIndexLink": "檢視全部條款與說明 ↓",
    },
}

# Strings.cs 需要新增的公开属性（按 resx 键名生成）
CS_PROPS = [
    "Legal_Doc_UserAgreement_Title",
    "Legal_Doc_UserAgreement_Description",
    "Legal_Doc_PrivacyPolicy_Title",
    "Legal_Doc_PrivacyPolicy_Description",
    "Legal_Doc_MinorProtection_Title",
    "Legal_Doc_MinorProtection_Description",
    "Legal_Doc_Disclaimer_Title",
    "Legal_Doc_Disclaimer_Description",
    "Legal_Doc_MultiplayerConduct_Title",
    "Legal_Doc_MultiplayerConduct_Description",
    "Legal_Doc_ThirdPartyNotices_Title",
    "Legal_Doc_ThirdPartyNotices_Description",
    "Legal_Doc_Copyright_Title",
    "Legal_Doc_Copyright_Description",
    "Legal_Doc_License_Title",
    "Legal_Doc_License_Description",
    "Dialog_UserAgreementHint",
    "Dialog_UserAgreementIndexLink",
]

FAILED = []


def read(path):
    with io.open(path, "rb") as fh:
        raw = fh.read()
    crlf = b"\r\n" in raw
    text = raw.decode("utf-8-sig" if raw[:3] == b"\xef\xbb\xbf" else "utf-8")
    return text.replace("\r\n", "\n"), crlf


def write(path, text, crlf):
    out = text.replace("\n", "\r\n") if crlf else text
    with io.open(path, "wb") as fh:
        fh.write(out.encode("utf-8"))


def esc(value):
    return (value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


def replace_value(text, key, value, path):
    pattern = re.compile(
        r'(<data name="' + re.escape(key) + r'" xml:space="preserve"><value>)(.*?)(</value></data>)',
        re.S,
    )
    hits = pattern.findall(text)
    if len(hits) != 1:
        FAILED.append("%s :: 替换 %s 命中 %d 次（应为 1）" % (os.path.basename(path), key, len(hits)))
        return text
    return pattern.sub(lambda m: m.group(1) + esc(value) + m.group(3), text, count=1)


def add_key(text, key, value, path):
    if ('name="%s"' % key) in text:
        return text
    line = '  <data name="%s" xml:space="preserve"><value>%s</value></data>\n' % (key, esc(value))
    if "</root>" in text:
        # ⚠️ rpartition 返回 (head, sep, tail)，拼回去必须带上 sep —— 漏掉它等于删掉 </root>。
        head, sep, tail = text.rpartition("</root>")
        if not head.endswith("\n"):
            head += "\n"
        return head + line + sep + tail
    # 文件缺 </root>（被上一轮写坏过）：补回去再插，脚本可自愈。
    text = text.rstrip("\n") + "\n"
    return text + line + "</root>"


def main():
    for path, mapping in REPLACE.items():
        if not os.path.exists(path):
            FAILED.append("缺少文件 %s" % path)
            continue
        text, crlf = read(path)
        for key, value in mapping.items():
            text = replace_value(text, key, value, path)
        for key, value in ADD[path].items():
            text = add_key(text, key, value, path)
        write(path, text, crlf)
        print("[resx] %-46s 替换 %d / 新增 %d" % (os.path.basename(path), len(mapping), len(ADD[path])))

    # Strings.cs 属性
    text, crlf = read(STRINGS_CS)
    anchor = '\tpublic static string Settings_ViewLegalDocumentButton => Get("Settings_ViewLegalDocumentButton");'
    if text.count(anchor) != 1:
        FAILED.append("Strings.cs :: 锚点命中 %d 次（应为 1）" % text.count(anchor))
    else:
        block = ""
        added = 0
        for key in CS_PROPS:
            prop = '\tpublic static string %s => Get("%s");' % (key, key)
            if prop in text:
                continue
            block += "\n" + prop
            added += 1
        text = text.replace(anchor, anchor + "\n" + block, 1)
        write(text=text, path=STRINGS_CS, crlf=crlf)
        print("[cs]   Strings.cs 新增 %d 个属性" % added)

    if FAILED:
        print("\n以下条目未按预期应用，已中止：")
        for item in FAILED:
            print("  ! " + item)
        sys.exit(1)
    print("\n全部应用完成。")


if __name__ == "__main__":
    main()

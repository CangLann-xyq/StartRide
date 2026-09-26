# -*- coding: utf-8 -*-
"""把 docs/legal/ 下的六份法律文件上传到腾讯文档，并把拿到的链接回填进 SiteLinks.cs。

用法：
    python tools/_sr_tencent_legal.py            # 只补还没上传的（幂等）
    python tools/_sr_tencent_legal.py --force    # 全部重新上传

前置：腾讯文档连接器必须在 WorkBuddy 里已启用（否则会报 ERROR:no_token）。

设计要点：
  · 返回体结构依赖服务端，脚本**不假设字段名**，而是从 JSON 里递归找
    `https://docs.qq.com/...` 形式的链接，找不到就报错退出 —— 避免"猜错字段名
    结果回填了一串 null"。
  · 只写 SiteLinks.cs，且每条替换"命中数必须恰好 1"；任何一条不符合就整批不写。
"""
import base64
import io
import json
import os
import re
import sys
import time
import urllib.request

ROOT = r"D:\code\all\_bh_xaml"
LEGAL_DIR = os.path.join(ROOT, "docs", "legal")
SITE_LINKS = os.path.join(ROOT, "StartRide", "SiteLinks.cs")
SKILL_DIR = (r"C:\Users\xyq20\.workbuddy\plugins\cache\workbuddy-builtin"
             r"\tencent-docs-plugin\5.6.2-wb.39298511.g37a65c0b.he233403f909a\skills\tencent-docs")

# (md 文件名, SiteLinks.cs 里的常量名, 文档标题, 总目录里的一句话说明)
DOCS = [
    ("01-user-agreement.md", "UserAgreementDoc", "StartRide 用户服务协议",
     "使用本软件即表示接受；约定双方权利义务、许可范围与责任边界"),
    ("02-privacy-policy.md", "PrivacyPolicyDoc", "StartRide 隐私政策",
     "逐项列明收集哪些信息、存到哪张表、保留多久、如何删除"),
    ("03-minor-protection.md", "MinorProtectionDoc", "StartRide 未成年人个人信息保护规则",
     "面向不满 14 周岁用户的专门规则与监护人须知"),
    ("04-disclaimer.md", "DisclaimerDoc", "StartRide 免责声明与风险提示",
     "第三方游戏、联机服务与数据安全的风险说明"),
    ("05-multiplayer-conduct.md", "MultiplayerConductDoc", "StartRide 联机服务使用规范",
     "联机房间码、言行守则、违规处置与举报方式"),
    ("06-third-party-notices.md", "ThirdPartyNoticesDoc", "StartRide 第三方组件与开源许可声明",
     "随包分发的每个第三方组件、版本号与许可证"),
    ("07-open-source-license.md", "OpenSourceLicenseDoc", "StartRide 开源许可与版权声明",
     "本项目自身的 GPL-3.0 许可全文与版权署名"),
]

# ⚠️ 返回体里给的是**协议相对地址**（`//docs.qq.com/doc/DUWJ…`），没有 scheme。
# 只匹配 `https://` 会静默 0 命中 —— 表现是"文档明明建好了，脚本却说找不到链接"，
# 而每次重跑都会再建一份重复文档。所以这里同时认带 scheme 和不带 scheme 两种写法。
URL_RE = re.compile(r"(?:(?:https?:)?//)docs\.qq\.com/[A-Za-z0-9/_\-.?=&%]+")

# ⚠️ 必须显式把文档站钉在 docs.qq.com 上，不能用网关票据里下发的默认域。
#
# 实测（2026-09-26）宿主票据 provider 返回的 apiBase 是 `https://www-docs.workbuddy.cn`
# —— 那是 WorkBuddy 自建的文档域，**公网 DNS 都不解析**（getaddrinfo failed），
# 本机经隧道也只会拿到 502。更要命的是：就算它通，生成出来的链接也是别人打不开的内网地址，
# 而用户可见的条款必须落在公网的 docs.qq.com 上。
# 好在网关的 Bearer 票据对 docs.qq.com 同样有效（实测 tools/list 正常返回）。
#
# 另外：不要试图用 --no-proxy 绕代理 —— 沙盒直连没有 DNS，只会 getaddrinfo failed。
os.environ["TDOC_API_BASE_URL"] = "https://docs.qq.com"
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) StartRideLegalCheck/1.0"
if SKILL_DIR not in sys.path:
    sys.path.insert(0, SKILL_DIR)

_MCP_DOCS = None


def load_skill():
    """把腾讯文档 skill 当模块导入（而不是每次 shell 出去起一个解释器）。

    为什么不用 `tencentdocs.py tdoc_call …`：
      1. 长正文要 base64 后从命令行传 —— 13 KB 的正文编码后 ~18 KB，
         逼近 Windows 32767 字符的命令行上限；
      2. 每调一次都要新起解释器 + 重新取一次票据。
    该模块本身就导出了 `call_tool` 供复用（`import_file.py` 也是这么用的）。
    """
    global _MCP_DOCS
    if _MCP_DOCS is None:
        import tencentdocs
        _MCP_DOCS = tencentdocs
    return _MCP_DOCS



def tdoc(tool, args, service="doc-mcp", tries=4):
    """调用腾讯文档 MCP 工具；返回解析后的 JSON（失败抛异常）。

    ⚠️ 重试不是多余的：沙盒走的是本机隧道代理（127.0.0.1:9955），偶发
       `Tunnel connection failed: 502 Bad Gateway` —— 隔一会儿重跑就好，
       但批处理里失败一次整批就中断，所以这里自己退避重试。
    """
    skill = load_skill()
    last = ""
    for attempt in range(tries):
        result, err = skill.call_tool(service, tool, args)
        if err == "no_token":
            raise RuntimeError("腾讯文档连接器未启用：请先在 WorkBuddy 里授权腾讯文档连接器。")
        if err:
            last = str(err)
            print("      请求异常（第 %d/%d 次），%d 秒后重试：%s"
                  % (attempt + 1, tries, 2 + attempt * 3, last[:110]))
            time.sleep(2 + attempt * 3)
            continue
        if isinstance(result, dict) and result.get("error"):
            raise RuntimeError("腾讯文档返回错误：%s"
                               % json.dumps(result["error"], ensure_ascii=False)[:400])
        return result
    raise RuntimeError("腾讯文档请求失败：%s" % last)


def harvest_urls(node, sink):
    """递归收集返回体里出现的 docs.qq.com 链接。

    ⚠️ `content[].text` 里是**被转义的一层 JSON 字符串**，链接藏在里面；
       不把这一层解开就会漏掉。原始值还是协议相对的 `//docs.qq.com/…`。
    """
    if isinstance(node, str):
        for m in URL_RE.findall(node):
            sink.add(m)
        s = node.strip()
        if s.startswith("{"):
            try:
                harvest_urls(json.loads(s), sink)
            except ValueError:
                pass
    elif isinstance(node, dict):
        for v in node.values():
            harvest_urls(v, sink)
    elif isinstance(node, list):
        for v in node:
            harvest_urls(v, sink)


def verify_public(url):
    """真实匿名访问这份文档，返回 (可读?, 说明)。

    ⚠️ 为什么不能信 `manage.get_privilege`：`set_privilege` 调完**不报错**，
       但 `get_privilege` 仍然回 `policy: 0` —— 照它判断会得出"权限没设上"的
       假阴性；反过来也不能只看 set 的返回值，因为漏调那一步同样不留痕迹。
       （实测栽在过这上面：《用户服务协议》是加"建完设权限"之前建的，
         其余六份都设了，只有它 `canRead: false`，用户点开就是打不开。）

    真正的判据在文档页内嵌的初始状态里：
        authInfo.attribute.canRead —— 匿名访问时 isAuth=false、canRead=true
        才说明"任何人拿链接都能看"。
    """
    try:
        req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        with urllib.request.build_opener().open(req, timeout=30) as resp:
            if resp.status != 200:
                return False, "HTTP %d" % resp.status
            html = resp.read().decode("utf-8", "replace")
    except Exception as exc:  # noqa: BLE001
        return False, "抓取失败 %s: %s" % (type(exc).__name__, exc)

    for blob in re.findall(r'["\']([A-Za-z0-9+/]{150,}={0,2})["\']', html):
        try:
            raw = base64.b64decode(blob + "=" * ((4 - len(blob) % 4) % 4)).decode("utf-8", "replace")
            state = json.loads(raw)
        except Exception:  # noqa: BLE001
            continue
        attr = (state.get("authInfo") or {}).get("attribute")
        if isinstance(attr, dict) and "canRead" in attr:
            if attr.get("canRead"):
                return True, "匿名可读（isAuth=%s canEdit=%s）" % (attr.get("isAuth"), attr.get("canEdit"))
            return False, "匿名不可读（isAuth=%s）" % attr.get("isAuth")
    return False, "页面里找不到 authInfo（文档可能不存在）"


def pick_file_id(resp):
    """从返回体里找出新建文档的 file_id（设置权限要用它）。"""
    found = []

    def walk(node):
        if isinstance(node, dict):
            for k, v in node.items():
                if k == "file_id" and isinstance(v, str) and v:
                    found.append(v)
                walk(v)
        elif isinstance(node, list):
            for v in node:
                walk(v)
        elif isinstance(node, str):
            # content[].text 里是**被转义的一层 JSON 字符串**，得再解析一次
            s = node.strip()
            if s.startswith("{"):
                try:
                    walk(json.loads(s))
                except ValueError:
                    pass

    walk(resp)
    return found[0] if found else ""


def new_doc(title, markdown):
    """用 Markdown 建一篇新文档并把权限设为「所有人可读」，返回原始响应。

    ⚠️ 两个都不明显的点：
      1. 参数名是 `base64_markdown`（base64 后的正文），不是 `markdown` ——
         传错会得到 `-32602 … missing required parameters: [base64_markdown]`。
      2. **新建出来的文档默认不是公开的**。不显式设权限，用户点开只会看到
         "没有访问权限" —— 而这份文件是要给所有用户看的。所以建完立刻
         `manage.set_privilege policy=2`（2=所有人可读，3=所有人可编辑）。
         这是**另一个 service**（主服务 `tencent-docs`，工具名带点），别搞混。
    """
    payload = base64.b64encode(markdown.encode("utf-8")).decode("ascii")
    resp = tdoc("create_with_markdown", {"title": title, "base64_markdown": payload})

    file_id = pick_file_id(resp)
    if not file_id:
        raise RuntimeError("建文档成功但解析不出 file_id，无法设置公开权限：%s"
                           % json.dumps(resp, ensure_ascii=False)[:300])
    tdoc("manage.set_privilege", {"file_id": file_id, "policy": 2}, service="tencent-docs")

    urls = set()
    harvest_urls(resp, urls)
    ok, why = (False, "响应里没有链接")
    for u in urls:
        if "/doc/" in u:
            ok, why = verify_public("https:" + u if u.startswith("//") else u)
            break
    print("      权限已设为「所有人可读」（file_id=%s）；匿名校验：%s" % (file_id, why))
    if not ok:
        raise RuntimeError("文档建出来了但匿名打不开（%s），链接发给用户会显示无权限。" % why)
    return resp


def pick_url(resp):
    """从返回体里挑出新建文档的地址。

    只认 docs.qq.com 下的 doc / sheet / smartsheet 页面。返回体结构由服务端决定，
    所以这里是"递归找链接"而不是取某个固定字段 —— 猜字段名猜错就会回填一串 null。
    返回的永远补全成 `https://…`（原始值是协议相对的 `//docs.qq.com/…`）。
    """
    sink = set()
    harvest_urls(resp, sink)
    for kind in ("/doc/", "/smartsheet/", "/sheet/"):
        for u in sorted(sink):
            if kind in u:
                return "https:" + u if u.startswith("//") else u
    return ""


def read(path):
    with io.open(path, "rb") as fh:
        raw = fh.read()
    crlf = b"\r\n" in raw
    return raw.decode("utf-8-sig" if raw[:3] == b"\xef\xbb\xbf" else "utf-8").replace("\r\n", "\n"), crlf


def write(path, text, crlf):
    data = text.replace("\n", "\r\n") if crlf else text
    with io.open(path, "wb") as fh:
        fh.write(data.encode("utf-8"))


def current_value(source, const):
    m = re.search(r'public const string %s = "([^"]*)";' % re.escape(const), source)
    return m.group(1) if m else None


def build_index_html(links):
    """生成「总目录」正文（HTML）：一个把全部法律文件串起来的入口页。

    为什么要有它：首次运行弹窗与设置页都是清单，缺一个「先看这个，再从这儿进全部」
    的落点；而且腾讯文档之间互相跳转，比让用户逐个复制地址省事。

    为什么用 HTML 而不是 Markdown：目录会随着文件增减而变化，需要**原地刷新**同一篇
    文档（不然每加一份文件就多一份孤儿文档）。原地刷新走的是
    `overwrite_doc_with_html`，它只吃 HTML。
    """
    rows = []
    for i, (_f, const, title, blurb) in enumerate(DOCS, 1):
        url = links.get(const)
        name = title.replace("StartRide ", "")
        cell = '<a href="%s">%s</a>' % (url, name) if url else name
        rows.append("<tr><td>%02d</td><td>%s</td><td>%s</td></tr>" % (i, cell, blurb))

    return "\n".join([
        "<h1>StartRide 法律文件总目录</h1>",
        "<p>本页汇总 StartRide 启动器全部对外的条款与说明。首次启动时的《用户服务协议》"
        "确认弹窗，以及「全局设置 → 关于 → 版权及法律声明」里的每一项，指向的都是下列文件。</p>",
        "<p><strong>生效日期：2026-09-26</strong>　　最近更新：2026-09-26</p>",
        "<table><thead><tr><th>序号</th><th>文件</th><th>一句话说明</th></tr></thead><tbody>",
        "\n".join(rows),
        "</tbody></table>",
        "<h2>与软件版本的关系</h2>",
        "<p>这些文件<strong>独立于软件版本</strong>更新。条款发生实质变化时，改动会直接体现在"
        "本目录指向的页面上，不需要等待新版本发布；每次修改都保留历史版本记录，"
        "便于核对「某个时间点生效的是哪一版」。</p>",
        "<h2>疑问与申诉</h2>",
        "<ul>",
        "<li>对条款内容有疑问、或需要行使隐私政策中列明的权利，请通过项目主页 "
        '<a href="https://startride.top">startride.top</a> 的反馈入口联系我们。</li>',
        "<li>涉及个人信息的请求（查阅、更正、删除、撤回同意），我们会在 15 个工作日内答复。</li>",
        "</ul>",
        "<p><em>本目录由仓库内 </em><em>docs/legal/</em><em> 下的正文同步生成，如有出入以各文件正文为准。</em></p>",
    ])


def write_index(links, existing_url):
    """建或原地刷新「总目录」，返回总目录地址。

    - 还没有 → 先建一篇（正文随便给个占位），再按 HTML 覆盖，保证与后续刷新格式一致；
    - 已有 → 直接按 URL 原地覆盖，**链接不变**，不会产生孤儿文档。
    """
    html = build_index_html(links)
    payload = base64.b64encode(html.encode("utf-8")).decode("ascii")

    if not existing_url:
        resp = new_doc("StartRide 法律文件总目录", "# StartRide 法律文件总目录\n")
        existing_url = pick_url(resp)
        if not existing_url:
            raise RuntimeError("总目录建好了但解析不出地址：%s"
                               % json.dumps(resp, ensure_ascii=False)[:300])
    tdoc("overwrite_doc_with_html", {"file_url": existing_url, "base64_html_text": payload})
    ok, why = verify_public(existing_url)
    if not ok:
        raise RuntimeError("总目录匿名打不开（%s）" % why)
    return existing_url


def fill(source, const, url):
    pattern = re.compile(r'(public const string %s = ")([^"]*)(";)' % re.escape(const))
    hits = pattern.findall(source)
    if len(hits) != 1:
        raise RuntimeError("SiteLinks.cs :: 常量 %s 命中 %d 次（应为 1）" % (const, len(hits)))
    return pattern.sub(lambda m: m.group(1) + url + m.group(3), source, count=1)


def verify_all(site_links):
    """`--verify`：把 SiteLinks.cs 里现存的每个法律文档地址重新匿名抓一遍。

    条款是"点开就能看"才算数，所以改完正文、或者手改过权限之后，
    跑这个比看 `get_privilege` 靠谱。
    """
    source, _ = read(site_links)
    consts = [c for _f, c, _t, _b in DOCS] + ["IndexDoc"]
    bad = 0
    for const in consts:
        url = current_value(source, const)
        if not url:
            print("  %-24s （空）" % const)
            continue
        ok, why = verify_public(url)
        bad += 0 if ok else 1
        print("  %-24s %s  %s" % (const, "OK  " if ok else "FAIL", why))
    print("\n%s" % ("全部可匿名访问 ✅" if not bad else "%d 份打不开 ❌" % bad))
    return 1 if bad else 0


def main():
    force = "--force" in sys.argv
    if not os.path.isdir(SKILL_DIR):
        print("FAIL 找不到腾讯文档 skill 目录：", SKILL_DIR)
        return 1
    if "--verify" in sys.argv:
        return verify_all(SITE_LINKS)

    source, crlf = read(SITE_LINKS)
    planned = []
    links = {}

    for filename, const, title, _blurb in DOCS:
        existing = current_value(source, const)
        path = os.path.join(LEGAL_DIR, filename)
        if not os.path.exists(path):
            print("SKIP  源文件不存在：%s" % filename)
            continue
        if existing and not force:
            print("SKIP  %-28s 已有链接 %s" % (const, existing))
            links[const] = existing
            continue

        markdown = io.open(path, encoding="utf-8").read()
        print("上传 %-28s %s …" % (const, title))
        try:
            resp = new_doc(title, markdown)
        except Exception as exc:
            print("FAIL  %s -> %s" % (const, exc))
            return 1

        url = pick_url(resp)
        if not url:
            print("FAIL  %s 的返回里找不到 docs.qq.com 链接，原始返回：" % const)
            print(json.dumps(resp, ensure_ascii=False)[:800])
            return 1
        print("      -> %s" % url)
        links[const] = url
        planned.append((const, url))

    # ── 总目录：等各份都拿到地址后再生成，这样目录里的链接才是真的 ──────────────
    #    只要这一轮新上传过任何一份（或显式 --force），就刷新一次目录 ——
    #    否则加了文件而目录没更新，用户从总目录进不去新文件。
    index_existing = current_value(source, "IndexDoc")
    if force or planned or not index_existing:
        missing = [c for _f, c, _t, _b in DOCS if not links.get(c)]
        if missing:
            print("SKIP  总目录：还有 %d 份没有地址 %s" % (len(missing), missing))
        else:
            print("%s %-28s %s …" % ("刷新" if index_existing else "创建", "IndexDoc",
                                     "StartRide 法律文件总目录"))
            try:
                url = write_index(links, index_existing)
            except Exception as exc:
                print("FAIL  IndexDoc -> %s" % exc)
                return 1
            print("      -> %s" % url)
            links["IndexDoc"] = url
            if url != index_existing:
                planned.append(("IndexDoc", url))
    else:
        print("SKIP  %-28s 已有链接 %s" % ("IndexDoc", index_existing))

    if not planned:
        print("\n没有需要回填的内容，SiteLinks.cs 未改动。")
        return 0

    for const, url in planned:
        source = fill(source, const, url)
    write(SITE_LINKS, source, crlf)
    print("\n已回填 SiteLinks.cs：")
    for const, url in planned:
        print("  %-28s %s" % (const, url))
    print("\n下一步：重新构建（dotnet build StartRide.csproj -c Release）打包发版。")
    return 0


if __name__ == "__main__":
    sys.exit(main())

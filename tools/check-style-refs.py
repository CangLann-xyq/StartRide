# -*- coding: utf-8 -*-
"""样式字典「前向 StaticResource 引用」检查器。

背景（P0 教训）：
    ControlStyles.Page.xaml 里的 RideWideListBoxStyle 写了
        BasedOn="{StaticResource ListPageVirtualizedListBoxStyle}"
    但被引用的样式在 ControlStyles.Lists.xaml 中，而 Page 是先于 Lists 合并的。
    StaticResource 在合并时解析 → 找不到 → MainWindow 加载即抛 XamlParseException，
    启动器 rc=-1 直接起不来。

规则：
    合并顺序第 N 个文件，只能用 StaticResource 引用「第 1..N 个」文件里定义的键
    （以及更早合并的非 styles 字典，如 App.xaml 里先于 styles 的资源）。
    DynamicResource 不受影响（运行时解析）。

用法:
    python tools/check-style-refs.py          # 扫描并报告（有跨文件前向引用时退出码 1）
    python tools/check-style-refs.py --json   # 机器可读

建议在改完任何 styles/*.xaml 后跑一次；CI 里可直接用它当门禁。
"""
import json
import os
import re
import sys

# 脚本在 <repo>/tools/ 下，仓库根 = 上一级
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STYLES = os.path.join(ROOT, "styles")

KEY_RE = re.compile(r'x:Key\s*=\s*"([^"]+)"')
# {StaticResource X} / {StaticResource X, ...}
REF_RE = re.compile(r'\{StaticResource\s+([A-Za-z0-9_.]+)')
# x:Static 不算
MERGE_RE = re.compile(r'Source\s*=\s*"pack://application:,,,/StartRide;component/([^"]+)"', re.I)
# 非 styles 目录里、且在 styles 之前合并的字典（App.xaml / resources/themes）
EXTRA_RE = re.compile(r'Source\s*=\s*"(?:pack://application:,,,/StartRide;component/)?([^"]*\.xaml)"', re.I)


def read(path):
    with open(path, "r", encoding="utf-8-sig", errors="replace") as fh:
        return fh.read()


def normalize(rel):
    return rel.replace("\\", "/").lstrip("./")


def keys_of(text):
    return set(KEY_RE.findall(text))


def refs_of(text):
    return set(REF_RE.findall(text))


def ref_lines(text, key):
    """返回该键被 StaticResource 引用的行号列表。"""
    out = []
    for n, line in enumerate(text.splitlines(), 1):
        if re.search(r'\{StaticResource\s+' + re.escape(key) + r'\b', line):
            out.append(n)
    return out


def key_lines(text, key):
    """返回该键被定义的行号。"""
    out = []
    for n, line in enumerate(text.splitlines(), 1):
        if re.search(r'x:Key\s*=\s*"' + re.escape(key) + r'"', line):
            out.append(n)
    return out


def main():
    as_json = "--json" in sys.argv

    # 1) styles 合并顺序：以 ControlStyles.xaml 为准；它自己提到的顺序就是权威
    index_path = os.path.join(STYLES, "controlstyles.xaml")
    if not os.path.exists(index_path):
        index_path = os.path.join(STYLES, "ControlStyles.xaml")
    index_text = read(index_path)
    merged = [normalize(m) for m in MERGE_RE.findall(index_text)]

    # 2) App.xaml 里 styles 之前合并的字典（主题色/共享资源）
    app_paths = []
    for cand in ("Launcher.App.App.xaml", "Launcher.App.xaml", "App.xaml"):
        p = os.path.join(ROOT, cand)
        if os.path.exists(p):
            app_paths.append(p)
    app_merged = []
    for p in app_paths:
        t = read(p)
        for m in EXTRA_RE.findall(t):
            m = normalize(m)
            if "/styles/" not in m.lower() and "controlstyles" not in m.lower():
                if m not in app_merged:
                    app_merged.append(m)
    # 主题目录整体视为 styles 之前可用（Shared/Dark/Light/Accents 等）
    themes_dir = os.path.join(ROOT, "resources", "themes")
    if os.path.isdir(themes_dir):
        for name in sorted(os.listdir(themes_dir)):
            if name.lower().endswith(".xaml"):
                rel = "Resources/Themes/" + name
                if rel not in app_merged:
                    app_merged.append(rel)
        accents = os.path.join(themes_dir, "accents")
        if os.path.isdir(accents):
            for name in sorted(os.listdir(accents)):
                if name.lower().endswith(".xaml"):
                    rel = "Resources/Themes/Accents/" + name
                    if rel not in app_merged:
                        app_merged.append(rel)

    # 3) 先累积「更早」的键集合（App.xaml 里 styles 之前的 + 已知基础资源）
    available = set()
    for rel in app_merged:
        p = os.path.join(ROOT, rel)
        if os.path.exists(p):
            available |= keys_of(read(p))

    order_report = []
    forward = []          # 跨文件前向引用（已证实：启动即崩）
    samefile = []         # 同文件内先引用后定义（隐患，需人工确认）
    missing = []          # 整个 styles 体系里都不存在（可能来自别的程序集，仅提示）

    all_keys = {}
    texts = {}
    for rel in merged:
        p = os.path.join(ROOT, rel)
        if not os.path.exists(p):
            p = os.path.join(ROOT, "styles", os.path.basename(rel))
        if not os.path.exists(p):
            order_report.append(("MISSING-FILE", rel))
            continue
        t = read(p)
        texts[rel] = t
        all_keys[rel] = keys_of(t)

    # 全体系键集合（用于区分"前向引用"和"根本不存在"）
    universe = set(available)
    for k in all_keys.values():
        universe |= k

    for i, rel in enumerate(merged):
        if rel not in texts:
            continue
        earlier = set(available)
        for j in range(i):
            earlier |= all_keys.get(merged[j], set())
        own = all_keys.get(rel, set())
        for ref in sorted(refs_of(texts[rel])):
            if ref in earlier:
                continue
            if ref in own:
                # 同文件内：WPF 要求「先定义后引用」。只有引用行 < 定义行 才是真隐患。
                defs = key_lines(texts[rel], ref)
                uses = ref_lines(texts[rel], ref)
                if defs and uses and min(uses) < min(defs):
                    samefile.append({"file": rel, "key": ref, "uses": uses, "defs": defs})
            elif ref in universe:
                forward.append({"file": rel, "key": ref, "definedIn": [f for f, ks in all_keys.items() if ref in ks]})
            else:
                missing.append({"file": rel, "key": ref})
        available = earlier | own
        order_report.append((i, rel, len(own)))

    if as_json:
        print(json.dumps({"forward": forward, "samefile": samefile, "missing": missing}, ensure_ascii=False, indent=2))
        return 1 if forward else 0

    print("== styles 合并顺序 ==")
    for row in order_report:
        if row[0] == "MISSING-FILE":
            print("  [!] 文件缺失:", row[1])
        else:
            print("  %d. %s  (%d 个键)" % (row[0] + 1, row[1], row[2]))

    print()
    if forward:
        print("== [ERROR] 跨文件前向 StaticResource 引用（会导致启动即崩）==")
        for f in forward:
            print("  %s\n     引用 %s —— 定义在 %s （合并更晚）"
                  % (f["file"], f["key"], ", ".join(f["definedIn"])))
    else:
        print("== 跨文件前向引用: 无 ==")

    if samefile:
        print()
        print("== [WARN] 同文件内先引用后定义（真隐患，需人工确认）==")
        for f in samefile:
            print("  %s  引用 %s（行 %s），但定义在行 %s"
                  % (f["file"], f["key"], f["uses"], f["defs"]))
    else:
        print()
        print("== 同文件内先引用后定义: 无 ==")

    if missing:
        print()
        print("== 本体系内未定义（可能来自其它程序集，仅提示）==")
        for m in missing:
            print("  %s -> %s" % (m["file"], m["key"]))

    return 1 if forward else 0


if __name__ == "__main__":
    sys.exit(main())

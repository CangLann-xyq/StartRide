"""导出 Strings.resx 全部条目为 TSV，并按关键词筛查 Minecraft 语境文案。

用法:
  python strings_scan.py dump  <out.tsv>
  python strings_scan.py scan  [关键词...]
"""
import re
import sys
import xml.etree.ElementTree as ET

RESX = r"D:\code\all\_bh_xaml\StartRide.App.Resources.Strings.resx"

# 需要改写的语境词 → 含义
TERMS = [
    "Minecraft", "minecraft", "我的世界",
    "Terracotta", "陶瓦", "EasyTier",
    "Java", "Forge", "Fabric", "Quilt", "NeoForge", "OptiFine",
    "Modrinth", "CurseForge",
    "局域网世界", "游戏世界", "世界", "存档",
    "正版", "Microsoft", "Xbox",
]


def load():
    tree = ET.parse(RESX)
    root = tree.getroot()
    out = []
    for data in root.findall("data"):
        name = data.get("name")
        val_el = data.find("value")
        val = val_el.text if val_el is not None and val_el.text else ""
        out.append((name, val))
    return out


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "scan"
    items = load()

    if cmd == "dump":
        out = sys.argv[2] if len(sys.argv) > 2 else r"D:\code\all\_bh_xaml\_tools\strings.tsv"
        with open(out, "w", encoding="utf-8") as f:
            for k, v in items:
                f.write(k + "\t" + v.replace("\n", "\\n") + "\n")
        print(f"{len(items)} 条 -> {out}")
        return

    terms = sys.argv[2:] or TERMS
    if len(sys.argv) > 2:
        terms = [sys.argv[2]]
    pat = re.compile("|".join(re.escape(t) for t in terms))
    hits = [(k, v) for k, v in items if pat.search(v)]
    print(f"总 {len(items)} 条，命中 {len(hits)} 条：\n")
    for k, v in hits:
        print(f"{k}\t{v}")


if __name__ == "__main__":
    main()

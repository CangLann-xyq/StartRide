"""把反编译 csproj 里的素材项从 <EmbeddedResource LogicalName=...> 改成 WPF 的 <Resource>。

原因: EmbeddedResource 生成的是普通程序集清单资源，WPF 的
      pack://application:,,,/Asm;component/<path> 只在 Asm.g.resources 里查找，
      因此图标/像素着色器全部取不到 → 界面无图标、模糊特效降级。

用法: python fix_assets.py [--apply]
"""
import re
import sys

CSPROJ = r"D:\code\all\_bh_xaml\StartRide.csproj"

PAT = re.compile(r'^(\s*)<EmbeddedResource Include="(?P<inc>[^"]+)" LogicalName="[^"]+" />\s*$')


def main():
    apply = "--apply" in sys.argv
    with open(CSPROJ, "r", encoding="utf-8") as f:
        lines = f.readlines()

    out, hit, kept = [], 0, []
    for ln in lines:
        m = PAT.match(ln)
        if not m:
            out.append(ln)
            continue
        inc = m.group("inc")
        norm = inc.replace("\\", "/")
        if norm.startswith("assets/") or norm.startswith("effects/"):
            out.append(f'{m.group(1)}<Resource Include="{inc}" />\n')
            hit += 1
        else:
            out.append(ln)
            kept.append(inc)

    print(f"转为 <Resource>: {hit} 项")
    print(f"保留 <EmbeddedResource>: {kept}")
    if apply:
        with open(CSPROJ, "w", encoding="utf-8", newline="\n") as f:
            f.writelines(out)
        print("已写入", CSPROJ)
    else:
        print("(试运行，加 --apply 生效)")


if __name__ == "__main__":
    main()

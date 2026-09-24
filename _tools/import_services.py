"""把 startride-wpf 的联机服务层搬进反编译得到的启动器工程，并把命名空间改为 StartRide.Core。

用法: python import_services.py [--apply]
"""
import os
import re
import shutil
import sys

SRC = r"D:\code\all\startride-wpf\Services"
DST = r"D:\code\all\_bh_xaml\StartRide"

# 服务层自包含，可整块搬（已确认零 Pages/Controls/Strings 依赖）
FILES = [
    "ApiService.cs",
    "AppSettings.cs",
    "AppState.cs",
    "BuildInfo.cs",
    "DownloadManager.cs",
    "GameLauncher.cs",
    "InstanceStore.cs",
    "LuaBridge.cs",
    "ModInstaller.cs",
    "MultiplayerSession.cs",
    "RelayClient.cs",
    "WebSocketTransport.cs",
]


def main():
    apply = "--apply" in sys.argv
    if not apply:
        print("(试运行，加 --apply 生效)")
        return

    os.makedirs(DST, exist_ok=True)
    for name in FILES:
        src = os.path.join(SRC, name)
        with open(src, "r", encoding="utf-8-sig") as f:
            text = f.read()
        text = text.replace("namespace StartRide.WPF.Services", "namespace StartRide.Core")
        text = text.replace("using StartRide.WPF.Services;", "using StartRide.Core;")
        text = text.replace("StartRide.WPF.Services.", "StartRide.Core.")
        dst = os.path.join(DST, name)
        with open(dst, "w", encoding="utf-8", newline="\r\n") as f:
            f.write(text)
        print(f"  搬入 {name}")
    print(f"共 {len(FILES)} 个文件 -> {DST}")


if __name__ == "__main__":
    main()

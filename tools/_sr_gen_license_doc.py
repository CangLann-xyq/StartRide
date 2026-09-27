# -*- coding: utf-8 -*-
"""由仓库 LICENSE 生成 docs/legal/07-open-source-license.md。

为什么用脚本生成而不是手写：GPL-3.0 全文 35 KB，手抄一遍既费时又必然出错；
而且以后换许可版本时，改 LICENSE 重跑一次就好，正文不会和仓库脱节。

用法：python tools/_sr_gen_license_doc.py
"""
import io
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LICENSE = os.path.join(ROOT, "LICENSE")
OUT = os.path.join(ROOT, "docs", "legal", "07-open-source-license.md")

HEADER = """# StartRide 开源许可与版权声明

**版本：1.0　生效日期：2026 年 9 月 26 日**

> 本文件说明 StartRide 启动器（以下称「本软件」）的版权归属与开源许可方式，
> 并在第三节附上 GNU 通用公共许可证第 3 版的**完整文本**。
> 本文件是《StartRide 用户服务协议》的组成部分。

---

## 一、版权声明

| 项目 | 内容 |
|---|---|
| 软件名称 | StartRide 启动器（StartRide Launcher） |
| 版权所有者 | 肖又祺 |
| 版权声明 | Copyright © 2026 肖又祺 · StartRide |
| 源码地址 | https://github.com/CangLann-xyq/StartRide |
| 问题反馈 | https://startride.top/feedback.html |

本软件是自由软件，不是公共领域软件。在遵守许可协议的前提下，你可以自由使用、
修改和再分发，但**必须保留上述版权声明与许可声明**。

## 二、许可方式

本软件依据 **GNU 通用公共许可证第 3 版（GNU GPL-3.0）** 发布。
许可全文见本文件第三节，随软件分发的压缩包内也包含一份 `LICENSE` 文件。

**要点提示**（仅为方便理解，不构成对许可文本的解释；如有歧义，以第三节的英文原文为准）：

- 你可以为任何目的运行本软件，包括商业用途，无需额外授权；
- 你可以修改本软件并分发修改后的版本，但分发时**必须一并提供完整源码**；
- 你分发的衍生作品**必须以同样的 GPL-3.0 协议授权**，不能改成闭源协议；
- 你必须保留原作者的版权声明与「无担保」声明；
- 本软件**不提供任何担保**（详见第三节第十七条）。

## 三、GNU 通用公共许可证第 3 版 · 完整文本

以下为英文原文。中文翻译仅供参考，若与英文原文有出入，**以英文原文为准**。

```text
"""

FOOTER = """```

---

## 四、关于第三方组件

本软件分发的部分第三方组件由各自的许可协议授权，与上述 GPL-3.0 无关。
逐项清单见《StartRide 第三方组件与开源许可声明》。

---

*本文件由仓库根目录的 `LICENSE` 文件自动生成，请勿手工编辑；
如需修改，请改 `LICENSE` 后重新运行 `tools/_sr_gen_license_doc.py`。*
"""


def main():
    if not os.path.exists(LICENSE):
        print("FAIL 找不到 LICENSE：", LICENSE)
        return 1

    text = io.open(LICENSE, encoding="utf-8").read().replace("\r\n", "\n").rstrip("\n")
    body = HEADER + text + "\n" + FOOTER
    io.open(OUT, "w", encoding="utf-8", newline="\n").write(body)
    print("已生成 %s  (%d 字符, GPL 正文 %d 行)"
          % (os.path.relpath(OUT, ROOT), len(body), len(text.splitlines())))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

# StartRide 法律文件（正文源）

本目录是 StartRide 全部对外法律文件的**正文源**，同时也是**运行时唯一的正文来源**。

| 序号 | 文件 | 生效日期 |
|---|---|---|
| 01 | [用户服务协议](01-user-agreement.md) | 2026-09-26 |
| 02 | [隐私政策](02-privacy-policy.md) | 2026-09-26 |
| 03 | [未成年人个人信息保护规则](03-minor-protection.md) | 2026-09-26 |
| 04 | [免责声明与风险提示](04-disclaimer.md) | 2026-09-26 |
| 05 | [联机服务使用规范](05-multiplayer-conduct.md) | 2026-09-26 |
| 06 | [第三方组件与开源许可声明](06-third-party-notices.md) | 2026-09-26 |
| 07 | [开源许可与版权声明](07-open-source-license.md) | 2026-09-26 |

> ⚠️ `07-open-source-license.md` 是**生成物**，别手改 —— 改根目录的 `LICENSE`，
> 然后重跑 `python tools/_sr_gen_license_doc.py`。

## 这些正文是怎么进到软件里的（2.11.0 起）

1. `StartRide.csproj` 把本目录的 `*.md` 作为**嵌入资源**打进程序集：

   ```xml
   <EmbeddedResource Include="docs\legal\*.md" Exclude="docs\legal\README.md"
                     LogicalName="StartRide.App.Resources.Legal.%(Filename)%(Extension)" />
   ```

   → 本文件（README.md）**不进包**，它是给维护者看的说明。
2. `StartRide/LegalDocuments.cs` 是唯一的条目清单（首次运行弹窗与设置页都遍历它）。
3. `StartRide/LegalDocumentContent.cs` 按资源名从自己的程序集里读正文；读不到就返回空串
   （条款页打不开可以补，启动器崩了没法补）。
4. `StartRide.App.Markdown/MarkdownParser.cs` 把正文切成块，内置阅读器
   （`LegalReaderViewModel` + `LegalReaderPanel`）渲染出来。

**因此：改完条款必须重新构建发版才会生效**，不存在"改完线上就变"的情况。
"用户在何时同意的是哪一版"由 `LauncherSettings` 里记录的同意时间 + 当时的发行版本共同确定。

> 历史：2026-09-26 ~ 2.11.0 之前，正文曾同时上传到腾讯文档、由启动器跳浏览器打开。
> 有两个问题：离线打不开；网络差时点开是白屏，等于"同意了一份自己看不到的文件"。
> 现在只走内置阅读器，腾讯文档那条链已经整条删掉（含 `SiteLinks.Legal` 与同步脚本）。

## Markdown 支持范围

阅读器只为本目录这七份文件服务，**不要**指望它是完整实现。已支持的写法：

| 类别 | 写法 |
|---|---|
| 标题 | `#` ~ `######`（`#` 一级标题渲染时会被去掉，因为阅读器头部已显示标题） |
| 段落 | 普通行；连续行会合并成一段（软换行不产生断行） |
| 列表 | `-` / `*` / `+`；`1.` / `1)`；支持一层 3 空格缩进嵌套 |
| 引用 | `>` |
| 代码块 | ```` ``` ```` |
| 表格 | 2 列 / 3 列，第二行需有 `\|---\|` 分隔行 |
| 分隔线 | `---` |
| 行内 | `**加粗**`、`*斜体*` / `_斜体_`、`` `行内代码` ``、`[文字](链接)` |

写了范围外的语法不会报错，只会以原文显示。

## 数据库字段与《隐私政策》的对应关系

《隐私政策》第 2 条列的信息种类必须与服务端**实际落库的字段**一致。
服务端建表语句在 `storm-server/migrate_v8_startride_cloud.sql`：

| 表 | 落库字段 |
|---|---|
| `startride_users` | `username`、`password_hash`、`steam_id`、`avatar`、`token`、`token_expire`、`owns_beamng`、`beamng_playtime_min`、`created_at`、`updated_at` |
| `startride_sync` | `username`、`sync_key`、`sync_value`、`updated_at` |
| `startride_ugc` | `username`、`kind`、`target_id`、`rating`、`content`、`created_at` |
| `startride_rooms` | `id`、`name`、`mode`、`host`、`host_ip`、`host_port`、`map`、`capacity`、`players`、`created`、`last_beat` |

⚠️ **加表/加字段时一定要回来核对《隐私政策》第 2 条**。
"政策里没写但实际在收"是这类文件唯一真正的漏洞。

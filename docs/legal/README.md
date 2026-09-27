# StartRide 法律文件（正文源）

本目录是 StartRide 全部对外法律文件的**正文源**，同时也是**运行时唯一的正文来源**。

| 序号 | 文件 | 生效日期 |
|---|---|---|
| 01 | [用户服务协议](01-user-agreement.md) | 2026-09-26 |
| 02 | [隐私政策](02-privacy-policy.md) | 2026-09-27（v2.1 修订） |
| 03 | [未成年人个人信息保护规则](03-minor-protection.md) | 2026-09-26 |
| 04 | [免责声明与风险提示](04-disclaimer.md) | 2026-09-26 |
| 05 | [联机服务使用规范](05-multiplayer-conduct.md) | 2026-09-26 |
| 06 | [第三方组件与开源许可声明](06-third-party-notices.md) | 2026-09-26 |
| 07 | [开源许可与版权声明](07-open-source-license.md) | 2026-09-26 |

> ⚠️ `07-open-source-license.md` 是**生成物**，别手改 —— 改根目录的 `LICENSE`，
> 然后重跑 `python tools/_sr_gen_license_doc.py`。

## 这些正文是怎么进到软件里的

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

> 历史：早期正文曾同时上传到腾讯文档、由启动器跳浏览器打开。
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
| `startride_configs` | `config_key`、`file_name`、`content`、`size_bytes`、`sha256`、`game_version`、`uploader`、`updated_at` —— **不含用户个人信息**：`POST /config/upload` 需要 `x-sr-config-key` 共享密钥，只有运营者能上传（见 `upload_startride_configs.py`） |

**不落库的两项**（别以为漏了）：

- **房间聊天**（`POST/GET /rooms/:id/chat`）：服务端用**内存 `Map`** 暂存、上限 `CHAT_KEEP = 100` 条，
  房间失活后随 TTL 一起消失，**不写数据库**。所以《隐私政策》第 2 条写成
  「仅在内存中转发、不写入数据库、不做长期留存」。
- **中继通道**（`relay-server.js`，4444/7777/7788）：纯转发，无数据库、无落盘。

⚠️ **加表/加字段时一定要回来核对《隐私政策》第 2 条**。
"政策里没写但实际在收"是这类文件唯一真正的漏洞。

> **2026-09-27 复核记录（隐私政策 v2.0 → v2.1）**：把上面 4 张表 + `startride_configs` 的字段，
> 逐个与《隐私政策》第 2 条对照，又与客户端 `ApiService.cs` 的**全部接口调用**对照，结论：
> - 落库字段**全部已被第 2 条覆盖**，没有"在收但没写"的项；
> - 补上了原来漏写的**联机聊天内容**（经服务器内存转发）；
> - **更正**了 §4.1 关于 Steam 的不实描述 —— 客户端实际**不走 OpenID**
>   （`SteamOpenIdLoginUrl` 在客户端**没有任何调用点**，是死代码；服务端 `/auth/steam/login`
>   虽然存在但没被用），实际路径是：本机读 `loginusers.vdf` 取 SteamID64+昵称 →
>   `POST /auth/steam` → **服务器**调 Steam Web API 查 AppID 284160 的拥有与时长；
> - 新增 `www.beamng.com`（`BeamNgRepositoryClient.cs` 里 3 处直连）为第三方出口；
> - §6.6 诊断包原只声明"不含令牌"，已改为**如实列举**（含计算机名、玩家昵称、游戏路径）。
>
> 教训：**写"我们通过 OpenID 验证身份"之前先去客户端找调用点** —— 反编译来的工程里
> 死代码很多，照着上游文档写会写出不存在的处理行为。

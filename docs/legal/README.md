# StartRide 法律文件（正文源）

本目录是 StartRide 全部对外法律文件的**正文源**。

| 序号 | 文件 | 生效日期 | 腾讯文档 |
|---|---|---|---|
| 01 | [用户服务协议](01-user-agreement.md) | 2026-09-26 | 见 `StartRide/SiteLinks.cs` 的 `Legal.UserAgreementDoc` |
| 02 | [隐私政策](02-privacy-policy.md) | 2026-09-26 | `Legal.PrivacyPolicyDoc` |
| 03 | [未成年人个人信息保护规则](03-minor-protection.md) | 2026-09-26 | `Legal.MinorProtectionDoc` |
| 04 | [免责声明与风险提示](04-disclaimer.md) | 2026-09-26 | `Legal.DisclaimerDoc` |
| 05 | [联机服务使用规范](05-multiplayer-conduct.md) | 2026-09-26 | `Legal.MultiplayerConductDoc` |
| 06 | [第三方组件与开源许可声明](06-third-party-notices.md) | 2026-09-26 | `Legal.ThirdPartyNoticesDoc` |
| 07 | [开源许可与版权声明](07-open-source-license.md) | 2026-09-26 | `Legal.OpenSourceLicenseDoc` |

> 00–07 之外的「总目录」页由 `tools/_sr_tencent_legal.py` 自动生成并原地刷新，
> 没有对应源文件（正文里只有链接表，没有独立内容）。
>
> ⚠️ `07-open-source-license.md` 是**生成物**，别手改 —— 改根目录的 `LICENSE`，
> 然后重跑 `python tools/_sr_gen_license_doc.py`。

## 上传到腾讯文档

改动正文后执行：

```bash
python tools/_sr_tencent_legal.py            # 只补没传过的（幂等）
python tools/_sr_tencent_legal.py --force    # 全部重传（会换地址）
python tools/_sr_tencent_legal.py --verify   # 只校验现存的每一条是否"匿名可读"
```

脚本会自动回填 `StartRide/SiteLinks.cs`，所以**改完正文要重新构建发版**，
链接才会跟着更新（除非地址没变）。

⚠️ 两个必须知道的点，否则会踩：

1. **新建的文档默认不是公开的。** 脚本建完会调 `manage.set_privilege policy=2`
   （所有人可读）并**匿名抓一次页面确认 `canRead`**，不通过就报错。
   不要看 `manage.get_privilege` —— 它即使设置成功也回 `policy: 0`，会误判。
2. **文档站必须钉在 `docs.qq.com`。** 宿主票据 provider 默认下发的
   `www-docs.workbuddy.cn` 公网不解析，生成出来的链接用户打不开。

## 两处正文的关系

- **本目录的 Markdown**：随仓库分发，是**改动起点**。改条款请改这里。
- **腾讯文档上的同一份**：面向普通用户，国内直连可打开；启动器里的每个入口都指向它。
  地址收口在 `StartRide/SiteLinks.cs`，**留空时运行时会自动退回本目录的 Markdown**，
  所以不会出现"按钮点了是死链"。

改完条款的流程：

1. 改本目录的 Markdown；
2. 同步更新腾讯文档上对应的一份（标题里带版本号与生效日期）；
3. 腾讯文档地址变了才需要改 `SiteLinks.cs`；
4. 提交仓库。

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

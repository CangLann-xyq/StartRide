# Code signing policy

**Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).**

This page describes how StartRide release binaries are built, reviewed and
signed. It is the authoritative code signing policy for the project.

## Team roles

StartRide is developed and maintained by a single person. All signing-related
roles are therefore held by the same account, and multi-factor authentication is
enabled on it.

| Role | Member |
| --- | --- |
| Authors (may commit to `main`) | [CangLann-xyq](https://github.com/CangLann-xyq) |
| Reviewers (review contributions) | [CangLann-xyq](https://github.com/CangLann-xyq) |
| Approvers (approve signing requests) | [CangLann-xyq](https://github.com/CangLann-xyq) |

Commit access is limited to the account listed above. External contributions
arrive as pull requests and are reviewed by the maintainer before being merged.
Every signing request is approved manually by the Approver listed above after
verifying that the artifacts were produced by the project's own build pipeline
from the source code in this repository.

## What is signed

Only artifacts built by this project from the source code in this repository are
submitted for signing. For each release, the signed set is:

- `StartRide.exe` — the launcher itself
- `StartRide-Uninstall.exe` — the uninstaller shipped inside the installer
- `StartRide-Setup-<version>-win-x64.exe` — the installer

Third-party runtime libraries published by other open-source projects
(`CommunityToolkit.Mvvm`, `Serilog` and others, listed in
[`docs/legal/06-third-party-notices.md`](legal/06-third-party-notices.md)) are
included as-is in the signed packages. They are **not** signed with this
certificate, and their own maintainers are the source of those binaries.

Every signed binary carries enforced file metadata: the product name is
`StartRide` and the product version matches the release version, so that the
version embedded in the signature, the release tag and the version shown by the
launcher always agree.

## Build and release process

1. Release source is tagged in this repository (`v<version>`).
2. The release build is produced by the project's own build pipeline from that
   tag on GitHub-hosted runners
   ([`.github/workflows/release-sign.yml`](../.github/workflows/release-sign.yml)),
   in a clean environment, without manual modification of the artifacts.
3. The resulting artifacts are submitted to SignPath for signing and approval.
4. Signed artifacts are published to the
   [Releases](https://github.com/CangLann-xyq/StartRide/releases) page and to the
   official download page, [startride.top](https://startride.top).

SHA-256 checksums of every published file are listed on the download page, so
users can verify the file they downloaded matches the signed release.

## Privacy

StartRide offers optional online features (multiplayer rooms, Steam library
check, cloud sync of settings). Any information that leaves the user's computer
is described in the project's privacy policy:

- [Privacy policy](legal/02-privacy-policy.md)
- [Third-party components and open-source notices](legal/06-third-party-notices.md)

The program does not transfer anything to networked systems other than those
described in that policy.

## System changes and uninstallation

- The launcher starts games with the arguments the user has configured. It does
  not modify system settings silently; every change it makes to the game
  installation (installing or removing the multiplayer mod) is shown in the
  interface and can be turned off in settings.
- The installer registers the application in the standard Windows uninstall
  list. An uninstaller is placed in the installation directory next to
  `StartRide.exe`, and installation can be removed through Windows
  "Apps & features" as usual.

---

# 代码签名政策

**免费代码签名由 [SignPath.io](https://about.signpath.io) 提供，证书由 [SignPath Foundation](https://signpath.org) 签发。**

本页说明 StartRide 发行版二进制文件的构建、审核与签名方式。

## 团队角色

StartRide 由个人独立开发维护，因此签名相关的全部角色由同一账号承担，该账号已启用多因素认证。

| 角色 | 成员 |
| --- | --- |
| 作者（可提交到 `main`） | [CangLann-xyq](https://github.com/CangLann-xyq) |
| 审核者 | [CangLann-xyq](https://github.com/CangLann-xyq) |
| 批准者（批准签署请求） | [CangLann-xyq](https://github.com/CangLann-xyq) |

提交权限仅限上述账号。外部贡献以 Pull Request 形式提出，由维护者审核后合并。
每一次签署请求都由上表中的批准者在确认产物确实由本项目构建流水线、从本仓库源码构建后**人工批准**。

## 签名范围

仅提交由本项目从本仓库源码构建的产物用于签名。每次发行签名的文件为：

- `StartRide.exe` —— 启动器本体
- `StartRide-Uninstall.exe` —— 随安装包一同分发的卸载程序
- `StartRide-Setup-<版本>-win-x64.exe` —— 安装程序

其他开源项目发布的第三方运行时库（`CommunityToolkit.Mvvm`、`Serilog` 等，清单见
[`docs/legal/06-third-party-notices.md`](legal/06-third-party-notices.md)）随包原样分发，
**不使用本证书签名**，其来源为各自项目的维护者。

每个被签名的二进制都写入了强制校验的元数据：产品名称为 `StartRide`，产品版本与发行版本一致，
因此签名中的版本、发行标签与启动器显示的版本号始终相符。

## 构建与发布流程

1. 发行源码在仓库中以标签形式标记（`v<版本>`）。
2. 发行版本由本项目自己的构建流水线从该标签在 GitHub 托管运行器上构建
   （[`.github/workflows/release-sign.yml`](../.github/workflows/release-sign.yml)），
   产物不做任何手工修改。
3. 构建产物提交至 SignPath 完成签名与批准。
4. 已签名的产物发布到 [Releases](https://github.com/CangLann-xyq/StartRide/releases) 页面与官方下载页 [startride.top](https://startride.top)。

下载页会公示每个发布文件的 SHA-256 校验值，用户可据此核对下载到的文件与已签名发行版一致。

## 隐私

StartRide 提供可选的联网功能（联机房间、Steam 游戏持有校验、设置云端同步）。
任何离开用户电脑的信息都在隐私政策中说明：

- [隐私政策](legal/02-privacy-policy.md)
- [第三方组件与开源许可声明](legal/06-third-party-notices.md)

除该政策所述内容外，本程序不向其他联网系统传输任何信息。

## 系统更改与卸载

- 启动器按用户配置的参数启动游戏，不会静默修改系统设置；它对游戏目录所做的改动
  （安装或移除联机模组）都会在界面中提示，并可在设置中关闭。
- 安装程序会在 Windows 标准卸载列表中登记本应用；卸载程序位于安装目录中与
  `StartRide.exe` 同层，也可照常通过 Windows「应用和功能」卸载。

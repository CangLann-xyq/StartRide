# 代码签名流水线（SignPath）

本目录存放 **发行版签名** 相关的配置。签名由
[`.github/workflows/release-sign.yml`](../../.github/workflows/release-sign.yml)
在 GitHub 托管的运行器上完成。

> 为什么必须在 CI 里签：SignPath Foundation 的免费证书要求
> `Binary artifacts must be built from source code in a verifiable way`。
> 拿本机编译的产物去签是**不受理**的。因此本目录与工作流是申请通过后的唯一发版入口。

## 一、它做什么

```
读取 StartRide/BuildInfo.cs 里的版本号
  ↓
dotnet publish 主程序            → 提交签名 → 校验 Authenticode
  ↓
dotnet publish 卸载器            → 提交签名
  ↓
用「已签名的主程序」组装安装载荷 → dotnet publish 安装向导 → 提交签名
  ↓
生成 StartRide-<版本>-win-x64.zip 与 StartRide-Setup-<版本>-win-x64.exe
  ↓
创建 GitHub Release（可关闭）
```

**顺序不能颠倒**：安装向导把整包 zip 当嵌入资源。如果先打安装包再签名，
安装包里的 `StartRide.exe` 就是未签名的，用户装完运行照样显示「未知发布者」——
等于白签。工作流里 `组装安装载荷` 一步拿的是 `signed\app\`，不是 `build\app\`。

## 二、一次性准备（仅需在 SignPath 门户做一遍）

1. **创建项目**，记下 project slug。
2. **Artifact Configurations → Add → Custom**，把
   [`artifact-configuration.xml`](artifact-configuration.xml) 的内容原样粘进去，
   记下配置的 slug（本工作流默认用 `startride-binaries`，可在仓库变量里改）。
3. **创建签名策略**，建议两条：
   - `test-signing`（自动批准）—— 调通流水线用，不打扰发布；
   - `release-signing`（人工批准）—— 正式发版用。
4. **把 GitHub 设为受信任构建系统**（Trusted Build Systems → 添加 GitHub.com 并关联本仓库）。
5. **签发 API Token**：Profile → API Tokens，权限选 Submitter。

## 三、仓库里要配的变量

Settings → Secrets and variables → Actions：

| 名称 | 类型 | 值 |
|---|---|---|
| `SIGNPATH_API_TOKEN` | Secret | 上一步签发的 token |
| `SIGNPATH_ORGANIZATION_ID` | Secret | SignPath 组织 ID |
| `SIGNPATH_PROJECT_SLUG` | Variable | 项目 slug |
| `SIGNPATH_SIGNING_POLICY_SLUG` | Variable | `release-signing`（调试期填 `test-signing`） |

工作流里 `artifact-configuration-slug` 直接写死为 `startride-binaries`；
若门户里用了别的 slug，改工作流顶部的 `ARTIFACT_CONFIG` 环境变量。

## 四、怎么跑

Actions → **Release (signed)** → Run workflow。

- `version` 留空即从 `StartRide/BuildInfo.cs` 读取；填了则必须与源码一致，否则报错退出。
- `create_release` 设为 `false` 可以只跑构建和签名，不建 Release（首次调通建议这样）。

**每次运行会有 3 次签名请求，都需要人工批准**（`release-signing` 策略）。
工作流会在这里等待，等待超时已放宽到 3600 秒。批准入口在 SignPath 门户的
Signing Requests 页面，也会出现在工作流的日志里（`signing-request-web-url`）。

## 五、签完之后

签名产物会作为工作流 artifact `startride-signed-<版本>` 上传（保存 30 天）。
官网分发仍走本地脚本，下载那两个文件后：

```powershell
python _sr_deploy_update.py
```

## 六、已知坑

1. **免费档只支持 file-based 签名**（上传未签名产物 → 等签名 → 下载回来）。
   私钥不出 HSM 的 hash-based 模式属于付费的 Code Signing Gateway，别照着写。
2. **`release-signing` 需要人工批准** → 超时不够流水线会失败。别把
   `wait-for-completion-timeout-in-seconds` 改回默认的 600。
3. **三个 artifact 名字必须互不相同**。GitHub 的 `upload-artifact` 有已知问题：
   同名 artifact 会导致动作失败。
4. **`SetupVersion` 必须与 `BuildInfo.Version` 对齐**。SignPath 要求签名的二进制
   带有 product name / product version 元数据，且各次构建保持一致。
5. **不要改回本地 `_sr_*` 脚本去签名**。本地脚本继续负责「上传到官网」，
   签名只发生在 CI 里；否则 `verifiable build` 这一条就不成立了。
6. **首次运行大概率要调试**：本工程此前从未在 CI 里构建过。
   `third_party/launcher-libs/` 就是为了让 CI 能构建而收进仓库的
   （此前 11 个 `<Reference>` 指向仓库外的路径，公开仓库根本构建不了）。

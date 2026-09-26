# StartRide 第三方组件与开源许可声明

**版本：1.0　生效日期：2026 年 9 月 26 日**

> 本文件列明 StartRide 启动器（以下称「本软件」）中**实际分发**的第三方软件组件
> 及其许可信息，以及本项目自身的许可方式。
> 本文件是《StartRide 用户服务协议》的组成部分，仅为履行开源许可协议的署名要求而提供，
> **不构成对本文件所列任何项目的背书，也不代表其与本项目存在合作关系。**

---

## 一、本项目的许可方式

| 项目 | 内容 |
|---|---|
| 软件名称 | StartRide 启动器（StartRide Launcher） |
| 版权 | Copyright © 2026 肖又祺 · StartRide |
| 许可协议 | **GNU General Public License v3.0（GPL-3.0）** |
| 许可全文 | 随软件分发，另见仓库 `LICENSE` 文件 |
| 源码地址 | https://github.com/CangLann-xyq/StartRide |

**GPL-3.0 要点提示（非正式说明，以许可全文为准）**：

1. 你可以自由地运行、研究、修改和分发本软件；
2. 你分发本软件或其衍生作品时，**必须以 GPL-3.0 授权**，并向接收者提供**完整源代码**；
3. 你必须**保留原始版权声明与许可声明**，并说明你所做的修改；
4. 你必须向接收者提供 GPL-3.0 的完整文本；
5. 本软件**不提供任何担保**（GPL-3.0 第 15、16 条）；
6. 你**不得**使用「StartRide」名称、标识或域名暗示你的衍生作品获得本项目认可
   （名称与标识不在 GPL-3.0 的授权范围内，见《用户服务协议》第三条）。

## 二、随软件分发的第三方组件

下列组件随本软件一同分发。我们在此依照各自许可协议的要求，
保留其版权声明并致谢。

### 2.1 MIT 许可组件

| 组件 | 版本 | 版权归属 | 项目地址 |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.0 | Copyright (c) .NET Foundation and Contributors | https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | Copyright (c) Microsoft Corporation | https://github.com/dotnet/runtime |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.0 | Copyright (c) Microsoft Corporation | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Logging | 10.0.0 | Copyright (c) Microsoft Corporation | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Logging.Abstractions | 10.0.0 | Copyright (c) Microsoft Corporation | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Options | 10.0.0 | Copyright (c) Microsoft Corporation | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Primitives | 10.0.0 | Copyright (c) Microsoft Corporation | https://github.com/dotnet/runtime |
| System.Management | 8.0.0 | Copyright (c) .NET Foundation and Contributors | https://github.com/dotnet/runtime |
| SharpCompress | 0.49.1 | Copyright (c) Adam Hathcock | https://github.com/adamhathcock/sharpcompress |
| ICSharpCode.SharpZipLib | 1.4.2 | Copyright (c) 2000-2018 SharpZipLib Contributors | https://github.com/icsharpcode/SharpZipLib |
| Microsoft.Identity.Client (MSAL.NET) | 4.86.1 | Copyright (c) Microsoft Corporation | https://github.com/AzureAD/microsoft-authentication-library-for-dotnet |
| Microsoft.IdentityModel.Abstractions | 8.14.0 | Copyright (c) Microsoft Corporation | https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet |
| CmlLib.Core | 4.0.6 | Copyright (c) CmlLib Contributors | https://github.com/CmlLib/CmlLib.Core |
| CmlLib.Core.Auth.Microsoft | 3.3.1 | Copyright (c) CmlLib Contributors | https://github.com/CmlLib/CmlLib.Core.Auth.Microsoft |
| CmlLib.Core.Commons | 4.0.0 | Copyright (c) CmlLib Contributors | https://github.com/CmlLib/CmlLib.Core |
| XboxAuthNet | 3.0.4 | Copyright (c) XboxAuthNet Contributors | https://github.com/XboxAuthNet/XboxAuthNet |
| XboxAuthNet.Game | 1.4.1 | Copyright (c) XboxAuthNet Contributors | https://github.com/XboxAuthNet/XboxAuthNet |
| XboxAuthNet.Game.Msal | 0.1.3 | Copyright (c) XboxAuthNet Contributors | https://github.com/XboxAuthNet/XboxAuthNet |

### 2.2 Apache-2.0 许可组件

| 组件 | 版本 | 版权归属 | 项目地址 |
|---|---|---|---|
| Serilog | 4.2.0 | Copyright (c) Serilog Contributors | https://github.com/serilog/serilog |
| Serilog.Extensions.Logging | 7.0.0 | Copyright (c) Microsoft, Serilog Contributors | https://github.com/serilog/serilog-extensions-logging |
| Serilog.Sinks.File | 6.0.0 | Copyright (c) Serilog Contributors | https://github.com/serilog/serilog-sinks-file |
| IconPark（图标素材） | — | Copyright 2019-present Bytedance Inc. | https://github.com/bytedance/IconPark |

### 2.3 LGPL-2.1-or-later 许可组件

| 组件 | 版本 | 版权归属 | 项目地址 |
|---|---|---|---|
| SevenZip（7-Zip SDK 的 .NET 封装） | 19.0.0 | Copyright (c) Igor Pavlov（7-Zip SDK）；.NET 封装由 chrishaly 维护 | https://github.com/chrishaly/SevenZip |

> **关于 LGPL-2.1-or-later**：该组件以**动态链接（独立 DLL）**方式使用，
> 未与本项目代码静态合并。你可以自行替换该 DLL 文件。
> 如需该组件的完整源代码，请访问上述项目地址，或通过本文件末的联系方式索取。

### 2.4 微软专有许可组件

| 组件 | 版本 | 版权归属 | 许可方式 |
|---|---|---|---|
| Microsoft.Web.WebView2.Core | 1.0.1823.32 | Copyright (c) Microsoft Corporation | Microsoft 软件许可条款 |
| Microsoft.Web.WebView2.WinForms | 1.0.1823.32 | Copyright (c) Microsoft Corporation | Microsoft 软件许可条款 |

> WebView2 用于在软件内显示网页内容（如登录页面、说明页面）。
> 其运行时由 Microsoft Edge WebView2 Runtime 提供，适用微软的许可条款。

## 三、运行环境

| 组件 | 许可方式 | 说明 |
|---|---|---|
| .NET 8 运行时 | MIT License | 本软件以 framework-dependent 方式发布，需目标机器安装 .NET 8 桌面运行时。版权归 Microsoft Corporation 与 .NET Foundation 所有。 |
| Windows 桌面运行环境 | Microsoft 软件许可条款 | 本软件运行于 Windows 操作系统之上，不随包分发。 |

## 四、第三方游戏与商标

| 名称 | 权利归属 | 说明 |
|---|---|---|
| BeamNG.drive | BeamNG GmbH | **本软件为非官方第三方工具，与 BeamNG GmbH 无隶属、代理、赞助或合作关系。** 本软件不分发游戏本体。BeamNG.drive 的名称、商标、游戏内容与素材的全部权利归其权利人所有。 |
| Steam | Valve Corporation | Steam 为其权利人所有的商标。本软件通过 Steam 官方接口提供登录与账号信息读取功能，不使用其商标暗示任何关联或授权。 |
| GitHub | GitHub, Inc. | 本软件源代码托管于 GitHub，不使用其商标暗示任何关联或授权。 |

## 五、许可文本的获取

1. 上述各组件的许可全文，可在各项目的官方地址查阅。
2. 本软件随包提供了本项目自身的 `LICENSE` 文件（GPL-3.0 全文）。
3. 如你在本软件内无法找到某个组件的许可文本，或需要 7-Zip（LGPL 组件）的源代码，
   请通过第六节的邮箱联系我们，我们将在 **15 个工作日**内提供。
4. 本软件「设置 → 法律与许可 → 引用的项目」中也列出了主要第三方组件的版权与许可信息。

## 六、版权投诉

如你认为本软件侵犯了你的著作权、商标权或其他合法权益，请通过下列方式联系我们，
并提供权利证明、侵权内容位置与你的联系方式。我们将在收到有效通知后
**15 个工作日**内核查处理，必要时**立即移除相关内容**：

| 渠道 | 地址 |
|---|---|
| 电子邮箱 | cloudfur2026@qq.com |
| 项目主页 | https://startride.top |
| 问题反馈 | https://startride.top/feedback.html |

---

> 本文件是《StartRide 用户服务协议》的组成部分。
> 本文件所列组件的许可协议与本项目 GPL-3.0 许可**各自独立**；
> 第三方组件的许可条件不受本项目协议影响，本项目协议亦不得被解释为
> 对第三方组件许可条件的修改或限制。

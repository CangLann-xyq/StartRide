# StartRide

**BeamNG.drive 多人联机启动器** · Windows · C# / WPF (.NET 8)

StartRide 把「装模组 → 找房间 → 进游戏」这几件事收进一个窗口里。它自建了一套中继
服务，用房间码组队，不依赖任何第三方穿透工具，也不需要你在路由器上开端口。

---

## 功能

**联机**

- 自建中继：房主开房拿到一个四位房间码，其他人输码即进，无需端口映射
- 车辆物理在房主电脑上计算，中继只做转发，因此联机手感取决于房主机器
- 大厅实时显示成员与车辆数，房主可解散房间

**启动与实例**

- 自动探测 BeamNG.drive 安装位置（进程 → Steam 注册表与 libraryfolders.vdf → 卸载表 → 常见路径）
- 启动前自动安装/更新联机模组，退出后回收
- 游戏内存上限调节、配置文件完整性检查与一键修复

**资源管理**

- 车辆、模组（Mods）、回放三个管理器：列表 + 详情面板，可直接定位文件、复制路径
- 附件包体积统计与占用排序

**账户**

- Steam 账户登录（读取本机 Steam 登录态，可选联网校验游戏持有情况）
- 头像、联机 ID 与本机昵称

**其它**

- 云端同步：账户顺序、启动参数、设置项随账号漫游
- 游玩时长统计（含异常退出后的会话恢复）
- 诊断包导出：一键打包日志与配置，便于反馈问题（**不含任何账号凭据**）

---

## 构建

需要 **.NET 8 SDK** 或更高版本，Windows 10 1809+。

```powershell
dotnet build .\StartRide.csproj -c Release
```

产物位于 `bin\Release\net8.0-windows\`。首次构建会自动还原 NuGet 依赖。

> 若要发布单文件版本，使用 `dotnet publish -c Release -r win-x64 --self-contained false`。

### 改完样式后请跑一次引用检查

WPF 的 `{StaticResource}` 在**资源字典合并时**解析，因此 `styles/` 下靠后的字典里定义的样式，
不能被靠前的字典用 `BasedOn="{StaticResource ...}"` 引用——这种**跨文件前向引用**编译期不报错，
但会在 `MainWindow` 加载时抛 `XamlParseException`，表现为启动器一闪而过、退出码 `-1`。

```powershell
python .\tools\check-style-refs.py
```

它按 `styles/ControlStyles.xaml` 里的合并顺序，逐文件核对每个 `StaticResource` 引用是否可用，
有跨文件前向引用时退出码为 `1`，可直接用作 CI 门禁。

---

## 目录结构

| 路径 | 说明 |
| --- | --- |
| `StartRide/` | 自研模块：联机会话、中继客户端、模组安装、Steam 登录、云端同步、链接常量等 |
| `Mods/` | 游戏内 Lua 联机模组（打包进发行包） |
| `styles/` | 界面样式：按钮、输入、列表、导航、对话框、滚动 |
| `resources/themes/` | 主题与配色（深色 / 浅色 / 强调色） |
| `Assets/branding/` | 品牌图标与 Logo |
| `tools/` | 开发辅助脚本（样式引用检查等） |
| `StartRide.App.*` | 界面、视图模型与服务层 |

---

## 许可证与来源声明

本项目以 **GNU General Public License v3.0** 发布，详见 [LICENSE](LICENSE)。

StartRide 的界面层基于一个开源 Minecraft 启动器（GPL-3.0）的既有骨架改造而来。
依照 GPL-3.0，本项目同样以 GPL-3.0 授权，完整源代码在
[本仓库](https://github.com/CangLann-xyq/StartRide) 公开。

**属于独立实现的部分**：联机会话与中继协议、模组安装与版本校验、BeamNG 启动与运行时
管理、游玩时长统计、诊断包导出、云端同步、Steam 登录、以及全部车辆/模组/回放的管理界面。

界面层沿用上述骨架。那个启动器面向 Minecraft，其实例扫描、加载器选择、皮肤披风、
第三方认证等模块在 StartRide 中已不可达或移除；被替换为 BeamNG.drive 的对应实现。

第三方依赖及其许可证可在启动器「设置 → 关于」中查看。

---

## 法律文件

使用本软件前请阅读下列文件。**软件内「设置 → 版权及法律声明」以及首次启动的
同意条款弹窗里都能直接点开它们** —— 正文随安装包一起分发，由启动器内置的阅读器
直接渲染，**不跳浏览器、离线也能看**（2.11.0 起）。

| 文件 | 仓库源文 |
|---|---|
| 用户服务协议 | [docs/legal/01-user-agreement.md](docs/legal/01-user-agreement.md) |
| 隐私政策 | [docs/legal/02-privacy-policy.md](docs/legal/02-privacy-policy.md) |
| 未成年人个人信息保护规则 | [docs/legal/03-minor-protection.md](docs/legal/03-minor-protection.md) |
| 免责声明与风险提示 | [docs/legal/04-disclaimer.md](docs/legal/04-disclaimer.md) |
| 联机服务使用规范 | [docs/legal/05-multiplayer-conduct.md](docs/legal/05-multiplayer-conduct.md) |
| 第三方组件与开源许可声明 | [docs/legal/06-third-party-notices.md](docs/legal/06-third-party-notices.md) |
| 开源许可与版权声明（GPL-3.0 全文） | [docs/legal/07-open-source-license.md](docs/legal/07-open-source-license.md) |

`docs/legal/` 下的 Markdown 就是**唯一正文源**：`StartRide.csproj` 把它们作为嵌入资源
打进程序集，启动器运行时直接读自己程序集里的正文。所以**改完条款要重新构建发版才会生效**，
流程见 [docs/legal/README.md](docs/legal/README.md)。
（`docs/legal/README.md` 本身不进包，它是给维护者看的。）

> `07-open-source-license.md` 是脚本从根目录 `LICENSE` 生成的，不要手改。

---

## 相关链接

- 项目主页：[startride.top](https://startride.top)
- 问题反馈：[Issues](https://github.com/CangLann-xyq/StartRide/issues)
- 联系邮箱：cloudfur2026@qq.com

---

## 免责声明

StartRide 是非官方工具，与 BeamNG GmbH 无隶属或合作关系。BeamNG.drive 及相关商标
归其各自所有者所有。

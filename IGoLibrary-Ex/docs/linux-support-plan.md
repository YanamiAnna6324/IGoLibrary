# IGoLibrary-Ex Linux 适配修改说明

## 1. 目标

在不影响现有 Windows 和 macOS 版本的前提下，为 `IGoLibrary-Ex` 增加 Linux 桌面支持。

本次交付一个可直接运行的 Linux 版本，支持登录、抢座、远程签到、SQLite 数据、WebDAV、消息通知、手机控制以及 Cloudflare 临时隧道，并补齐开机启动、防休眠、凭据持久化、安装包和中文字体回退。

旧版 `IGoLibrary-Winform` 依赖 WinForms、WPF 和 Windows 专用控件，不纳入 Linux 迁移范围。Linux 版本仅基于 Avalonia 项目 `IGoLibrary-Ex` 开发。

## 2. 支持范围

首批支持以下运行环境：

| 项目 | 首批范围 |
| --- | --- |
| CPU 架构 | `linux-x64`、`linux-arm64` |
| 桌面环境 | Ubuntu GNOME、KDE Plasma |
| 显示协议 | X11、Wayland |
| 发布形式 | 自包含的 `tar.gz` 便携包 |
| .NET 依赖 | 自包含发布，用户无需单独安装 .NET |
| 自动更新 | 仅检查新版本并打开 Release 页面 |

AppImage、Flatpak、发行版软件仓库和 Linux 原地自动更新不属于首批范围。

## 3. 需要修改的部分

### 3.1 cloudflared Linux 运行时

当前资产清单只包含 Windows 和 macOS。Linux 启动到 Cloudflare 隧道相关功能时，会因为无法解析运行时资产而报不支持。

修改内容：

- 在 `build/cloudflared-assets.json` 中增加 `linux-x64` 和 `linux-arm64`。
- 在 `CloudflaredAssetCatalog.ResolveCurrentRid()` 中识别 Linux 的 x64 和 ARM64 架构。
- Linux 使用 Cloudflare 官方发布的无压缩二进制，资产类型为 `binary`，可执行文件名为 `cloudflared`。
- 安装完成后设置 Unix 可执行权限，并继续执行大小和 SHA256 校验。
- 更新资产清单测试和安装测试。

固定版本 `2026.7.0` 的资产信息：

| RID | 文件 | 大小 | SHA256 |
| --- | --- | ---: | --- |
| `linux-x64` | `cloudflared-linux-amd64` | 39,252,488 | `434a04eb237e07d3d4146fc44acdbb411260a94fcb01764f454abe38a09503f3` |
| `linux-arm64` | `cloudflared-linux-arm64` | 36,982,876 | `a4c14d1dfb4ea1092da4b64ede05fab7092ba8a424c7df1e7747f5232a4127ff` |

涉及文件：

- `build/cloudflared-assets.json`
- `src/IGoLibrary.Ex.Desktop/Services/CloudflaredAssetCatalog.cs`
- `src/IGoLibrary.Ex.Desktop/Services/CloudflaredExtractor.cs`
- `tests/IGoLibrary.Ex.Tests/CloudflaredRuntimeAssetTests.cs`
- `tests/IGoLibrary.Ex.Tests/CloudflaredInstallServiceTests.cs`

### 3.2 Linux 凭据持久化

当前 Linux 会回退到内存存储。应用关闭后，登录 Cookie、远程签到凭据、WebDAV 密码和备份加密密码都会丢失。

修改内容：

- 新增基于 `libsecret` 的 Linux Secret Service 后端，将敏感数据保存到桌面密钥环。
- 支持 GNOME Keyring 和 KDE Wallet 提供的 Freedesktop Secret Service。
- 会话凭据与备份密码使用同一套 Linux Secret Service 客户端，但使用不同的条目标识。
- 未启动 Secret Service 时，读取操作按“没有已保存凭据”处理，应用仍可运行；保存操作返回明确错误，不静默降级为明文文件。
- 保留现有 Windows Credential Manager 和 macOS Keychain 实现。
- 在保存或删除成功后继续通知 `IPersistentDataChangeTracker`。

建议条目标识：

| 数据 | Service | Account |
| --- | --- | --- |
| 图书馆会话 | `IGoLibrary-Ex` | `session` |
| 远程签到会话 | `IGoLibrary-Ex` | `remote-check-in` |
| WebDAV 密码 | `IGoLibrary-Ex` | `webdav` |
| 备份密码 | `IGoLibrary-Ex` | `backup-encryption` |
| 上一个备份密码 | `IGoLibrary-Ex` | `backup-encryption-previous` |

涉及文件：

- `src/IGoLibrary.Ex.Infrastructure/Security/PlatformCredentialStore.cs`
- `src/IGoLibrary.Ex.Infrastructure/Security/PlatformBackupSecretStore.cs`
- 新增 `src/IGoLibrary.Ex.Infrastructure/Security/LinuxSecretServiceClient.cs`
- 新增 `src/IGoLibrary.Ex.Infrastructure/Security/LinuxSecretServiceCredentialStore.cs`
- 新增或扩展凭据存储测试

### 3.3 Linux 开机启动

Linux 使用 XDG Autostart 规范，在当前用户的配置目录写入 `.desktop` 文件。

修改内容：

- `StartupEntryService.IsSupported` 对 Linux 返回 `true`。
- 优先使用 `$XDG_CONFIG_HOME/autostart`，未设置时使用 `~/.config/autostart`。
- 启用时生成 `igolibrary-ex.desktop`，写入当前可执行文件的绝对路径。
- 正确转义 `Exec` 中的空格、引号和反斜杠。
- 禁用时只删除本应用创建的文件。
- 查询状态时校验文件存在且执行路径与当前程序一致，避免把旧路径误判为已启用。

涉及文件：

- `src/IGoLibrary.Ex.Desktop/Services/StartupEntryService.cs`
- 新增 `tests/IGoLibrary.Ex.Tests/StartupEntryServiceTests.cs`

生成文件示例：

```ini
[Desktop Entry]
Type=Application
Name=IGoLibrary-Ex
Exec="/opt/IGoLibrary-Ex/IGoLibrary.Ex.Desktop"
Terminal=false
X-GNOME-Autostart-enabled=true
```

### 3.4 Linux 防休眠

抢座、预约和监控任务运行时，需要阻止系统进入空闲休眠。

修改内容：

- 新增 `LinuxSystemIdleSleepInhibitor`。
- 首批使用 `systemd-inhibit` 申请 `sleep` 阻止锁，并在任务结束时释放。
- 持有期间监控子进程；子进程异常退出时记录错误并允许重新申请。
- 释放超时后终止对应子进程，不能影响其他应用的 inhibitor。
- 找不到 `systemd-inhibit` 时将功能标记为不支持，并沿用现有警告逻辑。

涉及文件：

- `src/IGoLibrary.Ex.Desktop/Platform/Power/SystemIdleSleepInhibitor.cs`
- 新增 `src/IGoLibrary.Ex.Desktop/Platform/Power/LinuxSystemIdleSleepInhibitor.cs`
- `tests/IGoLibrary.Ex.Tests/SystemIdleSleepInhibitorTests.cs`

### 3.5 更新功能的平台隔离

当前独立启动器、更新器、UAC 提权和文件替换事务均为 Windows 方案。Linux 版本不能调用这些组件。

修改内容：

- Linux 保留版本检查和 Release 页面入口。
- Linux 发现新版本时不显示“下载并安装”操作，改为打开 Release 页面手动下载。
- Windows 自动更新服务只在 Windows 上注册或执行。
- Linux 启动时不运行 Windows 更新事务恢复和清理逻辑。
- Linux 发布包不包含 `IGoLibrary.Ex.Launcher.exe` 和 `IGoLibrary.Ex.Updater.exe`。

涉及文件：

- `src/IGoLibrary.Ex.Desktop/HostBuilderFactory.cs`
- `src/IGoLibrary.Ex.Desktop/ViewModels/Pages/UpdateLinksViewModel.cs`
- `src/IGoLibrary.Ex.Desktop/Program.cs`
- 更新界面及相关测试

### 3.6 Linux 发布脚本

新增 Linux 发布脚本，只发布 Avalonia Desktop 主项目，不发布 Windows 启动器和更新器。

修改内容：

- 分别执行 `linux-x64` 和 `linux-arm64` 自包含发布。
- 输出目录包含主程序、依赖、配置模板和第三方许可证。
- 为主程序及 `cloudflared` 设置可执行权限。
- 生成 `tar.gz` 和 SHA256 校验文件。
- 增加一份 Linux 运行说明，列出桌面密钥环和 systemd 的要求。

新增文件：

- `build/publish-linux.sh`
- `build/verify-linux-package.sh`
- `docs/linux-installation.md`

发布命令基线：

```bash
dotnet publish src/IGoLibrary.Ex.Desktop/IGoLibrary.Ex.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true
```

### 3.7 托盘、窗口与桌面通知

Avalonia 托盘和自定义 Toast 可以继续复用，但需要进行真实桌面测试。

修改内容：

- 验证 GNOME、KDE 下托盘图标、菜单和退出行为。
- 验证 X11、Wayland 下主窗口恢复、置顶和 Toast 定位。
- Wayland 不允许应用任意定位窗口时，让 Toast 使用桌面环境允许的位置，不依赖绝对屏幕坐标。
- 首批继续使用应用内 Toast；系统原生通知作为后续增强项。

可能涉及文件：

- `src/IGoLibrary.Ex.Desktop/App.axaml`
- `src/IGoLibrary.Ex.Desktop/ToastWindow.axaml.cs`
- `src/IGoLibrary.Ex.Desktop/Services/ToastNotificationService.cs`

### 3.8 Linux 中文字体回退

Avalonia 原先全局使用 Inter。Inter 不包含中文字形，在部分 Linux 和 WSLg 环境中会把中文显示为方框；源文件和运行时字符串本身仍是 UTF-8。

修改内容：

- 为所有应用窗口增加 Inter、Noto Sans SC、Noto Sans CJK SC、Microsoft YaHei UI、PingFang SC 和通用 sans-serif 的字体回退链。
- 安装说明将 `fonts-noto-cjk` 列为推荐依赖。
- 更新提示窗口的系统标题只使用产品名和版本号，避免 WSLg 的外部窗口装饰无法显示中文标题；窗口内容区仍保留完整中文信息。
- 在 WSLg/X11 中对发布后的 `linux-x64` 包进行实际截图检查，不只验证进程能进入事件循环。

涉及文件：

- `src/IGoLibrary.Ex.Desktop/App.axaml`
- `src/IGoLibrary.Ex.Desktop/UpdateReleaseWindow.cs`
- `tests/IGoLibrary.Ex.Tests/UpdateReleaseWindowTests.cs`
- `docs/linux-installation.md`

## 4. 实施顺序

### 阶段一：Linux 最小可运行版本

1. 增加 cloudflared 的 `linux-x64` 和 `linux-arm64` 资产。
2. 增加 Linux 发布脚本，仅构建 Desktop 项目。
3. 隔离 Windows 自动更新入口。
4. 在 Linux x64 环境完成启动、登录、数据库和核心抢座流程验证。

完成标准：Linux 包可以解压启动，核心业务可运行，关闭自动更新不会造成启动或运行异常。

### 阶段二：桌面集成

1. 接入 Secret Service，持久保存所有敏感凭据。
2. 实现 XDG Autostart。
3. 实现 `systemd-inhibit` 防休眠。
4. 验证 GNOME/KDE 和 X11/Wayland。

完成标准：重启应用后凭据仍可读取，开机启动可启停，任务运行期间系统不会自动休眠。

### 阶段三：分发完善

1. 增加安装说明和依赖检测提示。
2. 增加发布包结构与 SHA256 自动验证。
3. 根据实际使用反馈决定是否制作 AppImage 或 Flatpak。
4. 单独设计 Linux 自动更新方案。

## 5. 验收清单

### 构建与启动

- `linux-x64` 自包含发布成功。
- `linux-arm64` 自包含发布成功，或至少完成交叉发布并在 ARM64 机器实测。
- 全新用户目录下首次启动成功。
- 路径包含空格和中文时可以启动。
- 同一用户不能同时运行两个实例。

### 核心功能

- 手动 Cookie 和网页登录流程可用。
- 应用重启后会话凭据仍存在。
- SQLite 设置、任务和日志可以正常读写。
- 抢座、预约、远程签到和取消操作正常。
- WebDAV、SMTP、Telegram、Bark、ServerChan 和 WxPusher 行为与其他平台一致。
- 手机控制的局域网模式和 Cloudflare 模式均可启动和停止。

### 桌面集成

- 开机启动开关与实际 `.desktop` 文件状态一致。
- 开机启动路径更新后能够自动修正旧配置。
- 任务运行时防休眠生效，任务结束后锁被释放。
- 托盘菜单、显示主窗口和退出操作正常。
- GNOME/KDE、X11/Wayland 下窗口和 Toast 不遮挡主要控件。

### 安全与回归

- 敏感凭据不写入日志、SQLite 或普通文本文件。
- cloudflared 下载内容必须通过大小和 SHA256 校验。
- Linux 版本不会启动 Windows 更新器。
- Windows 和 macOS 原有测试继续通过。
- 发布包包含 cloudflared 第三方许可证和声明。

## 6. 已知限制

- Wayland 对窗口定位和全局置顶有系统级限制，不保证与 Windows 表现完全一致。
- 部分 GNOME 环境需要托盘扩展才能显示传统托盘图标。
- Secret Service 依赖当前桌面会话提供可用的密钥环；纯终端或无桌面会话需要给出明确提示。
- `systemd-inhibit` 依赖 systemd。非 systemd 发行版首批不会提供防休眠支持。
- Linux 自动更新、AppImage 和 Flatpak 将在核心版本稳定后单独设计。

## 7. 分支与提交建议

开发分支为 `feature/linux-support`。建议按以下粒度提交，方便逐项审查和回退：

1. `feat(linux): add cloudflared runtime assets`
2. `feat(linux): persist credentials with secret service`
3. `feat(linux): add xdg autostart support`
4. `feat(linux): inhibit idle sleep during tasks`
5. `feat(linux): isolate windows-only update flow`
6. `build(linux): add self-contained release packaging`
7. `docs(linux): add installation and compatibility notes`

## 8. 当前实现状态

截至 2026-09-17，`feature/linux-support` 分支已完成：

- cloudflared `linux-x64`、`linux-arm64` 资产解析、校验和 Unix 执行权限。
- Linux Secret Service 凭据存储，以及无 Secret Service 环境下的安全降级。
- XDG Autostart 开机启动。
- 基于 `systemd-inhibit` 的任务防休眠。
- Linux 中文字体回退链。
- `linux-x64`、`linux-arm64` 自包含发布和包结构校验脚本。
- Linux 安装、依赖和更新说明。

验证结果：

- Debug 构建通过，零警告、零错误。
- `IGoLibrary.Ex.Tests` 共 1539 项测试通过。
- `linux-x64` 和 `linux-arm64` 自包含包均已生成并通过校验。
- `linux-x64` 已在 WSLg/X11 环境完成启动冒烟测试；无 Secret Service 时仍能进入主界面并完成初始化。
- `linux-x64` 已完成发布包界面截图检查，首页、侧栏、日期和状态说明中的中文均能正常显示。

仍需在实际发行前完成 GNOME/KDE、原生 X11/Wayland、ARM64 真机和托盘行为验证。

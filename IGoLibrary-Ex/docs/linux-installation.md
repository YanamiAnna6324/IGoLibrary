# IGoLibrary-Ex Linux 安装说明

## 支持环境

- x86_64：使用 `linux-x64` 包。
- ARM64：使用 `linux-arm64` 包。
- 支持 X11 和 Wayland，建议使用 GNOME 或 KDE Plasma 桌面。

发布包包含 .NET 运行时，不需要另外安装 .NET SDK 或 Runtime。

## 从源码启动

已经安装 .NET SDK 10 和 GNU Make 时，在 `IGoLibrary-Ex` 目录运行：

```bash
make run
```

Makefile 会从 `PATH` 或 `~/.dotnet/dotnet` 查找 SDK。其他常用命令：

```bash
make run-debug
make test
make publish
make release
```

`make publish` 根据当前 CPU 架构生成一个包；`make release` 同时生成 `linux-x64` 和 `linux-arm64` 包。

## 注册到 Windows

在 WSLg 中运行本项目时，可以把 Linux 应用注册到当前 Windows 用户：

```bash
make windows-install
```

安装完成后，可以从 Windows 开始菜单打开 `IGoLibrary-Ex (WSL)`，也可以按 `Win+R` 输入：

```text
igolibrary-ex:
```

该入口通过 `wsl.exe` 启动当前 WSL 发行版中的 Linux 程序，不会把 Linux 程序转换为 Windows 原生程序。注册信息写入 `HKEY_CURRENT_USER`，无需管理员权限。请勿在注册后移动或删除对应的 Linux 程序目录；移动后重新执行安装命令即可更新路径。

从源码目录卸载：

```bash
make windows-uninstall
```

也可以在 Windows 的“设置 > 应用 > 已安装的应用”中卸载 `IGoLibrary-Ex (WSL)`。

## 系统依赖

应用使用系统的 Secret Service 安全保存 Cookie 和密码，并使用 `systemd-inhibit` 在任务运行时阻止系统休眠。

Ubuntu/Debian 可安装以下基础依赖：

```bash
sudo apt install libsecret-1-0 gnome-keyring libx11-6 libice6 libsm6 libfontconfig1 fonts-noto-cjk
```

桌面会话还需要提供 Secret Service。GNOME 通常由 GNOME Keyring 提供，KDE Plasma 可由 KWallet 提供。上述命令为 Ubuntu 安装 GNOME Keyring；KDE 用户可以使用桌面自带的 KWallet。如果系统没有可用的 Secret Service，应用仍可运行，但不会持久保存 Cookie 和密码，也不会把敏感信息降级保存到普通文本文件。

`fonts-noto-cjk` 为中文界面提供回退字形。应用优先使用 Inter 显示拉丁字符，并依次尝试 Noto Sans SC、Noto Sans CJK SC、Microsoft YaHei UI 和 PingFang SC 显示中文。

如果应用内容区仍显示中文方框，请安装上述字体并执行 `fc-cache -f`，然后重新启动应用。WSLg 的系统标题栏由 WSLg 窗口管理器绘制，不使用应用内的 Avalonia 字体回退；标题栏问题不代表配置或文本文件编码损坏。

非 systemd 发行版可以运行主要功能，但首版无法在任务执行期间阻止系统休眠。

## 运行

解压后进入程序目录：

```bash
tar -xzf IGoLibrary-Ex-vVERSION-linux-x64.tar.gz
cd IGoLibrary-Ex
./IGoLibrary.Ex.Desktop
```

如果解压位置位于 WSL 文件系统，还可以执行以下命令安装 Windows 启动入口：

```bash
./install-windows-launcher.sh
```

发布包已经包含执行权限。如果复制过程丢失了权限，可以重新设置：

```bash
chmod +x IGoLibrary.Ex.Desktop
```

## 开机启动

在应用的系统设置中启用开机启动后，程序会创建：

```text
$XDG_CONFIG_HOME/autostart/igolibrary-ex.desktop
```

如果没有设置 `XDG_CONFIG_HOME`，则使用 `~/.config/autostart/igolibrary-ex.desktop`。移动程序目录后，请重新打开开机启动开关，以写入新的执行路径。

## 更新

Linux 首版支持检查新版本。发现更新后会打开 GitHub Release 页面，需要手动下载并替换程序目录。Windows 专用启动器和自动更新器不会包含在 Linux 发布包中。

## 桌面环境说明

- Wayland 会限制应用任意定位窗口，Toast 的位置可能与 X11 略有不同。
- 部分 GNOME 版本需要 AppIndicator/KStatusNotifierItem 扩展才能显示传统托盘图标。
- 在纯终端、SSH 或没有用户 D-Bus 会话的环境中，Secret Service 和托盘功能不可用。

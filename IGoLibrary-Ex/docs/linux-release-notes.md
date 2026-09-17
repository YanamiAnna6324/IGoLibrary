# IGoLibrary-Ex Linux v1.0.3

这是首个 Linux 预发布版本，提供 `linux-x64` 和 `linux-arm64` 自包含包，无需另外安装 .NET Runtime。

## 主要改动

- 支持 Linux x64 和 ARM64 上的 Avalonia 桌面应用。
- 支持通过 Secret Service 安全保存登录 Cookie、远程签到凭据、WebDAV 密码和备份密码。
- 支持 XDG Autostart 开机启动。
- 任务运行时通过 `systemd-inhibit` 阻止系统自动休眠。
- 支持下载并校验对应架构的 cloudflared。
- 增加 Linux 中文字体回退和 Noto CJK 字体安装说明。
- WSLg 用户可注册 Windows 开始菜单快捷方式和 `igolibrary-ex:` 启动协议。

## 安装

Ubuntu/Debian 建议先安装：

```bash
sudo apt update
sudo apt install libsecret-1-0 gnome-keyring libx11-6 libice6 libsm6 libfontconfig1 fonts-noto-cjk
```

下载对应架构的 `tar.gz`，校验同名 `.sha256` 文件后解压运行：

```bash
tar -xzf IGoLibrary-Ex-v1.0.3-linux-x64.tar.gz
cd IGoLibrary-Ex
./IGoLibrary.Ex.Desktop
```

在 WSLg 中还可以运行 `./install-windows-launcher.sh`，随后从 Windows 开始菜单启动应用。

WSL/WSLg 用户需要安装并启动 Secret Service 提供者后，才能持久保存 Cookie。未提供 Secret Service 时，可以取消“记住会话”后临时登录。

## 验证范围

- 主测试套件 1539 项全部通过。
- x64 和 ARM64 发布包均通过路径安全、文件权限、许可证和 ELF 架构校验。
- x64 包已在 WSLg/X11 环境完成启动和中文界面检查。

ARM64 真机、原生 GNOME/KDE 以及不同 Wayland 组合仍需进一步验证，因此本版本标记为预发布。

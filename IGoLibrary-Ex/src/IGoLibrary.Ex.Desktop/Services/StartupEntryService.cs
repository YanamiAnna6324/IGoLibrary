using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IGoLibrary.Ex.Desktop.Services;

public sealed class StartupEntryService : IStartupEntryService
{
    private const string AppName = "IGoLibrary-Ex";
    private const string WindowsRunKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";
    private const string MacLaunchAgentPlist = "com.IGoLibrary-Ex.plist";
    private const string LinuxAutostartFileName = "igolibrary-ex.desktop";
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(10);
    private readonly ILogger<StartupEntryService> _logger;

    public StartupEntryService(ILogger<StartupEntryService>? logger = null)
    {
        _logger = logger ?? NullLogger<StartupEntryService>.Instance;
    }

    public bool IsSupported =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows())
        {
            var enabled = IsWindowsStartupEntryEnabled();
            _logger.LogInformation("已查询开机启动状态。平台=Windows，已启用={Enabled}。", enabled);
            return Task.FromResult(enabled);
        }

        if (OperatingSystem.IsMacOS())
        {
            var enabled = IsMacLaunchAgentEnabled();
            _logger.LogInformation("已查询开机启动状态。平台=macOS，已启用={Enabled}。", enabled);
            return Task.FromResult(enabled);
        }

        if (OperatingSystem.IsLinux())
        {
            var enabled = IsLinuxAutostartEntryEnabled();
            _logger.LogInformation("已查询开机启动状态。平台=Linux，已启用={Enabled}。", enabled);
            return Task.FromResult(enabled);
        }

        return Task.FromResult(false);
    }

    public Task EnableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows())
        {
            EnableWindowsStartupEntry();
            _logger.LogInformation("开机启动已启用。平台=Windows。");
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsMacOS())
        {
            EnableMacLaunchAgent();
            _logger.LogInformation("开机启动已启用。平台=macOS。");
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsLinux())
        {
            EnableLinuxAutostartEntry();
            _logger.LogInformation("开机启动已启用。平台=Linux。");
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    public Task DisableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows())
        {
            DisableWindowsStartupEntry();
            _logger.LogInformation("开机启动已禁用。平台=Windows。");
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsMacOS())
        {
            DisableMacLaunchAgent();
            _logger.LogInformation("开机启动已禁用。平台=macOS。");
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsLinux())
        {
            DisableLinuxAutostartEntry();
            _logger.LogInformation("开机启动已禁用。平台=Linux。");
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static string? GetExecutablePath()
    {
        return Environment.ProcessPath;
    }

    // ── Windows (registry via reg.exe) ──────────────────────────────────

    private bool IsWindowsStartupEntryEnabled()
    {
        return TryQueryWindowsStartupEntry(out var exists, out _) && exists;
    }

    private void EnableWindowsStartupEntry()
    {
        var exePath = GetExecutablePath()
            ?? throw new InvalidOperationException("无法确定当前可执行文件路径");

        var quotedPath = "\"" + exePath + "\"";
        var (exitCode, _, stderr) = RunRegProcess(
            $"add \"{WindowsRunKey}\" /v {AppName} /t REG_SZ /d {quotedPath} /f",
            redirectError: true);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"写入开机启动注册表失败（退出码 {exitCode}）：{FormatProcessError(stderr)}");
        }
    }

    private void DisableWindowsStartupEntry()
    {
        var (exitCode, _, stderr) = RunRegProcess(
            $"delete \"{WindowsRunKey}\" /v {AppName} /f",
            redirectError: true);

        if (exitCode == 0)
        {
            return;
        }

        if (TryQueryWindowsStartupEntry(out var exists, out var queryError) && !exists)
        {
            return;
        }

        var detail = string.IsNullOrWhiteSpace(stderr)
            ? queryError
            : stderr;
        throw new InvalidOperationException(
            $"移除开机启动注册表失败（退出码 {exitCode}）：{FormatProcessError(detail)}");
    }

    private bool TryQueryWindowsStartupEntry(out bool exists, out string error)
    {
        try
        {
            var (exitCode, _, stderr) = RunRegProcess(
                $"query \"{WindowsRunKey}\" /v {AppName}",
                redirectError: true);
            exists = exitCode == 0;
            error = stderr;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询 Windows 开机启动注册表项失败。");
            exists = false;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Runs reg.exe with the given arguments, reading stdout/stderr asynchronously to avoid deadlocks.
    /// Returns (exitCode, stdout, stderr). Throws if the process cannot be started or times out.
    /// </summary>
    private static (int ExitCode, string StdOut, string StdError) RunRegProcess(string arguments, bool redirectError)
    {
        var info = new ProcessStartInfo("reg", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = redirectError
        };

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("无法启动 reg.exe 进程");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = redirectError
            ? process.StandardError.ReadToEndAsync()
            : Task.FromResult(string.Empty);

        if (!process.WaitForExit(ProcessTimeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"reg.exe 在 {ProcessTimeout.TotalSeconds:0}s 内未退出");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        return (process.ExitCode, stdout, stderr);
    }

    private static string FormatProcessError(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "未返回错误详情"
            : value.Trim();
    }

    // ── macOS (LaunchAgent plist) ───────────────────────────────────────

    private static string GetMacLaunchAgentPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", MacLaunchAgentPlist);
    }

    private static bool IsMacLaunchAgentEnabled()
    {
        return File.Exists(GetMacLaunchAgentPath());
    }

    private static void EnableMacLaunchAgent()
    {
        var exePath = GetExecutablePath()
            ?? throw new InvalidOperationException("无法确定当前可执行文件路径");

        var plistPath = GetMacLaunchAgentPath();
        var directory = Path.GetDirectoryName(plistPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plist = BuildMacLaunchAgentPlist(exePath);
        File.WriteAllText(plistPath, plist);
    }

    private static void DisableMacLaunchAgent()
    {
        var plistPath = GetMacLaunchAgentPath();
        if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }
    }

    private static string BuildMacLaunchAgentPlist(string exePath)
    {
        // Escape XML special characters to prevent injection via executable path.
        var escapedExePath = System.Security.SecurityElement.Escape(exePath)
            ?? exePath;

        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        builder.AppendLine("<plist version=\"1.0\">");
        builder.AppendLine("<dict>");
        builder.AppendLine($"  <key>Label</key>");
        builder.AppendLine($"  <string>{AppName}</string>");
        builder.AppendLine($"  <key>ProgramArguments</key>");
        builder.AppendLine($"  <array>");
        builder.AppendLine($"    <string>{escapedExePath}</string>");
        builder.AppendLine($"  </array>");
        builder.AppendLine($"  <key>RunAtLoad</key>");
        builder.AppendLine($"  <true/>");
        builder.AppendLine("</dict>");
        builder.AppendLine("</plist>");
        return builder.ToString();
    }

    // XDG Autostart (.desktop file)

    private static string GetLinuxAutostartPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        }

        return ResolveLinuxAutostartPath(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
            userProfile);
    }

    internal static string ResolveLinuxAutostartPath(string? xdgConfigHome, string userProfile)
    {
        var configRoot = !string.IsNullOrWhiteSpace(xdgConfigHome) && Path.IsPathRooted(xdgConfigHome)
            ? xdgConfigHome
            : !string.IsNullOrWhiteSpace(userProfile)
                ? Path.Combine(userProfile, ".config")
                : throw new InvalidOperationException("无法确定当前用户的 Linux 配置目录");
        return Path.Combine(configRoot, "autostart", LinuxAutostartFileName);
    }

    private static bool IsLinuxAutostartEntryEnabled()
    {
        var executablePath = GetExecutablePath();
        var autostartPath = GetLinuxAutostartPath();
        return executablePath is not null &&
               File.Exists(autostartPath) &&
               IsLinuxAutostartEntryForExecutable(
                   File.ReadAllText(autostartPath),
                   executablePath);
    }

    private static void EnableLinuxAutostartEntry()
    {
        var executablePath = GetExecutablePath()
            ?? throw new InvalidOperationException("无法确定当前可执行文件路径");
        var autostartPath = GetLinuxAutostartPath();
        Directory.CreateDirectory(Path.GetDirectoryName(autostartPath)!);
        File.WriteAllText(
            autostartPath,
            BuildLinuxAutostartEntry(executablePath),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void DisableLinuxAutostartEntry()
    {
        var autostartPath = GetLinuxAutostartPath();
        if (File.Exists(autostartPath))
        {
            File.Delete(autostartPath);
        }
    }

    internal static string BuildLinuxAutostartEntry(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var normalizedPath = Path.GetFullPath(executablePath);
        var exec = EscapeLinuxDesktopExecArgument(normalizedPath);
        return string.Join(
            '\n',
            "[Desktop Entry]",
            "Type=Application",
            "Version=1.0",
            $"Name={AppName}",
            $"Exec={exec}",
            "Terminal=false",
            "StartupNotify=false",
            "X-GNOME-Autostart-enabled=true",
            string.Empty);
    }

    internal static bool IsLinuxAutostartEntryForExecutable(
        string desktopEntry,
        string executablePath)
    {
        ArgumentNullException.ThrowIfNull(desktopEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var directives = desktopEntry
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var expectedExec = "Exec=" + EscapeLinuxDesktopExecArgument(Path.GetFullPath(executablePath));
        return directives.Contains("[Desktop Entry]") &&
               directives.Contains("Type=Application") &&
               directives.Contains(expectedExec) &&
               !directives.Contains("Hidden=true");
    }

    internal static string EscapeLinuxDesktopExecArgument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return '"' + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("%", "%%", StringComparison.Ordinal) + '"';
    }
}

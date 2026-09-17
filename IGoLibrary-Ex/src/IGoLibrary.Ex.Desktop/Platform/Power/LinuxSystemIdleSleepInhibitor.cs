using System.Diagnostics;

namespace IGoLibrary.Ex.Desktop.Platform.Power;

internal sealed class LinuxSystemIdleSleepInhibitor(
    ILinuxSleepInhibitProcessFactory? processFactory = null) : ISystemIdleSleepInhibitor
{
    private readonly ILinuxSleepInhibitProcessFactory _processFactory =
        processFactory ?? new LinuxSleepInhibitProcessFactory();
    private readonly object _gate = new();
    private ILinuxSleepInhibitProcess? _process;

    public event EventHandler<SystemSleepInhibitorException>? CleanupFailed;

    public string PlatformName => "Linux";

    public bool IsSupported => _processFactory.IsSupported;

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _process is { HasExited: false };
            }
        }
    }

    public void Activate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException("当前 Linux 环境未安装 systemd-inhibit");
            }

            if (_process is { HasExited: false })
            {
                return;
            }

            _process?.Dispose();
            _process = null;
            try
            {
                _process = _processFactory.Start(reason);
            }
            catch (SystemSleepInhibitorException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("systemd-inhibit start", -1, exception.Message, exception);
            }
        }
    }

    public void Deactivate()
    {
        lock (_gate)
        {
            if (_process is null)
            {
                return;
            }

            if (_process.HasExited)
            {
                _process.Dispose();
                _process = null;
                return;
            }

            try
            {
                _process.Stop();
                _process.Dispose();
                _process = null;
            }
            catch (SystemSleepInhibitorException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("systemd-inhibit stop", -1, exception.Message, exception);
            }
        }
    }

    public void Dispose()
    {
        ILinuxSleepInhibitProcess? process;
        lock (_gate)
        {
            process = _process;
            _process = null;
        }

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Stop();
            }
        }
        catch (Exception exception)
        {
            CleanupFailed?.Invoke(
                this,
                exception as SystemSleepInhibitorException ??
                CreateException("systemd-inhibit cleanup", -1, exception.Message, exception));
        }
        finally
        {
            process.Dispose();
        }
    }

    private static SystemSleepInhibitorException CreateException(
        string operation,
        int errorCode,
        string detail,
        Exception? innerException = null)
        => new(
            "Linux",
            operation,
            errorCode,
            $"{operation} failed: {detail}",
            innerException);
}

internal interface ILinuxSleepInhibitProcessFactory
{
    bool IsSupported { get; }

    ILinuxSleepInhibitProcess Start(string reason);
}

internal interface ILinuxSleepInhibitProcess : IDisposable
{
    bool HasExited { get; }

    void Stop();
}

internal sealed class LinuxSleepInhibitProcessFactory : ILinuxSleepInhibitProcessFactory
{
    private const string CommandName = "systemd-inhibit";
    private readonly string? _commandPath;

    public LinuxSleepInhibitProcessFactory()
        : this(FindExecutableOnPath(CommandName, Environment.GetEnvironmentVariable("PATH")))
    {
    }

    internal LinuxSleepInhibitProcessFactory(string? commandPath)
    {
        _commandPath = commandPath;
    }

    public bool IsSupported => OperatingSystem.IsLinux() && _commandPath is not null;

    public ILinuxSleepInhibitProcess Start(string reason)
    {
        if (!IsSupported || _commandPath is null)
        {
            throw new PlatformNotSupportedException("当前 Linux 环境未安装 systemd-inhibit");
        }

        var startInfo = new ProcessStartInfo(_commandPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--what=sleep");
        startInfo.ArgumentList.Add("--mode=block");
        startInfo.ArgumentList.Add("--who=IGoLibrary-Ex");
        startInfo.ArgumentList.Add($"--why={reason}");
        startInfo.ArgumentList.Add("cat");

        Process? process = null;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动 systemd-inhibit");
            if (process.WaitForExit(150))
            {
                var error = process.StandardError.ReadToEnd().Trim();
                var exitCode = process.ExitCode;
                process.Dispose();
                throw new SystemSleepInhibitorException(
                    "Linux",
                    "systemd-inhibit start",
                    exitCode,
                    string.IsNullOrWhiteSpace(error)
                        ? $"systemd-inhibit 启动后立即退出（退出码 {exitCode}）"
                        : $"systemd-inhibit 启动失败（退出码 {exitCode}）：{error}");
            }

            return new LinuxSleepInhibitProcess(process);
        }
        catch
        {
            process?.Dispose();
            throw;
        }
    }

    internal static string? FindExecutableOnPath(string commandName, string? pathValue)
    {
        if (string.IsNullOrWhiteSpace(commandName) || string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, commandName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ignore malformed PATH entries and continue searching.
            }
        }

        return null;
    }
}

internal sealed class LinuxSleepInhibitProcess(Process process) : ILinuxSleepInhibitProcess
{
    private static readonly TimeSpan GracefulStopTimeout = TimeSpan.FromSeconds(2);

    public bool HasExited => process.HasExited;

    public void Stop()
    {
        if (process.HasExited)
        {
            return;
        }

        process.StandardInput.Close();
        if (process.WaitForExit(GracefulStopTimeout))
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        if (!process.WaitForExit(GracefulStopTimeout))
        {
            throw new TimeoutException("systemd-inhibit 未在超时时间内退出");
        }
    }

    public void Dispose() => process.Dispose();
}

using System.Runtime.InteropServices;

namespace IGoLibrary.Ex.Infrastructure.Security;

internal interface ILinuxSecretServiceClient
{
    bool IsAvailable { get; }

    Task<string?> LookupAsync(string account, CancellationToken cancellationToken = default);

    Task StoreAsync(
        string account,
        string label,
        string secret,
        CancellationToken cancellationToken = default);

    Task ClearAsync(string account, CancellationToken cancellationToken = default);
}

internal sealed class LinuxSecretServiceClient : ILinuxSecretServiceClient
{
    private const string LibSecret = "libsecret-1.so.0";
    private const string LibGlib = "libglib-2.0.so.0";
    private const string SchemaName = "com.ejianzq.IGoLibrary.Ex";
    private const string ServiceAttribute = "service";
    private const string ServiceValue = "IGoLibrary-Ex";
    private const string AccountAttribute = "account";
    private const string DefaultCollection = "default";
    private static readonly Lazy<nint> Schema = new(CreateSchema);
    private int _isAvailable;
    private string? _unavailableReason;

    public LinuxSecretServiceClient()
    {
        _isAvailable = OperatingSystem.IsLinux() && IsLibraryAvailable(LibSecret) ? 1 : 0;
        if (_isAvailable == 0)
        {
            _unavailableReason = "当前 Linux 环境缺少 libsecret；请安装 libsecret 后重新启动应用";
        }
    }

    public bool IsAvailable => Volatile.Read(ref _isAvailable) == 1;

    public Task<string?> LookupAsync(
        string account,
        CancellationToken cancellationToken = default)
    {
        ValidateAccount(account);
        if (!IsAvailable)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var password = SecretPasswordLookupSync(
                    Schema.Value,
                    nint.Zero,
                    out var error,
                    ServiceAttribute,
                    ServiceValue,
                    AccountAttribute,
                    account,
                    nint.Zero);
                ThrowIfError(error, "读取 Linux Secret Service 凭据失败");
                if (password == nint.Zero)
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUTF8(password);
                }
                finally
                {
                    SecretPasswordFree(password);
                }
            }
            catch (LinuxSecretServiceUnavailableException exception)
            {
                MarkUnavailable(exception.Message);
                return null;
            }
        }, cancellationToken);
    }

    public Task StoreAsync(
        string account,
        string label,
        string secret,
        CancellationToken cancellationToken = default)
    {
        ValidateAccount(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(secret);
        EnsureAvailable();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stored = SecretPasswordStoreSync(
                Schema.Value,
                DefaultCollection,
                label,
                secret,
                nint.Zero,
                out var error,
                ServiceAttribute,
                ServiceValue,
                AccountAttribute,
                account,
                nint.Zero);
            try
            {
                ThrowIfError(error, "写入 Linux Secret Service 凭据失败");
            }
            catch (LinuxSecretServiceUnavailableException exception)
            {
                MarkUnavailable(exception.Message);
                throw;
            }

            if (stored == 0)
            {
                throw new InvalidOperationException("写入 Linux Secret Service 凭据失败：未返回错误详情");
            }
        }, cancellationToken);
    }

    public Task ClearAsync(
        string account,
        CancellationToken cancellationToken = default)
    {
        ValidateAccount(account);
        if (!IsAvailable)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = SecretPasswordClearSync(
                Schema.Value,
                nint.Zero,
                out var error,
                ServiceAttribute,
                ServiceValue,
                AccountAttribute,
                account,
                nint.Zero);
            try
            {
                ThrowIfError(error, "删除 Linux Secret Service 凭据失败");
            }
            catch (LinuxSecretServiceUnavailableException exception)
            {
                MarkUnavailable(exception.Message);
            }
        }, cancellationToken);
    }

    private static nint CreateSchema()
    {
        var schema = SecretSchemaNew(
            SchemaName,
            flags: 0,
            ServiceAttribute,
            attribute1Type: 0,
            AccountAttribute,
            attribute2Type: 0,
            nint.Zero);
        return schema != nint.Zero
            ? schema
            : throw new InvalidOperationException("创建 Linux Secret Service schema 失败");
    }

    private static bool IsLibraryAvailable(string libraryName)
    {
        if (!NativeLibrary.TryLoad(libraryName, out var handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new PlatformNotSupportedException(
                _unavailableReason ?? "当前 Linux 环境没有可用的 Secret Service");
        }
    }

    private void MarkUnavailable(string reason)
    {
        _unavailableReason = reason;
        Volatile.Write(ref _isAvailable, 0);
    }

    private static void ValidateAccount(string account)
        => ArgumentException.ThrowIfNullOrWhiteSpace(account);

    private static void ThrowIfError(nint error, string message)
    {
        if (error == nint.Zero)
        {
            return;
        }

        try
        {
            var nativeError = Marshal.PtrToStructure<GError>(error);
            var detail = Marshal.PtrToStringUTF8(nativeError.Message);
            var fullMessage = string.IsNullOrWhiteSpace(detail)
                ? $"{message}（错误代码 {nativeError.Code}）"
                : $"{message}：{detail}";
            if (IsServiceUnavailableError(detail))
            {
                throw new LinuxSecretServiceUnavailableException(fullMessage);
            }

            throw new InvalidOperationException(fullMessage);
        }
        finally
        {
            GErrorFree(error);
        }
    }

    private static bool IsServiceUnavailableError(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return false;
        }

        return detail.Contains("org.freedesktop.secrets", StringComparison.OrdinalIgnoreCase) &&
               (detail.Contains("not provided", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("has no owner", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("NameHasNoOwner", StringComparison.OrdinalIgnoreCase)) ||
               detail.Contains("Cannot autolaunch D-Bus", StringComparison.OrdinalIgnoreCase) ||
               detail.Contains("Could not connect", StringComparison.OrdinalIgnoreCase);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GError
    {
        public uint Domain;
        public int Code;
        public nint Message;
    }

    [DllImport(LibSecret, EntryPoint = "secret_schema_new", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SecretSchemaNew(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int flags,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute1,
        int attribute1Type,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute2,
        int attribute2Type,
        nint terminator);

    [DllImport(LibSecret, EntryPoint = "secret_password_lookup_sync", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SecretPasswordLookupSync(
        nint schema,
        nint cancellable,
        out nint error,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute2,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value2,
        nint terminator);

    [DllImport(LibSecret, EntryPoint = "secret_password_store_sync", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SecretPasswordStoreSync(
        nint schema,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
        nint cancellable,
        out nint error,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute2,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value2,
        nint terminator);

    [DllImport(LibSecret, EntryPoint = "secret_password_clear_sync", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SecretPasswordClearSync(
        nint schema,
        nint cancellable,
        out nint error,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute2,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value2,
        nint terminator);

    [DllImport(LibSecret, EntryPoint = "secret_password_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SecretPasswordFree(nint password);

    [DllImport(LibGlib, EntryPoint = "g_error_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GErrorFree(nint error);
}

internal sealed class LinuxSecretServiceUnavailableException(string message) :
    InvalidOperationException(message);

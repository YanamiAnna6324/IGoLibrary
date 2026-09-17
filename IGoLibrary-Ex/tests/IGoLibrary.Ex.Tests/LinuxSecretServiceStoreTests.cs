using IGoLibrary.Ex.Domain.Enums;
using IGoLibrary.Ex.Domain.Models;
using IGoLibrary.Ex.Infrastructure.Security;

namespace IGoLibrary.Ex.Tests;

public sealed class LinuxSecretServiceStoreTests
{
    [Fact]
    public async Task CredentialStore_RoundTripsAndTracksBothCredentialTypes()
    {
        var client = new FakeLinuxSecretServiceClient();
        var tracker = new FakePersistentDataChangeTracker();
        var store = new LinuxSecretServiceCredentialStore(client, tracker);
        var session = new SessionCredentials(
            "cookie-value",
            SessionSource.ManualCookie,
            DateTimeOffset.UtcNow,
            true);
        var remote = new RemoteCheckInSessionCredentials(
            "remote-token",
            DateTimeOffset.UtcNow,
            true,
            DateTimeOffset.UtcNow.AddDays(1));

        await store.SaveSessionAsync(session);
        await store.SaveRemoteCheckInSessionAsync(remote);

        Assert.Equal(session, await store.LoadSessionAsync());
        Assert.Equal(remote, await store.LoadRemoteCheckInSessionAsync());
        Assert.Equal(2, tracker.Version);
        Assert.Contains("session", client.Values.Keys);
        Assert.Contains("remote-check-in", client.Values.Keys);

        await store.ClearSessionAsync();
        await store.ClearRemoteCheckInSessionAsync();

        Assert.Null(await store.LoadSessionAsync());
        Assert.Null(await store.LoadRemoteCheckInSessionAsync());
        Assert.Equal(4, tracker.Version);
    }

    [Fact]
    public async Task BackupBackend_UsesPersistentSecretServiceStorage()
    {
        var client = new FakeLinuxSecretServiceClient();
        var backend = new LinuxBackupSecretBackend(client);

        await backend.WriteAsync("webdav", "password-value", CancellationToken.None);

        Assert.True(backend.IsPersistent);
        Assert.Equal(
            "password-value",
            await backend.ReadAsync("webdav", CancellationToken.None));
        await backend.DeleteAsync("webdav", CancellationToken.None);
        Assert.Null(await backend.ReadAsync("webdav", CancellationToken.None));
    }

    [Fact]
    public void PlatformCredentialStore_UsesSecretServiceOnLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Assert.IsType<LinuxSecretServiceCredentialStore>(PlatformCredentialStore.CreateDefault());
    }

    private sealed class FakeLinuxSecretServiceClient : ILinuxSecretServiceClient
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public bool IsAvailable => true;

        public Task<string?> LookupAsync(
            string account,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Values.GetValueOrDefault(account));
        }

        public Task StoreAsync(
            string account,
            string label,
            string secret,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Values[account] = secret;
            return Task.CompletedTask;
        }

        public Task ClearAsync(
            string account,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Values.Remove(account);
            return Task.CompletedTask;
        }
    }
}

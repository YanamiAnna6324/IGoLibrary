using System.Text.Json;
using IGoLibrary.Ex.Application.Abstractions;
using IGoLibrary.Ex.Domain.Models;
using IGoLibrary.Ex.Infrastructure.Persistence;

namespace IGoLibrary.Ex.Infrastructure.Security;

internal sealed class LinuxSecretServiceCredentialStore(
    ILinuxSecretServiceClient client,
    IPersistentDataChangeTracker? changeTracker = null) : ICredentialStore
{
    private const string SessionAccount = "session";
    private const string RemoteCheckInAccount = "remote-check-in";

    public Task SaveSessionAsync(
        SessionCredentials credentials,
        CancellationToken cancellationToken = default)
        => SaveTrackedAsync(SessionAccount, "IGoLibrary-Ex 图书馆会话", credentials, cancellationToken);

    public Task<SessionCredentials?> LoadSessionAsync(CancellationToken cancellationToken = default)
        => LoadAsync<SessionCredentials>(SessionAccount, cancellationToken);

    public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        => ClearTrackedAsync(SessionAccount, cancellationToken);

    public Task SaveRemoteCheckInSessionAsync(
        RemoteCheckInSessionCredentials credentials,
        CancellationToken cancellationToken = default)
        => SaveTrackedAsync(
            RemoteCheckInAccount,
            "IGoLibrary-Ex 远程签到会话",
            credentials,
            cancellationToken);

    public Task<RemoteCheckInSessionCredentials?> LoadRemoteCheckInSessionAsync(
        CancellationToken cancellationToken = default)
        => LoadAsync<RemoteCheckInSessionCredentials>(RemoteCheckInAccount, cancellationToken);

    public Task ClearRemoteCheckInSessionAsync(CancellationToken cancellationToken = default)
        => ClearTrackedAsync(RemoteCheckInAccount, cancellationToken);

    private async Task SaveTrackedAsync<T>(
        string account,
        string label,
        T value,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(value, AppJson.Default);
        await client.StoreAsync(account, label, payload, cancellationToken);
        changeTracker?.MarkChanged();
    }

    private async Task<T?> LoadAsync<T>(string account, CancellationToken cancellationToken)
    {
        var payload = await client.LookupAsync(account, cancellationToken);
        return string.IsNullOrWhiteSpace(payload)
            ? default
            : JsonSerializer.Deserialize<T>(payload, AppJson.Default);
    }

    private async Task ClearTrackedAsync(string account, CancellationToken cancellationToken)
    {
        await client.ClearAsync(account, cancellationToken);
        changeTracker?.MarkChanged();
    }
}

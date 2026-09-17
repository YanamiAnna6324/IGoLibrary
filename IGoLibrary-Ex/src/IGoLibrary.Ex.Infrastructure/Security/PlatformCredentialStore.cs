using System.Runtime.InteropServices;
using IGoLibrary.Ex.Application.Abstractions;

namespace IGoLibrary.Ex.Infrastructure.Security;

public static class PlatformCredentialStore
{
    public static ICredentialStore CreateDefault(IPersistentDataChangeTracker? changeTracker = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsCredentialStore(changeTracker);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacKeychainCredentialStore(changeTracker);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxSecretServiceCredentialStore(
                new LinuxSecretServiceClient(),
                changeTracker);
        }

        return new InMemoryCredentialStore(changeTracker);
    }
}

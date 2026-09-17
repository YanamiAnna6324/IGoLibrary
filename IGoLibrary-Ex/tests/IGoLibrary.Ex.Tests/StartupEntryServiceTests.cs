using IGoLibrary.Ex.Desktop.Services;

namespace IGoLibrary.Ex.Tests;

public sealed class StartupEntryServiceTests
{
    [Fact]
    public void ResolveLinuxAutostartPath_PrefersAbsoluteXdgConfigHome()
    {
        var path = StartupEntryService.ResolveLinuxAutostartPath(
            "/custom/config",
            "/home/tester");

        Assert.Equal(
            Path.Combine("/custom/config", "autostart", "igolibrary-ex.desktop"),
            path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/config")]
    public void ResolveLinuxAutostartPath_FallsBackToUserConfigDirectory(string? xdgConfigHome)
    {
        var path = StartupEntryService.ResolveLinuxAutostartPath(
            xdgConfigHome,
            "/home/tester");

        Assert.Equal(
            Path.Combine("/home/tester", ".config", "autostart", "igolibrary-ex.desktop"),
            path);
    }

    [Fact]
    public void BuildLinuxAutostartEntry_EscapesReservedExecCharacters()
    {
        var executablePath = Path.Combine(
            Path.GetPathRoot(Environment.CurrentDirectory)!,
            "opt",
            "I Go $Library%",
            "app\"name");

        var entry = StartupEntryService.BuildLinuxAutostartEntry(executablePath);

        Assert.Contains("[Desktop Entry]\n", entry);
        Assert.Contains("Type=Application\n", entry);
        Assert.Contains("Terminal=false\n", entry);
        Assert.Contains("\\$Library%%", entry);
        Assert.Contains("app\\\"name", entry);
    }

    [Fact]
    public void IsLinuxAutostartEntryForExecutable_RejectsMovedOrHiddenEntry()
    {
        var executablePath = Path.Combine(
            Path.GetPathRoot(Environment.CurrentDirectory)!,
            "opt",
            "IGoLibrary-Ex",
            "IGoLibrary.Ex.Desktop");
        var entry = StartupEntryService.BuildLinuxAutostartEntry(executablePath);

        Assert.True(StartupEntryService.IsLinuxAutostartEntryForExecutable(
            entry,
            executablePath));
        Assert.False(StartupEntryService.IsLinuxAutostartEntryForExecutable(
            entry,
            executablePath + ".moved"));
        Assert.False(StartupEntryService.IsLinuxAutostartEntryForExecutable(
            entry + "Hidden=true\n",
            executablePath));
    }
}

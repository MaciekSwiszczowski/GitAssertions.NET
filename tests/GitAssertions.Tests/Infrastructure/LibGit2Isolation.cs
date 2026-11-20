using System.Runtime.CompilerServices;

using LibGit2Sharp;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Cuts libgit2 off from the machine's and the user's git config for the whole test process, before any test runs.
/// </summary>
internal static class LibGit2Isolation
{
    /// <summary>
    /// libgit2 reads the machine's git config and its global ignore file, for example Git for Windows'
    /// core.autocrlf=true or a user's *.log rule, so the same test could pass on CI and fail on a desk. An empty search
    /// path turns those levels off. The setting is process-wide, which is the point: the library under test runs in
    /// this process and sees the same clean slate. It runs as the module loads, not on first use of a fixture, so every
    /// test here gets the same settings whichever runs first. A test of a real repository does not belong in this
    /// project: it needs the machine's config, as <c>git status</c> reads it, so it lives in
    /// GitAssertions.ThisRepository.Tests.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        foreach (ConfigurationLevel level in new[]
        {
            ConfigurationLevel.ProgramData,
            ConfigurationLevel.System,
            ConfigurationLevel.Xdg,
            ConfigurationLevel.Global,
        })
        {
            GlobalSettings.SetConfigSearchPaths(level, []);
        }
    }
}

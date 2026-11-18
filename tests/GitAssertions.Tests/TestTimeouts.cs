namespace GitAssertions.Tests;

/// <summary>
/// The one deadline every test that can wait shares, so two files never pick different numbers by accident. A
/// timeout ends the run, not the work, so a test that awaits also passes <c>TestContext.Current.CancellationToken</c>.
/// </summary>
internal static class TestTimeouts
{
    /// <summary>
    /// A test here is a handful of file operations on a throwaway repository, which takes well under a second. The
    /// rest is room for a loaded build agent and for antivirus scanning a freshly written <c>.git</c> folder.
    /// </summary>
    public const int DefaultMilliseconds = 30_000;
}

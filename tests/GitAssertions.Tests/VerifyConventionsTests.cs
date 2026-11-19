namespace GitAssertions.Tests;

/// <summary>
/// Pins the repository settings Verify's snapshot files depend on: .gitattributes keeps them LF on checkout, and
/// .editorconfig keeps an editor from adding a final newline, dropping the BOM, or trimming trailing whitespace.
/// </summary>
public sealed class VerifyConventionsTests
{
    // Trigger: Someone edits .gitattributes or .editorconfig and drops a setting Verify's snapshot files need.
    // Behavior: The suite says so directly, instead of snapshot tests failing for a reason unrelated to the code.
    // Assertion: Verify's own conventions check passes.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public Task RepositorySettings_ForSnapshotFiles_MatchVerifyConventions()
    {
        // Assert
        return VerifyChecks.Run().WaitAsync(TestContext.Current.CancellationToken);
    }
}

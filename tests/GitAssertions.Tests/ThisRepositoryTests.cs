using System.Reflection;

using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.Tests;

/// <summary>
/// Runs the library's own checks on this repository, the one these tests are built from, so an agent that leaves it
/// in a state a check reports fails the suite here first. Each check is added here as it lands (docs/tasks.md,
/// Phase 3). libgit2 reads no machine or user git config in this test process
/// (<see cref="Infrastructure.LibGit2Isolation"/>), so a file ignored only by a developer's own global ignore file
/// still counts: the repository's .gitignore must cover it, as it must on CI.
/// </summary>
public sealed class ThisRepositoryTests
{
    /// <summary>
    /// The repository root, written into the test assembly by the test project file at build time, so the tests never
    /// search the disk for a .git folder and cannot end up checking some other repository.
    /// </summary>
    private static readonly string RepositoryRoot = typeof(ThisRepositoryTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(static attribute => string.Equals(attribute.Key, "RepositoryRoot", StringComparison.Ordinal))
        .Value!;

    // Trigger: An agent working on this repository creates a file and adds it neither to Git nor to .gitignore.
    // Behavior: The suite fails, and the failure names the file and the commands that fix it.
    // Assertion: The shared healthy snapshot: a healthy result with no files. On a failure, the received file holds
    //            the check's whole result.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public Task NoUntrackedFiles_ThisRepository_IsHealthy()
    {
        // Act
        CheckResult result = GitAssert.Open(RepositoryRoot).NoUntrackedFiles();

        // Assert
        return CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUntrackedFiles), result);
    }
}

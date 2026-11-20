using System.Reflection;

using GitAssertions.Tests;
using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.ThisRepository.Tests;

/// <summary>
/// Runs the library's own checks on this repository, the one these tests are built from, so an agent that leaves it
/// in a state a check reports fails the suite here first. Each check is added here as it lands (docs/tasks.md,
/// Phase 3). This project does not cut libgit2 off from the machine's git config, as GitAssertions.Tests does: libgit2
/// settings are process-wide, and a real repository must be read as <c>git status</c> reads it, with the same
/// core.autocrlf, or a CRLF file saved unchanged would read as modified. A user's own test project sees the same
/// config. The same goes for a developer's global ignore file: a file only that file ignores is ignored here too, and
/// CI, which has no such file, reports it.
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

    // Trigger: An agent working on this repository edits or deletes a tracked file and does not stage the change.
    // Behavior: The suite fails, and the failure names the file and the commands that fix it.
    // Assertion: The shared healthy snapshot: a healthy result with no files. On a failure, the received file holds
    //            the check's whole result.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public Task NoUnstagedChanges_ThisRepository_IsHealthy()
    {
        // Act
        CheckResult result = GitAssert.Open(RepositoryRoot).NoUnstagedChanges();

        // Assert
        return CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUnstagedChanges), result);
    }

    // Trigger: An agent working on this repository is handed a merge, pull, or rebase that stopped on a conflict, and
    //          leaves a conflicted file unresolved or resolved but not staged.
    // Behavior: The suite fails, and the failure names the file and the command that stages it once resolved.
    // Assertion: The shared healthy snapshot: a healthy result with no files. On a failure, the received file holds
    //            the check's whole result.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public Task NoConflicts_ThisRepository_IsHealthy()
    {
        // Act
        CheckResult result = GitAssert.Open(RepositoryRoot).NoConflicts();

        // Assert
        return CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoConflicts), result);
    }
}

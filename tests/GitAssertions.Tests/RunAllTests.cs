using GitAssertions.Tests.Infrastructure;

using LibGit2Sharp;

namespace GitAssertions.Tests;

/// <summary>
/// Pins <see cref="GitAssert.RunAll"/> (docs/specification.md, Public API and RunAll message): it runs the five checks
/// one after another and reports every result, healthy ones included, and the failing ones again in
/// <see cref="RunAllResult.Failures"/> and in one message. A failing check does not stop the checks after it. Each
/// check's own scenarios are pinned by its own tests and are not repeated here. The two snapshot tests end in their
/// snapshot with no other assertion: a reviewed <c>.verified.txt</c> file beside this class holds the whole result. The
/// healthy one ends in <see cref="CheckResultSnapshot.VerifyHealthy(string, RunAllResult, string)"/> and shares
/// RunAll.IsHealthy.verified.txt; the failing one has a file of its own. The two value tests pin what a snapshot cannot
/// state: an exception passing through, and a result that cannot be changed after it is made.
/// </summary>
public sealed class RunAllTests
{
    // Trigger: An agent finishes its work and leaves the repository exactly as it was committed.
    // Behavior: Every check passes, so RunAll is healthy and says so in one line.
    // Assertion: The shared healthy snapshot: five healthy results in the spec's order, no failures, and the message
    //            "All Git checks passed.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task RunAll_CleanRepository_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        RunAllResult result = GitAssert.Open(repo.Root).RunAll();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.RunAll), result);
    }

    // Trigger: An agent creates tmp/notes.txt and never adds it, and edits the tracked readme.txt and never stages it.
    // Behavior: Two checks fail and the three after them still run and pass. RunAll lists the two failures and prints
    //           the spec's RunAll example: a header, then each failing check's own message, unchanged.
    // Assertion: The snapshot shows five results, NoUntrackedFiles and NoUnstagedChanges unhealthy with their files and
    //            the other three healthy, the two failures, and the message of the spec's example, character for
    //            character.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task RunAll_UntrackedFileAndEditedFile_FailsTwoChecksAndPrintsSpecExample()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("tmp/notes.txt").AddFile(TempRepo.InitialFile, "changed\n");

        // Act
        RunAllResult result = GitAssert.Open(repo.Root).RunAll();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A test deletes the repository folder after opening it, then runs RunAll on the stored instance, so
    //          every check fails to read it.
    // Behavior: RunAll does not swallow the error or collect it into a result: it throws the exception of the first
    //           check to fail, unchanged, as that check throws it when called on its own. That the checks after it never
    //           start cannot be seen from outside, because each of them would throw the same exception.
    // Assertion: Exactly an InvalidOperationException, with no inner exception, whose message equals the one
    //            LibGit2Sharp gives for opening that folder.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void RunAll_RepositoryDeletedAfterOpen_ThrowsTheCheckExceptionUnchanged()
    {
        // Arrange
        TempRepo repo = TempRepo.Create();
        string root = repo.Root;
        GitAssert git;
        using (repo)
        {
            git = GitAssert.Open(root);
        }

        string libGit2SharpMessage = Should.Throw<RepositoryNotFoundException>(() => new Repository(root)).Message;

        // Assert
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => git.RunAll());
        exception.GetType().ShouldBe(typeof(InvalidOperationException));
        exception.InnerException.ShouldBeNull();
        exception.Message.ShouldBe(libGit2SharpMessage);
    }

    // Trigger: A test author casts a RunAll result's Results and Failures to writable lists and puts another check's
    //          result in the first place of each.
    // Behavior: Both lists refuse the change, so a result stored or shared between tests keeps what the checks found.
    // Assertion: Setting the first element of either list throws NotSupportedException, and each list still holds
    //            the results it held.
    [Fact]
    public void Create_ResultsAndFailuresCastToWritableLists_RefuseChange()
    {
        // Arrange
        CheckResult healthy = CheckResult.Healthy("HealthyRule");
        CheckResult failing = CheckResult.Failing(
            "FailingRule", "1 path found.", [FailureEntry.Path("a.txt")],
            [FixStep.WithPaths("Fix:", "git add")], ["a.txt"]);
        RunAllResult result = RunAllResult.Create([healthy, failing]);
        IList<CheckResult> results = (IList<CheckResult>)result.Results;
        IList<CheckResult> failures = (IList<CheckResult>)result.Failures;

        // Assert
        Should.Throw<NotSupportedException>(() => results[0] = failing);
        Should.Throw<NotSupportedException>(() => failures[0] = healthy);
        result.Results.ShouldBe([healthy, failing]);
        result.Failures.ShouldBe([failing]);
    }
}

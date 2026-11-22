using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.Tests;

/// <summary>
/// Pins <see cref="GitAssert.NoOperationInProgress"/> against real repositories (docs/specification.md, Checks,
/// NoOperationInProgress): a merge, rebase, cherry-pick, or revert that stopped, on a conflict or without one, and was
/// neither finished nor aborted fails the check, and the message names the operation and its own <c>--continue</c> and
/// <c>--abort</c> commands. Every operation here is set up with LibGit2Sharp, which leaves it in progress as Git does:
/// each stops on a conflict, except a merge that stops before its commit (<c>git merge --no-commit</c>). A
/// cherry-pick or revert of several commits, a rebase stopped without a conflict, and a bisect are not set up here:
/// libgit2 has no sequencer, no interactive rebase, and no bisect, so the scripts
/// <c>Cherry_pick_sequence_conflict</c>, <c>Revert_sequence_conflict</c>, <c>Rebase_exec_fails</c>, and
/// <c>Bisect_in_progress</c> (<see cref="Scripts.ScriptTests"/>) build them with real Git; a bisect has its own
/// message, with <c>git bisect reset</c>. An operation that was aborted passes: a rebase aborted with LibGit2Sharp is
/// here, and a merge aborted with <c>git merge --abort</c>, which LibGit2Sharp cannot do, is the script
/// <c>Merge_conflict_aborted</c>. Any other operation uses the generic message and is not tested in v1:
/// libgit2 cannot set up a <c>git am</c>, and Git's state files are never written by hand (docs/specification.md,
/// Test tooling). Every test is a snapshot test with no other assertion: a reviewed <c>.verified.txt</c> file beside
/// this class holds the whole result, rule, health, files, and message. A failing scenario ends in
/// <see cref="CheckResultSnapshot.Verify(CheckResult, string)"/> and has a file of its own; the healthy one ends in
/// <see cref="CheckResultSnapshot.VerifyHealthy(string, CheckResult, string)"/> and shares
/// NoOperationInProgress.IsHealthy.verified.txt.
/// </summary>
public sealed class NoOperationInProgressTests
{
    private const string Feature = "feature";

    // Trigger: An agent finishes its work in a repository where no operation stopped halfway.
    // Behavior: The check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files and the message
    //            "NoOperationInProgress: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_CleanRepository_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoOperationInProgress), result);
    }

    // Trigger: An agent works in a repository created with git init, before anything was committed.
    // Behavior: A repository with no commits has no operation in progress, and the check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files and the message
    //            "NoOperationInProgress: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_RepositoryWithNoCommits_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.CreateUnborn();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoOperationInProgress), result);
    }

    // Trigger: A feature branch and main both change the same line of readme.txt, and merging the feature branch stops
    //          on the conflict; nobody finishes or aborts the merge.
    // Behavior: The check fails, names the merge, and offers git merge --continue once the conflicts are resolved and
    //           staged, or git merge --abort.
    // Assertion: The snapshot's message is the spec's NoOperationInProgress example character for character, and Files
    //            is empty.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_MergeLeftOpenOnConflict_PrintsSpecExample()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        DivergeOnInitialFile(repo).Merge(Feature);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A feature branch and main both change the same line of readme.txt, and rebasing the feature branch onto
    //          main stops on the conflict; nobody finishes or aborts the rebase.
    // Behavior: The check fails and names the rebase, although libgit2 calls this state RebaseMerge; the commands are
    //           git rebase --continue and git rebase --abort.
    // Assertion: The snapshot's message names "a rebase" and both rebase commands, and Files is empty.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_RebaseLeftOpenOnConflict_FailsAsRebase()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        DivergeOnInitialFile(repo).Checkout(Feature).Rebase(TempRepo.MainBranch);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A feature branch and main both change the same line of readme.txt, and cherry-picking the feature
    //          branch's commit onto main stops on the conflict; nobody finishes or aborts the cherry-pick.
    // Behavior: The check fails and names the cherry-pick, with git cherry-pick --continue and git cherry-pick --abort.
    // Assertion: The snapshot's message names "a cherry-pick" and both cherry-pick commands, and Files is empty.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_CherryPickLeftOpenOnConflict_FailsAsCherryPick()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        DivergeOnInitialFile(repo).CherryPick(Feature);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent reverts a commit that edited a line of readme.txt which a later commit edited again, and the
    //          revert stops on the conflict; nobody finishes or aborts it.
    // Behavior: The check fails and names the revert, with git revert --continue and git revert --abort.
    // Assertion: The snapshot's message names "a revert" and both revert commands, and Files is empty.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_RevertLeftOpenOnConflict_FailsAsRevert()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile(TempRepo.InitialFile, $"first{Lf}").Stage(TempRepo.InitialFile).Commit("Edit first")
            .AddFile(TempRepo.InitialFile, $"second{Lf}").Stage(TempRepo.InitialFile).Commit("Edit second")
            .Revert("HEAD~1");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A feature branch and main each add a file of their own, and merging the feature branch stops before
    //          the merge commit, as git merge --no-commit does; there is no conflict, and nobody commits or aborts it.
    // Behavior: The check fails and names the merge, and its Fix line does not tell the reader to resolve conflicts
    //           that do not exist: it says "once any conflicts are resolved and staged", with the same two commands.
    // Assertion: The snapshot's message names "a merge" and "once any conflicts", with git merge --continue and
    //            git merge --abort, and Files is empty.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_MergeStoppedBeforeCommit_FailsAsMerge()
    {
        // Arrange
        const string featureFile = "feature.txt";
        const string mainFile = "main.txt";
        using TempRepo repo = TempRepo.Create();
        repo.Diverge(
                Feature,
                onFeature: static branch => branch.AddFile(featureFile).Stage(featureFile),
                onMain: static branch => branch.AddFile(mainFile).Stage(mainFile))
            .MergeWithoutCommit(Feature);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A feature branch and main both change the same line of readme.txt, rebasing the feature branch onto main
    //          stops on the conflict, and the agent aborts the rebase, as git rebase --abort does.
    // Behavior: The check passes: an aborted rebase leaves nothing in progress.
    // Assertion: The shared healthy snapshot: a healthy result with no files and the message
    //            "NoOperationInProgress: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoOperationInProgress_RebaseAborted_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        DivergeOnInitialFile(repo).Checkout(Feature).Rebase(TempRepo.MainBranch).AbortRebase();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoOperationInProgress();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoOperationInProgress), result);
    }

    /// <summary>
    /// A feature branch and main each commit their own text for readme.txt, so any operation that brings one into the
    /// other stops on a conflict. main is left checked out.
    /// </summary>
    private static TempRepo DivergeOnInitialFile(TempRepo repo)
    {
        return repo.Diverge(
            Feature,
            onFeature: static branch => branch.AddFile(TempRepo.InitialFile, $"feature{Lf}").Stage(TempRepo.InitialFile),
            onMain: static branch => branch.AddFile(TempRepo.InitialFile, $"main{Lf}").Stage(TempRepo.InitialFile));
    }
}

using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.Tests;

/// <summary>
/// Pins <see cref="GitAssert.NoConflicts"/> against real repositories (docs/specification.md, Checks, NoConflicts): a
/// file left with an unresolved conflict fails the check, whatever shape the conflict takes (both sides edited it, one
/// side deleted it, both sides added it, or it is binary and holds no conflict markers); once it is resolved and
/// staged, the check passes. The conflicts come from merging a feature branch into main, because Git records a
/// conflict only when an operation stops on one; the check reads the result and does not care which operation it was.
/// Every test is a snapshot test with no other assertion: a reviewed <c>.verified.txt</c> file beside this class holds
/// the whole result, rule, health, files, and message. A failing scenario ends in
/// <see cref="CheckResultSnapshot.Verify(CheckResult, string)"/> and has a file of its own; every healthy one ends in
/// <see cref="CheckResultSnapshot.VerifyHealthy(string, CheckResult, string)"/> and shares
/// NoConflicts.IsHealthy.verified.txt.
/// </summary>
public sealed class NoConflictsTests
{
    private const string Feature = "feature";

    private const string Program = "src/Program.cs";

    // Trigger: An agent finishes its work in a repository where no merge stopped on a conflict.
    // Behavior: The check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files and the message "NoConflicts: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_CleanRepository_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoConflicts), result);
    }

    // Trigger: An agent works in a repository created with git init, before anything was committed.
    // Behavior: A repository with no commits has no conflicts and no index file yet, and the check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_RepositoryWithNoCommits_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.CreateUnborn();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoConflicts), result);
    }

    // Trigger: A feature branch and main both change the same line of src/Program.cs, and merging the feature branch
    //          stops on the conflict; nobody resolves it.
    // Behavior: The check fails, names the file as conflicted, and says to resolve it and stage it with git add.
    // Assertion: The snapshot's message is the spec's NoConflicts example character for character.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_MergeEditedSameLine_PrintsSpecExample()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile(Program, "Console.WriteLine(\"base\");\n").Stage(Program).Commit("Add Program.cs");
        MergeDivergedBranches(
            repo,
            onFeature: static branch => branch.AddFile(Program, "Console.WriteLine(\"feature\");\n").Stage(Program),
            onMain: static branch => branch.AddFile(Program, "Console.WriteLine(\"main\");\n").Stage(Program));

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A feature branch and main both change readme.txt and src/Program.cs, and merging the feature branch
    //          stops on both files; nobody resolves them.
    // Behavior: The check fails and lists both files, sorted, with both paths in the one git add command.
    // Assertion: The snapshot shows both files in Files and the message in the plural, readme.txt first.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_MergeConflictsInTwoFiles_ListsBoth()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile(Program, "Console.WriteLine(\"base\");\n").Stage(Program).Commit("Add Program.cs");
        MergeDivergedBranches(
            repo,
            onFeature: static branch => branch
                .AddFile(TempRepo.InitialFile, "feature\n")
                .AddFile(Program, "Console.WriteLine(\"feature\");\n")
                .Stage(TempRepo.InitialFile, Program),
            onMain: static branch => branch
                .AddFile(TempRepo.InitialFile, "main\n")
                .AddFile(Program, "Console.WriteLine(\"main\");\n")
                .Stage(TempRepo.InitialFile, Program));

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A merge stops on a conflict in readme.txt, and an agent edits the file to the resolved text and stages
    //          it with git add; the merge itself is not committed yet.
    // Behavior: Staging the resolved file replaces its conflicting versions in the index, so the check passes; the
    //           unfinished merge is the operation check's business.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_ConflictResolvedAndStaged_IsHealthy()
    {
        // Arrange
        // A healthy result cannot show that the merge stopped first. That this merge leaves readme.txt conflicted is
        // pinned by TempRepoScenarioTests.Merge_BranchEditedSameLine_LeavesConflictAndMergeInProgress.
        using TempRepo repo = TempRepo.Create();
        MergeDivergedBranches(
            repo,
            onFeature: static branch => branch.AddFile(TempRepo.InitialFile, "feature\n").Stage(TempRepo.InitialFile),
            onMain: static branch => branch.AddFile(TempRepo.InitialFile, "main\n").Stage(TempRepo.InitialFile));
        repo.AddFile(TempRepo.InitialFile, "resolved\n").Stage(TempRepo.InitialFile);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoConflicts), result);
    }

    // Trigger: A merge stops on a conflict in readme.txt, and an agent stages the file with git add as the merge left
    //          it, conflict markers and all.
    // Behavior: The check reads only what Git records, and git add has marked the conflict resolved, so the check
    //           passes; it does not search files for markers (docs/specification.md, Checks, and the README).
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_ConflictStagedWithMarkersLeft_IsHealthy()
    {
        // Arrange
        // That this merge writes conflict markers into readme.txt is pinned by
        // TempRepoScenarioTests.Merge_BranchEditedSameLine_LeavesConflictAndMergeInProgress.
        using TempRepo repo = TempRepo.Create();
        MergeDivergedBranches(
            repo,
            onFeature: static branch => branch.AddFile(TempRepo.InitialFile, "feature\n").Stage(TempRepo.InitialFile),
            onMain: static branch => branch.AddFile(TempRepo.InitialFile, "main\n").Stage(TempRepo.InitialFile));
        repo.Stage(TempRepo.InitialFile);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoConflicts), result);
    }

    // Trigger: main deletes readme.txt while a feature branch edits it, and merging the feature branch stops on the
    //          conflict; git status shows DU.
    // Behavior: The check fails and names the file as conflicted, although the index holds no version from main, the
    //           side that deleted it. git add resolves it either way: it stages the file if it is kept on disk, and
    //           the deletion if it is removed.
    // Assertion: The snapshot shows readme.txt as conflicted.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_DeletedOnMainEditedOnBranch_FailsAsConflicted()
    {
        // Arrange
        // Measured on 2026-11-20 with Git for Windows 2.56.0: git ls-files -u lists stages 1 and 3 only, and git add
        // resolves the file whether it was kept or deleted.
        using TempRepo repo = TempRepo.Create();
        MergeDivergedBranches(
            repo,
            onFeature: static branch => branch.AddFile(TempRepo.InitialFile, "feature\n").Stage(TempRepo.InitialFile),
            onMain: static branch => branch.Delete(TempRepo.InitialFile).Stage(TempRepo.InitialFile));

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: main and a feature branch each create new.txt with different content, and merging the feature branch
    //          stops on the conflict; git status shows AA.
    // Behavior: The check fails and names the file as conflicted, although the file has no common ancestor version.
    // Assertion: The snapshot shows new.txt as conflicted.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_SameNewFileAddedOnBothBranches_FailsAsConflicted()
    {
        // Arrange
        // A conflicted file holds up to three versions in the index: stage 1 the common ancestor, stage 2 ours,
        // stage 3 theirs: https://git-scm.com/docs/git-merge#_how_conflicts_are_presented
        // Measured on 2026-11-20 with Git for Windows 2.56.0: for this conflict git ls-files -u lists stages 2 and 3
        // only, because the file has no ancestor.
        using TempRepo repo = TempRepo.Create();
        MergeDivergedBranches(
            repo,
            onFeature: static branch => branch.AddFile("new.txt", "feature\n").Stage("new.txt"),
            onMain: static branch => branch.AddFile("new.txt", "main\n").Stage("new.txt"));

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: main and a feature branch both change the binary file data.bin, and merging the feature branch stops on
    //          the conflict.
    // Behavior: Git cannot merge a binary file and writes no conflict markers into it, but the index still holds the
    //           conflict, so the check fails and names the file as conflicted.
    // Assertion: The snapshot shows data.bin as conflicted.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoConflicts_BinaryFileChangedOnBothBranches_FailsAsConflicted()
    {
        // Arrange
        // The NUL character makes Git treat the content as binary, as it guesses from the start of the content:
        // https://git-scm.com/docs/gitattributes#_marking_files_as_binary
        // Measured on 2026-11-20 with Git for Windows 2.56.0: git merge prints "Cannot merge binary files", git status
        // shows UU, and the file holds no conflict markers.
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("data.bin", "\0base").Stage("data.bin").Commit("Add data.bin");
        MergeDivergedBranches(
            repo,
            onFeature: static branch => branch.AddFile("data.bin", "\0feature").Stage("data.bin"),
            onMain: static branch => branch.AddFile("data.bin", "\0main").Stage("data.bin"));

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoConflicts();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    /// <summary>
    /// Commits <paramref name="onFeature"/>'s change on a new feature branch and <paramref name="onMain"/>'s change on
    /// main, then merges the feature branch into main, as when work done in parallel is brought together.
    /// </summary>
    private static void MergeDivergedBranches(
        TempRepo repo,
        Func<TempRepo, TempRepo> onFeature,
        Func<TempRepo, TempRepo> onMain)
    {
        repo.Diverge(Feature, onFeature, onMain).Merge(Feature);
    }
}

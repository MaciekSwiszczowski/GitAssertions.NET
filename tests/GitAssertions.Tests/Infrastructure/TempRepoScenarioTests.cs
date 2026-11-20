using LibGit2Sharp;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Pins the scenario helpers of <see cref="TempRepo"/> (<c>AddFile</c>, <c>Stage</c>, <c>Commit</c>, <c>Delete</c>,
/// <c>StageAsExecutable</c>, <c>CreateBranch</c>, <c>Checkout</c>, <c>AddGitIgnore</c>): each one must leave the
/// repository in the state its name says, because every later test sets up its own scenario with them and trusts the
/// result.
/// </summary>
public sealed class TempRepoScenarioTests
{
    // Trigger: A test author adds a file at a root path, or at a path whose folders do not exist yet.
    // Behavior: The file appears in the working tree as a new, untracked file, with its folders created as needed.
    // Assertion: The path's status reads NewInWorkdir.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("new.txt")]
    [InlineData("src/deep/new.txt")]
    public void AddFile_NewPath_IsNewInWorkdir(string path)
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        repo.AddFile(path);

        // Assert
        repo.Repo.RetrieveStatus(path).ShouldBe(FileStatus.NewInWorkdir);
    }

    // Trigger: A test author adds a file at the path of a file the repository already tracks.
    // Behavior: The tracked file is overwritten, so the change shows up as a modification.
    // Assertion: The path's status reads ModifiedInWorkdir.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void AddFile_TrackedPath_IsModifiedInWorkdir()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        repo.AddFile(TempRepo.InitialFile, "changed\n");

        // Assert
        repo.Repo.RetrieveStatus(TempRepo.InitialFile).ShouldBe(FileStatus.ModifiedInWorkdir);
    }

    // Trigger: A test author stages a file that is new to the repository.
    // Behavior: The file is in the index and not yet committed.
    // Assertion: The path's status reads NewInIndex.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Stage_NewFile_IsNewInIndex()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("new.txt");

        // Act
        repo.Stage("new.txt");

        // Assert
        repo.Repo.RetrieveStatus("new.txt").ShouldBe(FileStatus.NewInIndex);
    }

    // Trigger: A test author deletes a tracked file and stages the path.
    // Behavior: The deletion is staged, the way `git add` records a removed path.
    // Assertion: The path's status reads DeletedFromIndex.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Stage_DeletedFile_IsDeletedFromIndex()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.Delete(TempRepo.InitialFile);

        // Act
        // `git add <pathspec>` stages removals too; the --no-all option documents the older behavior that skipped
        // them: https://git-scm.com/docs/git-add
        repo.Stage(TempRepo.InitialFile);

        // Assert
        repo.Repo.RetrieveStatus(TempRepo.InitialFile).ShouldBe(FileStatus.DeletedFromIndex);
    }

    // Trigger: A test author stages a new file and commits it with a message.
    // Behavior: The commit lands on top of history, takes the staged file in, and leaves nothing pending.
    // Assertion: History reads "Add new.txt", "Initial commit"; the file is Unaltered; the status lists no file.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Commit_StagedFile_AddsCommitAndLeavesTreeClean()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("new.txt").Stage("new.txt");

        // Act
        repo.Commit("Add new.txt");

        // Assert
        repo.Repo.Commits.Select(static commit => commit.MessageShort).ShouldBe(["Add new.txt", "Initial commit"]);
        repo.Repo.RetrieveStatus("new.txt").ShouldBe(FileStatus.Unaltered);
        repo.Repo.RetrieveStatus().Select(static entry => entry.FilePath).ShouldBeEmpty();
    }

    // Trigger: A test author makes a second commit, on a machine in any month of any year.
    // Behavior: Both commits carry fixed times one minute apart, as author and committer, the same on every machine.
    // Assertion: Author and committer times read 2020-01-01 00:01 UTC for the second commit, 00:00 for the first.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Commit_StampsFixedTimesOneMinuteApart()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("new.txt").Stage("new.txt");

        // Act
        repo.Commit("Add new.txt");

        // Assert
        // Both are checked because the committer time is the one `git log` sorts by, and an author-only stamp would
        // leave it on the clock: https://git-scm.com/docs/git-commit#_date_formats
        DateTimeOffset[] newestFirst =
        [
            new(2020, 1, 1, 0, 1, 0, TimeSpan.Zero),
            new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ];
        repo.Repo.Commits.Select(static commit => commit.Author.When).ShouldBe(newestFirst);
        repo.Repo.Commits.Select(static commit => commit.Committer.When).ShouldBe(newestFirst);
    }

    // Trigger: A test author deletes a tracked file from the working tree and does nothing else.
    // Behavior: The file is gone from disk, and the index still holds it, so the status reports it as missing.
    // Assertion: The file does not exist on disk and its status reads DeletedFromWorkdir.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Delete_TrackedFile_IsDeletedFromWorkdir()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        repo.Delete(TempRepo.InitialFile);

        // Assert
        File.Exists(Path.Combine(repo.Root, TempRepo.InitialFile)).ShouldBeFalse();
        repo.Repo.RetrieveStatus(TempRepo.InitialFile).ShouldBe(FileStatus.DeletedFromWorkdir);
    }

    // Trigger: A test author stages a script as executable, the way `git add --chmod=+x` does.
    // Behavior: The index records the executable mode on any platform, also on Windows, where a file has no
    //           executable bit to read from disk.
    // Assertion: The index entry for build.sh reads ExecutableFile.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void StageAsExecutable_NewFile_IndexRecordsExecutableMode()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("build.sh");

        // Act
        repo.StageAsExecutable("build.sh");

        // Assert
        repo.Repo.Index["build.sh"].ShouldNotBeNull().Mode.ShouldBe(Mode.ExecutableFile);
    }

    // Trigger: A test author creates a branch and does not check it out.
    // Behavior: The branch exists at the commit HEAD points to, and HEAD stays on the branch it was on.
    // Assertion: The branch tip is the initial commit, HEAD still reads the initial branch, the status lists no file.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void CreateBranch_PointsAtHeadAndStaysOnMain()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string initialSha = repo.Repo.Head.Tip.Sha;

        // Act
        repo.CreateBranch("feature");

        // Assert
        repo.Repo.Branches["feature"].Tip.Sha.ShouldBe(initialSha);
        repo.Repo.Head.FriendlyName.ShouldBe(TempRepo.MainBranch);
        repo.Repo.RetrieveStatus().Select(static entry => entry.FilePath).ShouldBeEmpty();
    }

    // Trigger: A test author creates a branch and checks it out.
    // Behavior: HEAD moves onto that branch and the working tree stays clean.
    // Assertion: HEAD reads feature and the status lists no file.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Checkout_Branch_MovesHead()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.CreateBranch("feature");

        // Act
        repo.Checkout("feature");

        // Assert
        repo.Repo.Head.FriendlyName.ShouldBe("feature");
        repo.Repo.RetrieveStatus().Select(static entry => entry.FilePath).ShouldBeEmpty();
    }

    // Trigger: A test author commits a file on a feature branch and checks the initial branch out again.
    // Behavior: The file belongs to the feature branch only, so it leaves the working tree and the status.
    // Assertion: HEAD reads the initial branch, the file is not on disk, reads Nonexistent, and no file is listed.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Checkout_BackToMain_RemovesFileCommittedOnBranch()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.CreateBranch("feature")
            .Checkout("feature")
            .AddFile("feature.txt")
            .Stage("feature.txt")
            .Commit("Add feature.txt");

        // Act
        repo.Checkout(TempRepo.MainBranch);

        // Assert
        repo.Repo.Head.FriendlyName.ShouldBe(TempRepo.MainBranch);
        File.Exists(Path.Combine(repo.Root, "feature.txt")).ShouldBeFalse();
        repo.Repo.RetrieveStatus("feature.txt").ShouldBe(FileStatus.Nonexistent);
        repo.Repo.RetrieveStatus().Select(static entry => entry.FilePath).ShouldBeEmpty();
    }

    // Trigger: A test author adds an ignore rule for log files and then adds a log file.
    // Behavior: The log file is ignored, and the .gitignore itself is an ordinary new file, not staged.
    // Assertion: build.log reads Ignored and .gitignore reads NewInWorkdir.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void AddGitIgnore_MatchingFile_IsIgnored()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        repo.AddGitIgnore("*.log").AddFile("build.log");

        // Assert
        repo.Repo.RetrieveStatus("build.log").ShouldBe(FileStatus.Ignored);
        repo.Repo.RetrieveStatus(".gitignore").ShouldBe(FileStatus.NewInWorkdir);
    }

    // Trigger: A test author adds an ignore rule, then a second one in a separate call.
    // Behavior: The second call adds to the file and does not replace the first rule.
    // Assertion: Both a log file and a file inside tmp/ read Ignored.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void AddGitIgnore_CalledTwice_KeepsBothRules()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        // A pattern that ends in a slash matches a folder and everything under it, which is why tmp/notes.txt is
        // ignored: https://git-scm.com/docs/gitignore#_pattern_format
        repo.AddGitIgnore("*.log")
            .AddGitIgnore("tmp/")
            .AddFile("build.log")
            .AddFile("tmp/notes.txt");

        // Assert
        repo.Repo.RetrieveStatus("build.log").ShouldBe(FileStatus.Ignored);
        repo.Repo.RetrieveStatus("tmp/notes.txt").ShouldBe(FileStatus.Ignored);
    }
}

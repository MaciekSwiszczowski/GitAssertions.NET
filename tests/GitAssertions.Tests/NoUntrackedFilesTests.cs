using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.Tests;

/// <summary>
/// Pins <see cref="GitAssert.NoUntrackedFiles"/> against real repositories (docs/specification.md, Checks,
/// NoUntrackedFiles): a file Git does not track and does not ignore fails the check, wherever it sits; ignored files,
/// empty folders, and changes to tracked files do not. Every test is a snapshot test with no other assertion: a
/// reviewed <c>.verified.txt</c> file beside this class holds the whole result, rule, health, files, and message. A
/// failing scenario ends in <see cref="CheckResultSnapshot.Verify"/> and has a file of its own; every healthy one ends
/// in <see cref="CheckResultSnapshot.VerifyHealthy"/> and shares NoUntrackedFiles.IsHealthy.verified.txt.
/// </summary>
public sealed class NoUntrackedFilesTests
{
    // Trigger: An agent finishes its work and leaves the repository exactly as it was committed.
    // Behavior: The check passes.
    // Assertion: The snapshot shows a healthy result with no files and the message "NoUntrackedFiles: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUntrackedFiles_CleanRepository_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUntrackedFiles), result);
    }

    // Trigger: An agent creates notes.txt in the repository root and never adds it.
    // Behavior: The check fails, names the file, and offers to add it, delete it, or ignore it.
    // Assertion: The snapshot shows notes.txt in Files and the one-file message, in the singular.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUntrackedFiles_NewFileInRoot_FailsAndListsIt()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("notes.txt");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent leaves tmp/notes.txt and "docs/my notes.md" behind, in two folders Git has never seen.
    // Behavior: The check fails and prints exactly the spec's NoUntrackedFiles example.
    // Assertion: The snapshot's message is the spec example character for character, the path with a space quoted
    //            in the commands.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUntrackedFiles_TwoFilesInNewFolders_PrintsSpecExample()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("tmp/notes.txt").AddFile("docs/my notes.md");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent creates a folder tree, tmp/a.txt and tmp/sub/deeper/b.txt, and adds none of it.
    // Behavior: The check lists every file in the tree by its full path, not the folder tmp/ alone, so the fix
    //           commands name real files.
    // Assertion: The snapshot lists tmp/a.txt and tmp/sub/deeper/b.txt, in Files, the entries, and the commands.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUntrackedFiles_FilesDeepInNewFolder_ListsEachFileNotTheFolder()
    {
        // Arrange
        // The check matches git status --untracked-files=all, which lists each file in a new folder; the default
        // mode lists only the folder: https://git-scm.com/docs/git-status
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("tmp/a.txt").AddFile("tmp/sub/deeper/b.txt");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A build leaves a file that a committed .gitignore rule matches.
    // Behavior: Ignored files are what the ignore rules are for, so the check passes.
    // Assertion: Every row matches the shared healthy snapshot: a healthy result with no files.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("*.log", "debug.log")]
    [InlineData("bin/", "bin/Debug/app.dll")]
    [InlineData("/build", "build/output/report.txt")]
    public async Task NoUntrackedFiles_FileMatchingCommittedIgnoreRule_IsHealthy(string pattern, string path)
    {
        // Arrange
        // One row per gitignore pattern shape: a glob, a trailing slash (a folder and all below it), and a leading
        // slash (anchored to the folder of the .gitignore): https://git-scm.com/docs/gitignore
        using TempRepo repo = TempRepo.Create();
        repo.AddGitIgnore(pattern).Stage(".gitignore").Commit("Ignore build output").AddFile(path);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUntrackedFiles), result);
    }

    // Trigger: An agent creates empty folders and puts no file in them.
    // Behavior: Git tracks files, not folders, so an empty folder is not untracked, as in git status.
    // Assertion: The snapshot shows a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUntrackedFiles_EmptyFolders_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        Directory.CreateDirectory(Path.Combine(repo.Root, "empty", "nested"));

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUntrackedFiles), result);
    }

    // Trigger: An agent edits one tracked file, deletes another, and stages a new one, but leaves nothing untracked.
    // Behavior: Edits, deletes, and staged files are other checks' business, so this check passes.
    // Assertion: The snapshot shows a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUntrackedFiles_OnlyTrackedOrStagedChanges_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("edited.txt").AddFile("deleted.txt").Stage("edited.txt", "deleted.txt").Commit("Add two files");
        repo.AddFile("edited.txt", "changed\n").Delete("deleted.txt").AddFile("staged.txt").Stage("staged.txt");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUntrackedFiles), result);
    }

    // Trigger: An agent moves readme.txt to docs/readme.txt with a file operation, not with git mv.
    // Behavior: As in git status, the new path is untracked and is listed; the missing old path is left to the
    //           unstaged-changes check, not paired with it as a rename.
    // Assertion: The snapshot lists docs/readme.txt alone, as an untracked file, not as a rename.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUntrackedFiles_FileMovedWithoutGit_ListsNewPath()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("docs/readme.txt", "initial\n").Delete(TempRepo.InitialFile);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUntrackedFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }
}

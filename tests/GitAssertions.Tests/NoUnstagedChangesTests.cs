using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.Tests;

/// <summary>
/// Pins <see cref="GitAssert.NoUnstagedChanges"/> against real repositories (docs/specification.md, Checks,
/// NoUnstagedChanges): a tracked file that differs from the index, edited or deleted, fails the check, a file staged
/// and then changed again included; untracked and staged changes do not, and neither do the line-ending and
/// file-mode differences git status leaves out. A file recorded with <c>git add -N</c> (intent to add) is a kind of
/// its own and is offered <c>git add</c> and no <c>git restore</c>; LibGit2Sharp cannot record one, so the tests here
/// stage an empty file and then write it, which leaves the same state, and the scripts
/// <c>Intent_to_add_file</c>, <c>Intent_to_add_next_to_edited_file</c> and <c>Intent_to_add_empty_file</c>
/// (<see cref="Scripts.ScriptTests"/>) use real Git. Every test is a snapshot test with no other assertion: a reviewed
/// <c>.verified.txt</c> file beside this class holds the whole result, rule, health, files, and message. A failing
/// scenario ends in <see cref="CheckResultSnapshot.Verify(CheckResult, string)"/> and has a file of its own; every
/// healthy one ends in <see cref="CheckResultSnapshot.VerifyHealthy(string, CheckResult, string)"/> and shares
/// NoUnstagedChanges.IsHealthy.verified.txt.
/// </summary>
public sealed class NoUnstagedChangesTests
{
    /// <summary>
    /// The modification time a test gives a file it saves. Any fixed time other than the one the index cached will
    /// do: it makes Git read the file again instead of trusting the size and time it cached when the file was staged.
    /// </summary>
    private static readonly DateTime SavedAt = new(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    // Trigger: An agent finishes its work and leaves the repository exactly as it was committed.
    // Behavior: The check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files and the message
    //            "NoUnstagedChanges: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_CleanRepository_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUnstagedChanges), result);
    }

    // Trigger: An agent works in a repository created with git init, before anything was committed.
    // Behavior: A repository with no commits has no HEAD commit and no index file yet, so nothing differs from the
    //           index, and the check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files and the message
    //            "NoUnstagedChanges: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_RepositoryWithNoCommits_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.CreateUnborn();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUnstagedChanges), result);
    }

    // Trigger: An agent edits the tracked readme.txt and never stages the edit.
    // Behavior: The check fails, names the file as modified, and offers to stage the change or discard it.
    // Assertion: The snapshot shows readme.txt in Files and the one-file message, in the singular.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_EditedTrackedFile_FailsAsModified()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile(TempRepo.InitialFile, $"changed{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent deletes the tracked readme.txt from disk and never stages the removal.
    // Behavior: The check fails and names the file as deleted; git add stages the removal, git restore brings the
    //           file back.
    // Assertion: The snapshot shows readme.txt in Files and "deleted: readme.txt" in the message.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_DeletedTrackedFile_FailsAsDeleted()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.Delete(TempRepo.InitialFile);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent deletes the tracked old.txt and edits the tracked readme.txt, and stages neither.
    // Behavior: The check fails and prints exactly the spec's NoUnstagedChanges example.
    // Assertion: The snapshot's message is the spec example character for character, deleted before modified.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_DeletedAndEditedFiles_PrintsSpecExample()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("old.txt").Stage("old.txt").Commit("Add old.txt");
        repo.Delete("old.txt").AddFile(TempRepo.InitialFile, $"changed{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent deletes the tracked "$(rm -rf ~).txt" and edits the tracked readme.txt, and stages neither.
    // Behavior: The check lists both, with their kinds, but only readme.txt goes into the fix commands, and a note
    //           names the other to handle by hand: git add and git restore are both left without it.
    // Assertion: The snapshot has "deleted: $(rm -rf ~).txt" and "modified: readme.txt" in the entries, readme.txt
    //            alone in both commands, and "$(rm -rf ~).txt" alone in the note, with no kind.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_DeletedFileNamedAsShellCommandAndEditedFile_LeavesItOutOfCommands()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("$(rm -rf ~).txt").Stage("$(rm -rf ~).txt").Commit("Add the file");
        repo.Delete("$(rm -rf ~).txt").AddFile(TempRepo.InitialFile, $"changed{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent creates a file it never adds, stages a new file, and stages an edit to readme.txt.
    // Behavior: Untracked files are the untracked-files check's business, and staged changes are where finished work
    //           belongs: the working tree matches the index, so this check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_OnlyUntrackedOrStagedChanges_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("untracked.txt").AddFile("staged.txt").Stage("staged.txt");
        repo.AddFile(TempRepo.InitialFile, $"changed{Lf}").Stage(TempRepo.InitialFile);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUnstagedChanges), result);
    }

    // Trigger: An agent moves readme.txt to docs/readme.txt with a file operation, not with git mv.
    // Behavior: As in git status, the old path is listed as deleted and not paired with the new one as a rename; the
    //           new path is untracked, which is the untracked-files check's business.
    // Assertion: The snapshot lists readme.txt alone, as deleted.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_FileMovedWithoutGit_ListsOldPathAsDeleted()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("docs/readme.txt", $"initial{Lf}").Delete(TempRepo.InitialFile);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent edits readme.txt and stages the edit, then edits readme.txt again and does not stage that.
    //          git status shows MM.
    // Behavior: The check fails and names the file as modified: a commit would take the staged edit and leave the
    //           last one out. git restore brings back the staged version, so it drops only the last edit.
    // Assertion: The snapshot shows readme.txt as modified.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_StagedEditEditedAgain_FailsAsModified()
    {
        // Arrange
        // The two edits differ in length, so the second is a change whatever time the file system stamps on it.
        using TempRepo repo = TempRepo.Create();
        repo.AddFile(TempRepo.InitialFile, $"staged edit{Lf}").Stage(TempRepo.InitialFile);
        repo.AddFile(TempRepo.InitialFile, $"edited again after staging{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent creates new.txt and stages it, then edits new.txt and does not stage that. git status shows AM.
    // Behavior: The check fails and names the file as modified: the new file is staged, but a commit would take the
    //           first version and leave the last edit out.
    // Assertion: The snapshot shows new.txt as modified.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_NewFileStagedThenEdited_FailsAsModified()
    {
        // Arrange
        // The two versions differ in length, so the second is a change whatever time the file system stamps on it.
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("new.txt", $"staged{Lf}").Stage("new.txt");
        repo.AddFile("new.txt", $"edited after staging{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent creates new.txt and stages it, then deletes new.txt from disk. git status shows AD.
    // Behavior: The check fails and names the file as deleted: the index still holds a file the agent removed, and a
    //           commit would add it.
    // Assertion: The snapshot shows new.txt as deleted.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_NewFileStagedThenDeleted_FailsAsDeleted()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("new.txt").Stage("new.txt").Delete("new.txt");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent stages an empty notes.txt, then writes its content and does not stage that. The index holds the
    //          empty file and HEAD has no notes.txt: the state git add -N leaves, which libgit2 reads the same way.
    // Behavior: The check fails and names the file as new (intent to add), and offers git add alone: the index holds
    //           the file with no content, so git restore would empty it.
    // Assertion: The snapshot shows notes.txt in Files and a message with the Fix block and no discard block.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_EmptyFileStagedThenWritten_FailsAsIntentToAdd()
    {
        // Arrange
        // The content differs in size from the empty file, so the write is a change whatever time the file system
        // stamps on it. LibGit2Sharp cannot record git add -N, and a real one is the script Intent_to_add_file.
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("notes.txt", string.Empty).Stage("notes.txt");
        repo.AddFile("notes.txt", $"notes{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent edits the tracked readme.txt, and stages an empty notes.txt and then writes its content, so the
    //          working tree holds one edit and one file in the state git add -N leaves.
    // Behavior: The check fails and prints exactly the spec's intent-to-add example: git add takes both paths, and
    //           git restore only readme.txt, because restoring notes.txt would empty it.
    // Assertion: The snapshot's message is the spec example character for character, modified before new.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_EmptyFileStagedThenWrittenAndFileEdited_PrintsSpecExample()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("notes.txt", string.Empty).Stage("notes.txt");
        repo.AddFile("notes.txt", $"notes{Lf}").AddFile(TempRepo.InitialFile, $"changed{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: In a repository with no commits, an agent stages an empty notes.txt, then writes its content and does
    //          not stage that, as git add -N notes.txt does before a first commit.
    // Behavior: With no HEAD, no file is in HEAD, so the check fails and names the file as new (intent to add), with
    //           git add alone.
    // Assertion: The snapshot shows notes.txt in Files and a message with the Fix block and no discard block.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_EmptyFileStagedThenWrittenWithNoCommits_FailsAsIntentToAdd()
    {
        // Arrange
        using TempRepo repo = TempRepo.CreateUnborn();
        repo.AddFile("notes.txt", string.Empty).Stage("notes.txt");
        repo.AddFile("notes.txt", $"notes{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent commits an empty empty.txt, then writes content to it and does not stage that. The index holds
    //          the empty file, as it does for an intent-to-add file, but HEAD holds it too.
    // Behavior: The check fails and names the file as modified, and still offers git restore: it brings back the
    //           committed empty file, which drops the written content, as a person who asks for a discard means.
    // Assertion: The snapshot shows empty.txt as modified, with both the Fix and the discard block.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_EmptyCommittedFileWritten_FailsAsModified()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("empty.txt", string.Empty).Stage("empty.txt").Commit("Add empty.txt");
        repo.AddFile("empty.txt", $"written{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: Merging a feature branch stops on a conflict in readme.txt, and nobody resolves it, so the file on disk
    //          holds conflict markers.
    // Behavior: A conflicted file is the conflicts check's business, so this check passes and the file is not listed
    //           twice when both checks run; git restore would fail on it anyway, because the path is unmerged.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_UnresolvedConflict_IsHealthy()
    {
        // Arrange
        // Measured on 2026-11-20 with LibGit2Sharp 0.32.0: the status of a conflicted file reads Conflicted alone, with
        // no ModifiedInWorkdir, in the work-directory-only status this check reads.
        using TempRepo repo = TempRepo.Create();
        repo.CreateBranch("feature")
            .Checkout("feature")
            .AddFile(TempRepo.InitialFile, $"feature{Lf}")
            .Stage(TempRepo.InitialFile)
            .Commit("Edit on feature")
            .Checkout(TempRepo.MainBranch)
            .AddFile(TempRepo.InitialFile, $"main{Lf}")
            .Stage(TempRepo.InitialFile)
            .Commit("Edit on main")
            .Merge("feature");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUnstagedChanges), result);
    }

    // Trigger: On Windows with core.autocrlf=true, an agent opens a file Git checked out with CRLF line endings and
    //          saves it unchanged.
    // Behavior: Git compares the file with its CRLF turned back into LF, so it matches the committed file and the
    //           check passes, as in git status.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_CrlfFileSavedUnchangedWithAutoCrlf_IsHealthy()
    {
        // Arrange
        // With core.autocrlf=true, Git commits LF and checks files out with CRLF, so the committed blob holds LF and
        // the file on disk holds CRLF: https://git-scm.com/docs/git-config#Documentation/git-config.txt-coreautocrlf
        using TempRepo repo = TempRepo.Create();
        repo.Repo.Config.Set("core.autocrlf", true);
        repo.AddFile("windows.txt", $"line{CrLf}").Stage("windows.txt").Commit("Add windows.txt");

        // The save writes the same bytes at a new time, so Git reads the file again, and that read is where the line
        // endings are normalized. Without core.autocrlf the same bytes would differ from the LF blob.
        repo.AddFile("windows.txt", $"line{CrLf}");
        File.SetLastWriteTimeUtc(Path.Combine(repo.Root, "windows.txt"), SavedAt);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUnstagedChanges), result);
    }

    // Trigger: On Windows with core.autocrlf=true, an agent rewrites a file Git checked out with CRLF line endings,
    //          with LF line endings and no other change.
    // Behavior: As in git status, the file is listed as modified: the file's size no longer matches the size Git
    //           cached, and Git reports that without normalizing the line endings first. git add clears it and
    //           stages nothing, because the normalized content is the committed one.
    // Assertion: The snapshot shows windows.txt as modified.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_LineEndingsChangedWithAutoCrlf_FailsAsModified()
    {
        // Arrange
        // Measured on 2026-11-19 with Git for Windows 2.56.0: git status lists the file as modified, and git diff
        // prints no change, only the warning "LF will be replaced by CRLF the next time Git touches it".
        using TempRepo repo = TempRepo.Create();
        repo.Repo.Config.Set("core.autocrlf", true);
        repo.AddFile("windows.txt", $"line{CrLf}").Stage("windows.txt").Commit("Add windows.txt");
        repo.AddFile("windows.txt", $"line{Lf}");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A repository holds build.sh, committed as executable on Linux, and is checked out on Windows, where a
    //          file has no executable bit.
    // Behavior: The missing bit is not a change: Git for Windows sets core.filemode=false in every repository it
    //           creates or clones, so the check passes, as in git status.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoUnstagedChanges_FileModeChangeOnWindows_IsHealthy()
    {
        // Arrange
        // core.filemode=false tells Git to ignore the executable bit, and git init and git clone set it on a file
        // system that cannot hold the bit:
        // https://git-scm.com/docs/git-config#Documentation/git-config.txt-corefileMode
        // TempRepo's Repository.Init sets it on Windows too. It is set here as well, so the test means the same on a
        // file system that can hold the bit.
        using TempRepo repo = TempRepo.Create();
        repo.Repo.Config.Set("core.filemode", false);
        repo.AddFile("build.sh", $"echo build{Lf}").StageAsExecutable("build.sh").Commit("Add build.sh");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoUnstagedChanges();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoUnstagedChanges), result);
    }
}

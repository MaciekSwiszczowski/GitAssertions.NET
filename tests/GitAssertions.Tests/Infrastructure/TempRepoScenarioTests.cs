using LibGit2Sharp;

namespace GitAssertions.Tests.Infrastructure;

public sealed class TempRepoScenarioTests
{
    [Fact]
    public void AddFile_NewPath_IsNewInWorkdir()
    {
        using TempRepo repo = TempRepo.Create();

        repo.AddFile("new.txt");

        Assert.Equal(FileStatus.NewInWorkdir, repo.Repo.RetrieveStatus("new.txt"));
    }

    [Fact]
    public void AddFile_NestedPath_CreatesFolders()
    {
        using TempRepo repo = TempRepo.Create();

        repo.AddFile("src/deep/new.txt");

        Assert.Equal(FileStatus.NewInWorkdir, repo.Repo.RetrieveStatus("src/deep/new.txt"));
    }

    [Fact]
    public void AddFile_TrackedPath_IsModifiedInWorkdir()
    {
        using TempRepo repo = TempRepo.Create();

        repo.AddFile(TempRepo.InitialFile, "changed\n");

        Assert.Equal(FileStatus.ModifiedInWorkdir, repo.Repo.RetrieveStatus(TempRepo.InitialFile));
    }

    [Fact]
    public void Stage_NewFile_IsNewInIndex()
    {
        using TempRepo repo = TempRepo.Create();

        repo.AddFile("new.txt").Stage("new.txt");

        Assert.Equal(FileStatus.NewInIndex, repo.Repo.RetrieveStatus("new.txt"));
    }

    [Fact]
    public void Stage_DeletedFile_IsDeletedFromIndex()
    {
        using TempRepo repo = TempRepo.Create();

        repo.Delete(TempRepo.InitialFile).Stage(TempRepo.InitialFile);

        Assert.Equal(FileStatus.DeletedFromIndex, repo.Repo.RetrieveStatus(TempRepo.InitialFile));
    }

    [Fact]
    public void Commit_StagedFile_AddsCommitAndLeavesTreeClean()
    {
        using TempRepo repo = TempRepo.Create();

        repo.AddFile("new.txt").Stage("new.txt").Commit("Add new.txt");

        Assert.Equal("Add new.txt", repo.Repo.Head.Tip.MessageShort);
        Assert.Equal(2, repo.Repo.Commits.Count());
        Assert.Equal(FileStatus.Unaltered, repo.Repo.RetrieveStatus("new.txt"));
        Assert.False(repo.Repo.RetrieveStatus().IsDirty);
    }

    [Fact]
    public void Delete_TrackedFile_IsDeletedFromWorkdir()
    {
        using TempRepo repo = TempRepo.Create();

        repo.Delete(TempRepo.InitialFile);

        Assert.False(File.Exists(Path.Combine(repo.Root, TempRepo.InitialFile)));
        Assert.Equal(FileStatus.DeletedFromWorkdir, repo.Repo.RetrieveStatus(TempRepo.InitialFile));
    }

    [Fact]
    public void CreateBranch_PointsAtHeadAndStaysOnMain()
    {
        using TempRepo repo = TempRepo.Create();

        repo.CreateBranch("feature");

        Branch feature = repo.Repo.Branches["feature"];
        Assert.NotNull(feature);
        Assert.Equal(repo.Repo.Head.Tip, feature.Tip);
        Assert.Equal(TempRepo.MainBranch, repo.Repo.Head.FriendlyName);
        Assert.False(repo.Repo.RetrieveStatus().IsDirty);
    }

    [Fact]
    public void Checkout_Branch_MovesHead()
    {
        using TempRepo repo = TempRepo.Create();

        repo.CreateBranch("feature").Checkout("feature");

        Assert.Equal("feature", repo.Repo.Head.FriendlyName);
        Assert.False(repo.Repo.RetrieveStatus().IsDirty);
    }

    [Fact]
    public void Checkout_BackToMain_RemovesFileCommittedOnBranch()
    {
        using TempRepo repo = TempRepo.Create();

        repo.CreateBranch("feature")
            .Checkout("feature")
            .AddFile("feature.txt")
            .Stage("feature.txt")
            .Commit("Add feature.txt")
            .Checkout(TempRepo.MainBranch);

        Assert.Equal(TempRepo.MainBranch, repo.Repo.Head.FriendlyName);
        Assert.False(File.Exists(Path.Combine(repo.Root, "feature.txt")));
        Assert.Equal(FileStatus.Nonexistent, repo.Repo.RetrieveStatus("feature.txt"));
        Assert.False(repo.Repo.RetrieveStatus().IsDirty);
    }

    [Fact]
    public void AddGitIgnore_MatchingFile_IsIgnored()
    {
        using TempRepo repo = TempRepo.Create();

        repo.AddGitIgnore("*.log").AddFile("build.log");

        Assert.Equal(FileStatus.Ignored, repo.Repo.RetrieveStatus("build.log"));
        Assert.Equal(FileStatus.NewInWorkdir, repo.Repo.RetrieveStatus(".gitignore"));
    }

    [Fact]
    public void AddGitIgnore_CalledTwice_KeepsBothRules()
    {
        using TempRepo repo = TempRepo.Create();

        repo.AddGitIgnore("*.log")
            .AddGitIgnore("tmp/")
            .AddFile("build.log")
            .AddFile("tmp/notes.txt");

        Assert.Equal(FileStatus.Ignored, repo.Repo.RetrieveStatus("build.log"));
        Assert.Equal(FileStatus.Ignored, repo.Repo.RetrieveStatus("tmp/notes.txt"));
    }
}

using LibGit2Sharp;

namespace GitAssertions.Tests.Infrastructure;

public sealed class TempRepoTests
{
    [Fact]
    public void Create_FolderExistsAndIsRepository()
    {
        using TempRepo repo = TempRepo.Create();

        Assert.True(Directory.Exists(repo.Root));
        Assert.True(Repository.IsValid(repo.Root));
    }

    [Fact]
    public void Create_FolderIsUnderTempPath()
    {
        using TempRepo repo = TempRepo.Create();

        Assert.StartsWith(Path.GetTempPath(), repo.Root, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_EachRepoGetsItsOwnFolder()
    {
        using TempRepo first = TempRepo.Create();
        using TempRepo second = TempRepo.Create();

        Assert.NotEqual(first.Root, second.Root);
    }

    [Fact]
    public void Create_MakesOneInitialCommitOnMain()
    {
        using TempRepo repo = TempRepo.Create();

        Commit commit = Assert.Single(repo.Repo.Commits);
        Assert.NotNull(commit.Tree[TempRepo.InitialFile]);
        Assert.Equal(TempRepo.MainBranch, repo.Repo.Head.FriendlyName);
    }

    [Fact]
    public void Create_WorkingTreeIsClean()
    {
        using TempRepo repo = TempRepo.Create();

        Assert.False(repo.Repo.RetrieveStatus().IsDirty);
    }

    [Fact]
    public void Create_ReadsNoMachineLevelGitConfig()
    {
        using TempRepo repo = TempRepo.Create();

        Assert.All(repo.Repo.Config, entry => Assert.Equal(ConfigurationLevel.Local, entry.Level));
    }

    [Fact]
    public void Dispose_DeletesFolder()
    {
        TempRepo repo = TempRepo.Create();
        string root = repo.Root;

        repo.Dispose();

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        TempRepo repo = TempRepo.Create();
        repo.Dispose();

        Exception? exception = Record.Exception(repo.Dispose);

        Assert.Null(exception);
    }
}

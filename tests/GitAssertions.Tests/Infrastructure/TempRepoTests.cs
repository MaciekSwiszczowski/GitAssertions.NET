using LibGit2Sharp;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Pins what a test author gets from <see cref="TempRepo.Create"/> and <see cref="TempRepo.Dispose"/>: where the
/// repository lives, what it starts with, that nothing from the machine's own Git setup leaks in, and that nothing is
/// left behind afterwards. Every other test builds its scenario on these guarantees.
/// </summary>
public sealed class TempRepoTests
{
    // Trigger: A test author creates a TempRepo.
    // Behavior: The author gets a real Git repository in a folder on disk.
    // Assertion: The folder exists and libgit2 accepts it as a valid repository.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Create_FolderExistsAndIsRepository()
    {
        // Act
        using TempRepo repo = TempRepo.Create();

        // Assert
        Directory.Exists(repo.Root).ShouldBeTrue();
        Repository.IsValid(repo.Root).ShouldBeTrue();
    }

    // Trigger: A test author creates a TempRepo.
    // Behavior: The repository lives under the machine's temp folder, never inside the source tree.
    // Assertion: The repository root starts with the temp path.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Create_FolderIsUnderTempPath()
    {
        // Act
        using TempRepo repo = TempRepo.Create();

        // Assert
        repo.Root.ShouldStartWith(Path.GetTempPath(), Case.Insensitive);
    }

    // Trigger: A test author creates two TempRepos in one test, for example to compare two repositories.
    // Behavior: The two repositories never share a folder.
    // Assertion: The two roots differ.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Create_EachRepoGetsItsOwnFolder()
    {
        // Act
        using TempRepo first = TempRepo.Create();
        using TempRepo second = TempRepo.Create();

        // Assert
        second.Root.ShouldNotBe(first.Root);
    }

    // Trigger: A test author creates a TempRepo and has not committed anything yet.
    // Behavior: The repository starts with one commit holding readme.txt, on the branch main.
    // Assertion: History reads one commit, "Initial commit", whose tree holds only readme.txt, and HEAD reads main.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Create_MakesOneInitialCommitOnMain()
    {
        // Act
        using TempRepo repo = TempRepo.Create();

        // Assert
        repo.Repo.Commits.Select(static commit => commit.MessageShort).ShouldBe(["Initial commit"]);
        repo.Repo.Head.Tip.Tree.Select(static entry => entry.Path).ShouldBe(["readme.txt"]);
        repo.Repo.Head.FriendlyName.ShouldBe("main");
    }

    // Trigger: A test author needs a repository in which nothing has been committed yet, as right after git init.
    // Behavior: The repository has no commits, and HEAD names main, a branch that does not exist until the first
    //           commit.
    // Assertion: HEAD reads unborn and points at refs/heads/main, and history is empty.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void CreateUnborn_HasNoCommitsAndHeadNamesMain()
    {
        // Act
        using TempRepo repo = TempRepo.CreateUnborn();

        // Assert
        repo.Repo.Info.IsHeadUnborn.ShouldBeTrue();
        repo.Repo.Refs.Head.TargetIdentifier.ShouldBe("refs/heads/main");
        repo.Repo.Commits.ShouldBeEmpty();
    }

    // Trigger: A test author creates a TempRepo and touches nothing.
    // Behavior: The working tree is clean, so whatever a test changes next is the only change in the status.
    // Assertion: The status lists no file at all.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Create_WorkingTreeIsClean()
    {
        // Act
        using TempRepo repo = TempRepo.Create();

        // Assert
        repo.Repo.RetrieveStatus().Select(static entry => entry.FilePath).ShouldBeEmpty();
    }

    // Trigger: A test author creates a TempRepo on a machine with its own Git config, such as core.autocrlf.
    // Behavior: Only the repository's own config applies, so a test gives the same result on CI and on a desk.
    // Assertion: Every config entry the repository can see comes from the Local level.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Create_ReadsNoMachineLevelGitConfig()
    {
        // Act
        using TempRepo repo = TempRepo.Create();

        // Assert
        repo.Repo.Config.Select(static entry => entry.Level).Distinct().ShouldBe([ConfigurationLevel.Local]);
    }

    // Trigger: A test author's test ends and its TempRepo is disposed.
    // Behavior: Nothing is left behind under the temp folder, .git included.
    // Assertion: The folder the repository lived in no longer exists.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Dispose_DeletesFolder()
    {
        // Arrange
        TempRepo repo = TempRepo.Create();
        string root = repo.Root;

        // Act
        repo.Dispose();

        // Assert
        Directory.Exists(root).ShouldBeFalse();
    }

    // Trigger: A test disposes its TempRepo by hand, and its using declaration disposes it again at the end.
    // Behavior: The second Dispose does nothing and raises nothing.
    // Assertion: Disposing a second time throws no exception.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        TempRepo repo = TempRepo.Create();
        repo.Dispose();

        // Assert
        Should.NotThrow(repo.Dispose);
    }
}

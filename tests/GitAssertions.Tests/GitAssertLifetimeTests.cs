using GitAssertions.Tests.Infrastructure;

using LibGit2Sharp;

using Meziantou.Framework;

namespace GitAssertions.Tests;

/// <summary>
/// Pins what <see cref="GitAssert.Open"/> accepts, what it says when the path is no repository or a bare one, that an
/// instance can be shared, because it holds a path and no handle, and that a LibGit2Sharp error reaches the caller as
/// a .NET exception. Every check is built on an instance opened this way.
/// </summary>
public sealed class GitAssertLifetimeTests
{
    /// <summary>
    /// How many parallel tests share one instance in the shared-instance case: enough to overlap, few enough to stay
    /// quick.
    /// </summary>
    private const int ParallelReaderCount = 32;

    // Trigger: A test author passes GitAssert the root folder of a repository that has a commit of its own.
    // Behavior: GitAssert reads that repository, not another one, with its branch and history as they are on disk.
    // Assertion: HEAD reads main, and history reads "Add only-here.txt", then "Initial commit".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_RepositoryRoot_ReadsThatRepository()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("only-here.txt").Stage("only-here.txt").Commit("Add only-here.txt");

        // Act
        GitAssert git = GitAssert.Open(repo.Root);

        // Assert
        // The history identifies the repository. The working folder cannot: libgit2 reports the long form of a path
        // given in its 8.3 short form (RUNNER~1), so it would differ from the path passed in on a CI runner.
        git.Read(static repository => repository.Head.FriendlyName).ShouldBe("main");
        git.Read(static repository => repository.Commits.Select(static commit => commit.MessageShort).ToArray())
            .ShouldBe(["Add only-here.txt", "Initial commit"]);
    }

    // Trigger: A test author passes GitAssert a folder that exists but holds no repository.
    // Behavior: Open refuses, and the error names the folder and says it is no Git repository.
    // Assertion: An ArgumentException for repoPath whose message holds the folder path and "is not a Git repository".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_FolderWithoutRepository_ThrowsArgumentExceptionNamingTheFolder()
    {
        // Arrange
        using TemporaryDirectory folder = TemporaryDirectory.Create();

        // Assert
        ShouldRefuseAsNotARepository(folder.FullPath);
    }

    // Trigger: A test author passes GitAssert a path where nothing exists, for example a typo in a folder name.
    // Behavior: Open refuses the same way it refuses an empty folder, and the error names the path.
    // Assertion: An ArgumentException for repoPath whose message holds the path and "is not a Git repository".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_PathThatDoesNotExist_ThrowsArgumentExceptionNamingThePath()
    {
        // Arrange
        using TemporaryDirectory folder = TemporaryDirectory.Create();

        // Assert
        ShouldRefuseAsNotARepository(Path.Combine(folder.FullPath, "no-such-folder"));
    }

    // Trigger: A test author passes GitAssert a folder inside a repository, such as src, not the repository root.
    // Behavior: Open refuses; it never searches the parent folders, so it cannot end up reading some other repository.
    // Assertion: An ArgumentException for repoPath whose message holds the subfolder and "is not a Git repository".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_SubfolderOfRepository_ThrowsArgumentExceptionNamingTheSubfolder()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("src/new.txt");

        // Assert
        ShouldRefuseAsNotARepository(Path.Combine(repo.Root, "src"));
    }

    // Trigger: A test author passes GitAssert a bare repository, such as a folder a server pushes to, which has history
    //          but no checked-out files.
    // Behavior: Open refuses, because every check reads the working tree, and the error says the repository is bare.
    // Assertion: An ArgumentException for repoPath whose message holds the folder path and "is a bare repository".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_BareRepository_ThrowsArgumentExceptionNamingTheFolder()
    {
        // Arrange
        using TemporaryDirectory folder = TemporaryDirectory.Create();
        Repository.Init(folder.FullPath, isBare: true);

        // Assert
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(folder.FullPath));
        exception.ParamName.ShouldBe("repoPath");
        exception.Message.ShouldContain(folder.FullPath);
        exception.Message.ShouldContain("is a bare repository");
    }

    // Trigger: A test deletes the repository folder after opening it, then runs a check on the stored instance.
    // Behavior: The check fails with a .NET exception that carries LibGit2Sharp's message unchanged, and no
    //           LibGit2Sharp type reaches the caller, not even as the inner exception.
    // Assertion: Exactly an InvalidOperationException, with no inner exception, whose message equals the one
    //            LibGit2Sharp gives for opening that folder.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Check_RepositoryDeletedAfterOpen_ThrowsInvalidOperationExceptionWithLibGit2SharpMessage()
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
        ShouldRethrowWithMessageOnly(() => git.NoUntrackedFiles(), libGit2SharpMessage);
    }

    // Trigger: A test author opens a repository whose .git/config a tool left broken, for example half written.
    // Behavior: Open fails with a .NET exception that carries LibGit2Sharp's message unchanged, not with a LibGit2Sharp
    //           type.
    // Assertion: Exactly an InvalidOperationException, with no inner exception, whose message equals the one
    //            LibGit2Sharp gives for that folder.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_RepositoryWithBrokenConfig_ThrowsInvalidOperationExceptionWithLibGit2SharpMessage()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        File.WriteAllText(Path.Combine(repo.Root, ".git", "config"), $"[core{Lf}");
        string libGit2SharpMessage = Should.Throw<LibGit2SharpException>(() => Repository.IsValid(repo.Root)).Message;

        // Assert
        ShouldRethrowWithMessageOnly(() => GitAssert.Open(repo.Root), libGit2SharpMessage);
    }

    // Trigger: A test author passes a path that never got a value, such as an unset variable or a blank string.
    // Behavior: Open refuses before it looks at the disk, and the error points at the repoPath argument.
    // Assertion: An ArgumentException (a null path gives its subclass ArgumentNullException) for repoPath.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_NullOrBlankPath_ThrowsArgumentException(string? path)
    {
        // Assert
        // The null-forgiving operator is the point of the test: a caller who ignores nullable warnings passes null.
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(path!));
        exception.ParamName.ShouldBe("repoPath");
    }

    // Trigger: A test author stores one GitAssert in a static field, and tests running in parallel all read through it.
    // Behavior: Each read opens its own handle, so the readers never interfere and none of them fails or crashes.
    // Assertion: Every one of the parallel readers counts one commit.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Read_OneInstanceSharedByParallelTests_EachReadsItIntact()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        GitAssert shared = GitAssert.Open(repo.Root);
        int[] commitCounts = new int[ParallelReaderCount];

        // Act
        Parallel.For(0, ParallelReaderCount, index =>
            commitCounts[index] = shared.Read(static repository => repository.Commits.Count()));

        // Assert
        commitCounts.ShouldBe(Enumerable.Repeat(1, ParallelReaderCount));
    }

    /// <summary>
    /// The one error every path that is no repository root must give. It is a helper because the three tests that
    /// differ only in what exists on disk assert the same message, and a drift between them would be a bug.
    /// </summary>
    private static void ShouldRefuseAsNotARepository(string path)
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(path));
        exception.ParamName.ShouldBe("repoPath");
        exception.Message.ShouldContain(path);
        exception.Message.ShouldContain("is not a Git repository");
    }

    /// <summary>
    /// What every LibGit2Sharp failure must look like to the caller: exactly an <see cref="InvalidOperationException"/>
    /// with LibGit2Sharp's message and no inner exception, so no LibGit2Sharp type is reachable. The expected message
    /// is read from LibGit2Sharp itself, because the contract is that it passes through unchanged.
    /// </summary>
    private static void ShouldRethrowWithMessageOnly(Func<object> call, string libGit2SharpMessage)
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(call);
        exception.GetType().ShouldBe(typeof(InvalidOperationException));
        exception.InnerException.ShouldBeNull();
        exception.Message.ShouldBe(libGit2SharpMessage);
    }
}

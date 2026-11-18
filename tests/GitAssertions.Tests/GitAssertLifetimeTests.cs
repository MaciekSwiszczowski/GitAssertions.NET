using GitAssertions.Tests.Infrastructure;

using Meziantou.Framework;

namespace GitAssertions.Tests;

/// <summary>
/// Pins what <see cref="GitAssert.Open"/> accepts, what it says when the path is no repository, and that an instance
/// can be shared, because it holds a path and no handle. Every check is built on an instance opened this way.
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
}

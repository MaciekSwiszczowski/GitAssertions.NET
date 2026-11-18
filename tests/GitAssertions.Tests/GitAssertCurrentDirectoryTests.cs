using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.Tests;

/// <summary>
/// Pins that a <see cref="GitAssert"/> opened with a relative path keeps reading the repository that path named when
/// it was opened. The tests change the process's current directory, so the class joins
/// <see cref="NonParallelCollectionForCurrentDirectoryTests"/> to run alone, and puts the directory back in
/// <see cref="Dispose"/>.
/// </summary>
[Collection(NonParallelCollectionForCurrentDirectoryTests.Name)]
public sealed class GitAssertCurrentDirectoryTests : IDisposable
{
    private readonly string _originalDirectory = Environment.CurrentDirectory;

    // Trigger: A test author opens GitAssert with a relative path, and a later step changes the current directory.
    // Behavior: GitAssert keeps reading the repository the path named at Open, not whatever it names now.
    // Assertion: After the change, history still reads "Add only-here.txt", then "Initial commit".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Read_AfterCurrentDirectoryChanges_ReadsRepositoryNamedAtOpen()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("only-here.txt").Stage("only-here.txt").Commit("Add only-here.txt");
        Environment.CurrentDirectory = Path.GetDirectoryName(repo.Root)!;
        GitAssert git = GitAssert.Open(Path.GetFileName(repo.Root));

        // Act
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        // Assert
        git.Read(static repository => repository.Commits.Select(static commit => commit.MessageShort).ToArray())
            .ShouldBe(["Add only-here.txt", "Initial commit"]);
    }

    /// <summary>Puts the current directory back, whatever the test did, so no later test inherits it.</summary>
    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
    }
}

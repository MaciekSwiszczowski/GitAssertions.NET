using LibGit2Sharp;
using Meziantou.Framework;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// A throwaway Git repository in its own folder under the temp path, holding one commit on <see cref="MainBranch"/>.
/// The scenario helpers return the instance so a test can chain them. Dispose releases the repository handle and
/// deletes the folder.
/// </summary>
internal sealed class TempRepo : IDisposable
{
    public const string MainBranch = "main";

    public const string InitialFile = "readme.txt";

    private static readonly DateTimeOffset FixtureEpoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory _directory;

    private int _commitCount;

    private TempRepo(TemporaryDirectory directory, Repository repo)
    {
        _directory = directory;
        Repo = repo;
    }

    public string Root => _directory;

    public Repository Repo { get; }

    /// <summary>
    /// Creates the folder, the repository and its initial commit. If a step throws, whatever was already made is
    /// released and deleted before the exception goes on, so a failed setup leaves nothing under the temp path.
    /// </summary>
    public static TempRepo Create()
    {
        TemporaryDirectory directory = TemporaryDirectory.Create();
        Repository? repository = null;
        try
        {
            string root = directory;
            Repository.Init(root);
            repository = new Repository(root);

            // The first branch is named after init.defaultBranch, which differs from machine to machine.
            repository.Refs.UpdateTarget("HEAD", "refs/heads/" + MainBranch);

            return new TempRepo(directory, repository)
                .AddFile(InitialFile, "initial\n")
                .Stage(InitialFile)
                .Commit("Initial commit");
        }
        catch
        {
            // The handle first, because an open handle can lock files the delete needs.
            repository?.Dispose();
            directory.Dispose();
            throw;
        }
    }

    /// <summary>Writes a file to the working tree, creating its folders, and overwrites a file that exists.</summary>
    public TempRepo AddFile(string path, string content = "content\n")
    {
        _directory.CreateTextFile(path, content);
        return this;
    }

    /// <summary>Stages the paths, deleted ones included, like <c>git add</c>.</summary>
    public TempRepo Stage(params string[] paths)
    {
        Commands.Stage(Repo, paths);
        return this;
    }

    /// <summary>
    /// Commits the index. Each commit is stamped from a fixed UTC base, one minute after the one before, as author
    /// and committer alike, so a scenario builds the same history on any machine in any month.
    /// </summary>
    public TempRepo Commit(string message = "Commit")
    {
        DateTimeOffset when = FixtureEpoch.AddMinutes(_commitCount++);
        Signature signature = new("GitAssertions Tests", "tests@gitassertions.invalid", when);
        Repo.Commit(message, signature, signature);
        return this;
    }

    /// <summary>Deletes a file from the working tree only; the index is left alone.</summary>
    public TempRepo Delete(string path)
    {
        File.Delete(_directory.GetFullPath(path));
        return this;
    }

    /// <summary>Creates a branch at <c>HEAD</c> without checking it out.</summary>
    public TempRepo CreateBranch(string name)
    {
        Repo.CreateBranch(name);
        return this;
    }

    public TempRepo Checkout(string branch)
    {
        Commands.Checkout(Repo, branch);
        return this;
    }

    /// <summary>Appends the patterns to the root .gitignore, creating it if needed; the file is not staged.</summary>
    public TempRepo AddGitIgnore(params string[] patterns)
    {
        File.AppendAllText(_directory.GetFullPath(".gitignore"),
            string.Concat(patterns.Select(static pattern => pattern + Environment.NewLine)));
        return this;
    }

    /// <summary>Releases the repository handle first, because an open handle can lock files under .git.</summary>
    public void Dispose()
    {
        Repo.Dispose();
        _directory.Dispose();
    }
}

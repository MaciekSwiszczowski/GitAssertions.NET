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

    private readonly TemporaryDirectory _directory;

    static TempRepo()
    {
        // libgit2 reads the machine's git config and its global ignore file, for example Git for Windows'
        // core.autocrlf=true or a user's *.log rule, so the same test could pass on CI and fail on a desk. An empty
        // search path turns those levels off. The setting is process-wide, which is the point: the library under
        // test runs in this process and sees the same clean slate.
        foreach (ConfigurationLevel level in new[]
        {
            ConfigurationLevel.ProgramData,
            ConfigurationLevel.System,
            ConfigurationLevel.Xdg,
            ConfigurationLevel.Global,
        })
        {
            GlobalSettings.SetConfigSearchPaths(level, []);
        }
    }

    private TempRepo(TemporaryDirectory directory, Repository repo)
    {
        _directory = directory;
        Repo = repo;
    }

    public string Root => _directory;

    public Repository Repo { get; }

    public static TempRepo Create()
    {
        TemporaryDirectory directory = TemporaryDirectory.Create();
        string root = directory;
        Repository.Init(root);
        TempRepo tempRepo = new(directory, new Repository(root));

        // The first branch is named after init.defaultBranch, which differs from machine to machine.
        tempRepo.Repo.Refs.UpdateTarget("HEAD", "refs/heads/" + MainBranch);

        return tempRepo
            .AddFile(InitialFile, "initial\n")
            .Stage(InitialFile)
            .Commit("Initial commit");
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

    public TempRepo Commit(string message = "Commit")
    {
        Signature signature = new("GitAssertions Tests", "tests@gitassertions.invalid", DateTimeOffset.Now);
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

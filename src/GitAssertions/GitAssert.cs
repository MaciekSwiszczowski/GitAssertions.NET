using System.Diagnostics.CodeAnalysis;

using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// A read-only view of one Git repository, opened with <see cref="Open"/>.
/// </summary>
/// <remarks>
/// An instance holds only the repository's path, never an open handle: every read opens the repository, reads it,
/// and closes it again before returning. So there is nothing to dispose, an instance may be stored and shared between
/// tests that run in parallel, and the repository folder can be deleted as soon as a read has returned.
/// <para>
/// When reading the repository fails, for example because its folder was deleted after <see cref="Open"/>, the error
/// is an <see cref="InvalidOperationException"/> that carries the message of the underlying Git library and nothing
/// else, so no type of that library reaches the caller.
/// </para>
/// </remarks>
public sealed class GitAssert
{
    private readonly string _repoPath;

    private GitAssert(string repoPath)
    {
        _repoPath = repoPath;
    }

    /// <summary>
    /// Checks that <paramref name="repoPath"/> is the root folder of a Git repository and keeps its full path. A
    /// relative path is resolved against the current directory once, here, so a later change of the current directory
    /// cannot point a check at a different folder.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="repoPath"/> is null or blank, is not the root folder of a Git repository, or is a bare
    /// repository, which has no working tree for the checks to read.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The repository cannot be read, for example because its configuration is broken.
    /// </exception>
    public static GitAssert Open(string repoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoPath);
        string fullPath = Path.GetFullPath(repoPath);

        if (!WithoutLibGit2SharpExceptions(() => Repository.IsValid(fullPath)))
        {
            throw new ArgumentException(
                $"\"{repoPath}\" is not a Git repository. Pass the folder that contains .git.",
                nameof(repoPath));
        }

        GitAssert git = new(fullPath);

        // Every check reads the working tree. Without this, a bare repository would pass here and fail every check.
        if (git.Read(static repository => repository.Info.IsBare))
        {
            throw new ArgumentException(
                $"\"{repoPath}\" is a bare repository: it has no working tree for the checks to read. Pass the "
                + "folder of a clone that has one.",
                nameof(repoPath));
        }

        return git;
    }

    /// <summary>
    /// Checks that the working tree holds no file Git neither tracks nor ignores, such as a file an agent created and
    /// never added. Each file is listed by its full path, also inside a new folder. Ignored files and empty folders
    /// pass, as they do in <c>git status</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The repository cannot be read, for example because its folder was deleted after <see cref="Open"/>.
    /// </exception>
    public CheckResult NoUntrackedFiles()
    {
        return Read(NoUntrackedFilesCheck.Run);
    }

    /// <summary>
    /// Checks that no tracked file differs from the index: every edit and every delete is staged. A file moved without
    /// <c>git mv</c> reads as its old path deleted, and its new path is left to <see cref="NoUntrackedFiles"/>, as in
    /// <c>git status</c>. Line endings and the executable bit follow the <c>core.autocrlf</c> and <c>core.filemode</c>
    /// settings, as in <c>git status</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The repository cannot be read, for example because its folder was deleted after <see cref="Open"/>.
    /// </exception>
    public CheckResult NoUnstagedChanges()
    {
        return Read(NoUnstagedChangesCheck.Run);
    }

    /// <summary>
    /// Checks that no file has an unresolved conflict: a merge, pull, rebase, cherry-pick, revert, or stash pop that
    /// stopped on a conflict leaves the file's conflicting versions in the index until it is resolved and staged with
    /// <c>git add</c>, and until then Git refuses to commit. A file staged with conflict markers still in it passes,
    /// because Git no longer counts it as conflicted.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The repository cannot be read, for example because its folder was deleted after <see cref="Open"/>.
    /// </exception>
    public CheckResult NoConflicts()
    {
        return Read(NoConflictsCheck.Run);
    }

    /// <summary>
    /// Checks that no merge, rebase, cherry-pick, or revert is left stopped halfway: such an operation stops on a
    /// conflict and stays in progress until <c>git &lt;operation&gt; --continue</c> finishes it or <c>--abort</c> gives
    /// it up. The failure names the operation and the two commands; it lists no file, because the operation belongs to
    /// the repository, not to a path. A bisect left open fails too, and <c>git bisect reset</c> ends it. Any other
    /// operation libgit2 reports, such as <c>git am</c>, fails with a generic message that names it as libgit2 does and
    /// points to <c>git status</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The repository cannot be read, for example because its folder was deleted after <see cref="Open"/>.
    /// </exception>
    public CheckResult NoOperationInProgress()
    {
        return Read(NoOperationInProgressCheck.Run);
    }

    /// <summary>
    /// Checks that no file Git tracks matches an ignore rule. A rule hides only files Git does not track, so a file
    /// committed before its rule was added, or forced in with <c>git add -f</c>, stays tracked and keeps showing up in
    /// every diff. The rules are those <c>git ls-files -i -c --exclude-standard</c> reads: the repository's
    /// <c>.gitignore</c> files, <c>.git/info/exclude</c>, and the file <c>core.excludesFile</c> names. A file that a
    /// <c>!</c> rule brings back passes. A folder that is itself ignored, with every tracked file in it ignored, such as
    /// <c>bin/</c>, is listed as one entry with its file count; any other file is listed on its own.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The repository cannot be read, for example because its folder was deleted after <see cref="Open"/>.
    /// </exception>
    public CheckResult NoTrackedIgnoredFiles()
    {
        return Read(NoTrackedIgnoredFilesCheck.Run);
    }

    /// <summary>
    /// Runs every check, one after another and in a fixed order, each exactly as if it were called on its own, and
    /// reports all of them at once: a failing check does not stop the checks after it, so one run names every broken
    /// rule.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A check cannot read the repository, for example because its folder was deleted after <see cref="Open"/>. The
    /// exception is that check's own, and the checks after it do not run: a repository one check cannot read usually
    /// fails the others too, so going on would only repeat the error, after each had spent its own time failing.
    /// </exception>
    public RunAllResult RunAll()
    {
        // A collection expression evaluates its elements in order, so a check that throws ends the run here and the
        // checks after it never start.
        return RunAllResult.Create(
        [
            NoUntrackedFiles(),
            NoUnstagedChanges(),
            NoConflicts(),
            NoOperationInProgress(),
            NoTrackedIgnoredFiles(),
        ]);
    }

    /// <summary>
    /// Opens the repository, runs <paramref name="read"/> on it, and closes it before returning. Every check reads
    /// through here, so no handle outlives a call. The repository is closed once <paramref name="read"/> returns, so
    /// it must return finished data (a list, a count), never a lazy query over the repository.
    /// </summary>
    internal T Read<T>(Func<Repository, T> read)
    {
        return WithoutLibGit2SharpExceptions(() =>
        {
            using Repository repository = new(_repoPath);
            return read(repository);
        });
    }

    /// <summary>
    /// Runs <paramref name="call"/> and re-throws any LibGit2Sharp exception as an
    /// <see cref="InvalidOperationException"/> that carries only its message. LibGit2Sharp is an implementation detail:
    /// its exception types must not reach the caller, not even as an inner exception, which is why the original is
    /// not attached. Every LibGit2Sharp call goes through here.
    /// </summary>
    [SuppressMessage(
        "Design",
        "MA0054:Embed the caught exception as innerException",
        Justification = "The inner exception would hand a LibGit2Sharp type to the caller; only its message is kept.")]
    private static T WithoutLibGit2SharpExceptions<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (LibGit2SharpException exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }
}

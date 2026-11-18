using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// A read-only view of one Git repository, opened with <see cref="Open"/>.
/// </summary>
/// <remarks>
/// An instance holds only the repository's path, never an open handle: every read opens the repository, reads it,
/// and closes it again before returning. So there is nothing to dispose, an instance may be stored and shared between
/// tests that run in parallel, and the repository folder can be deleted as soon as a read has returned.
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
    /// <paramref name="repoPath"/> is null or blank, or is not the root folder of a Git repository.
    /// </exception>
    public static GitAssert Open(string repoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoPath);
        string fullPath = Path.GetFullPath(repoPath);

        if (!Repository.IsValid(fullPath))
        {
            throw new ArgumentException(
                $"\"{repoPath}\" is not a Git repository. Pass the folder that contains .git.",
                nameof(repoPath));
        }

        return new GitAssert(fullPath);
    }

    /// <summary>
    /// Opens the repository, runs <paramref name="read"/> on it, and closes it before returning. Every check reads
    /// through here, so no handle outlives a call. The repository is closed once <paramref name="read"/> returns, so
    /// it must return finished data (a list, a count), never a lazy query over the repository.
    /// </summary>
    internal T Read<T>(Func<Repository, T> read)
    {
        using Repository repository = new(_repoPath);
        return read(repository);
    }
}

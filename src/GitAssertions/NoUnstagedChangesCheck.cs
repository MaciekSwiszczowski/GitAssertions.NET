using System.Globalization;

using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// The NoUnstagedChanges check (docs/specification.md, Checks): fails for every tracked file whose working-tree copy
/// differs from the index, edited or deleted. It reads an open repository, so <see cref="GitAssert"/> can run it on a
/// repository it opens for this check alone, and a later RunAll on one it opens once for every check.
/// </summary>
internal static class NoUnstagedChangesCheck
{
    private const string Rule = "NoUnstagedChanges";

    private static readonly IReadOnlyList<FixStep> Fixes =
    [
        FixStep.WithPaths("Fix:", "git add"),
        FixStep.WithPaths("Or discard the changes:", "git restore"),
    ];

    public static CheckResult Run(Repository repository)
    {
        FailureEntry[] entries =
        [
            .. repository.RetrieveStatus(CreateStatusOptions())
                .Select(ToFailureEntry)
                .OfType<FailureEntry>(),
        ];

        if (entries.Length == 0)
        {
            return CheckResult.Healthy(Rule);
        }

        string summary = entries.Length == 1
            ? "1 file has unstaged changes."
            : string.Create(CultureInfo.InvariantCulture, $"{entries.Length} files have unstaged changes.");

        return CheckResult.Failing(Rule, summary, entries, Fixes, [.. entries.SelectMany(static entry => entry.Paths)]);
    }

    /// <summary>
    /// The entry for an edit or a delete, and null for anything else this status reports, such as a conflict, which
    /// is the conflicts check's business.
    /// </summary>
    private static FailureEntry? ToFailureEntry(StatusEntry entry)
    {
        FileStatus state = entry.State;
        if (state.HasFlag(FileStatus.ModifiedInWorkdir))
        {
            return FailureEntry.Change("modified", entry.FilePath);
        }

        return state.HasFlag(FileStatus.DeletedFromWorkdir) ? FailureEntry.Change("deleted", entry.FilePath) : null;
    }

    /// <summary>
    /// A new instance per read: <see cref="StatusOptions"/> is mutable, and checks run in parallel.
    /// </summary>
    private static StatusOptions CreateStatusOptions()
    {
        return new StatusOptions
        {
            // Unstaged changes sit between the index and the working tree; HEAD against the index is not needed.
            Show = StatusShowOption.WorkDirOnly,

            // Untracked files are the untracked-files check's business. Off, libgit2 does not walk new folders at all.
            IncludeUntracked = false,
            IncludeIgnored = false,

            // git status lists a file moved without git mv as a deleted path plus an untracked one. With rename
            // detection on, libgit2 would pair the two, and the old path would no longer read as deleted.
            DetectRenamesInWorkDir = false,
        };
    }
}

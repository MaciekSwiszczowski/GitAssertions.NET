using System.Globalization;

using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// The NoUnstagedChanges check (docs/specification.md, Checks): fails for every tracked file whose working-tree copy
/// differs from the index, edited or deleted. A file recorded with <c>git add -N</c> (intent to add) is listed as a
/// kind of its own, and <c>git restore</c> is not offered for it. It reads an open repository:
/// <see cref="GitAssert"/> opens one for this check alone, also when <see cref="GitAssert.RunAll"/> calls it, and
/// closes it when the check returns.
/// </summary>
internal static class NoUnstagedChangesCheck
{
    private const string Rule = "NoUnstagedChanges";

    /// <summary>
    /// The kind of a file recorded with <c>git add -N</c>, which Git holds with no content: <c>git status</c> prints it
    /// as <c> A</c> and lists it as <c>new file</c>.
    /// </summary>
    private const string IntentToAddKind = "new (intent to add)";

    /// <summary>
    /// The id of a blob with no content, which is what the index holds for an intent-to-add file. Git defines it, so
    /// a read of the object database is not needed, and a large changed file is never loaded to learn its size.
    /// </summary>
    private static readonly ObjectId EmptyBlobId = new("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391");

    private static readonly IReadOnlyList<FixStep> Fixes =
    [
        FixStep.WithPaths("Fix:", "git add"),

        // The index holds an intent-to-add file with no content, so git restore would empty it, and the file would
        // stay unstaged: the advice would destroy the work and the check would keep failing.
        FixStep.WithPaths(
            "Or discard the changes:",
            "git restore",
            static entry => !string.Equals(entry.Kind, IntentToAddKind, StringComparison.Ordinal)),
    ];

    public static CheckResult Run(Repository repository)
    {
        FailureEntry[] entries =
        [
            .. repository.RetrieveStatus(CreateStatusOptions())
                .Select(entry => ToFailureEntry(repository, entry))
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
    /// The entry for an edit, a delete, or an intent-to-add file, and null for anything else this status reports, such
    /// as a conflict, which is the conflicts check's business.
    /// </summary>
    private static FailureEntry? ToFailureEntry(Repository repository, StatusEntry entry)
    {
        FileStatus state = entry.State;
        if (state.HasFlag(FileStatus.ModifiedInWorkdir))
        {
            string kind = IsIntentToAdd(repository, entry.FilePath) ? IntentToAddKind : "modified";
            return FailureEntry.Change(kind, entry.FilePath);
        }

        return state.HasFlag(FileStatus.DeletedFromWorkdir) ? FailureEntry.Change("deleted", entry.FilePath) : null;
    }

    /// <summary>
    /// True for a file in the state <c>git add -N</c> leaves, which LibGit2Sharp does not expose as a flag: the index
    /// holds the empty blob, and HEAD holds no such file. The caller has already found the file changed in the working
    /// tree, which is why an empty file recorded with <c>git add -N</c> and left empty is not found: it matches the
    /// index, and no content can be lost. An empty file staged with a plain <c>git add</c> and written afterwards is in
    /// the same state, and gets the same answer. A file HEAD holds is a plain edit, even when the index holds it empty:
    /// <c>git restore</c> brings back what was committed.
    /// </summary>
    private static bool IsIntentToAdd(Repository repository, string path)
    {
        // The empty blob first: it is one comparison, and the HEAD lookup is only for the rare file that passes it.
        // The index holds the path, because the status reported it as changed in the working tree.
        return repository.Index[path]?.Id == EmptyBlobId && repository.Head.Tip?[path] is null;
    }

    /// <summary>
    /// A new instance per read: <see cref="StatusOptions"/> is mutable, and one <see cref="GitAssert"/> may be shared
    /// by tests that run in parallel, so no two reads may use the same instance.
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

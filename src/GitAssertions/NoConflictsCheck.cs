using System.Globalization;

using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// The NoConflicts check (docs/specification.md, Checks): fails for every file that still has conflicting versions in
/// the index, which <c>git ls-files -u</c> lists and <c>git status</c> shows as unmerged. It reads the index alone,
/// not the working tree: a file counts as resolved once <c>git add</c> has replaced its versions with one entry,
/// whatever the file on disk holds. It reads an open repository, so <see cref="GitAssert"/> can run it on a repository
/// it opens for this check alone, and a later RunAll on one it opens once for every check.
/// </summary>
internal static class NoConflictsCheck
{
    private const string Rule = "NoConflicts";

    // git add resolves every kind of conflict: it stages the file as it is on disk, or its removal when the resolution
    // was to delete it.
    private static readonly IReadOnlyList<FixStep> Fixes =
    [
        FixStep.WithPaths("Fix: resolve the conflicts in each file, then run:", "git add"),
    ];

    public static CheckResult Run(Repository repository)
    {
        string[] paths = [.. repository.Index.Conflicts.Select(PathOf)];

        if (paths.Length == 0)
        {
            return CheckResult.Healthy(Rule);
        }

        string summary = paths.Length == 1
            ? "1 file has unresolved conflicts."
            : string.Create(CultureInfo.InvariantCulture, $"{paths.Length} files have unresolved conflicts.");

        FailureEntry[] entries = [.. paths.Select(static path => FailureEntry.Change("conflicted", path))];
        return CheckResult.Failing(Rule, summary, entries, Fixes, paths);
    }

    /// <summary>
    /// The path of a conflicted file. Its versions in the index all share one path, but any one of them may be
    /// missing: a file both sides added has no common ancestor, and a file one side deleted has no version from that
    /// side.
    /// </summary>
    private static string PathOf(Conflict conflict)
    {
        return (conflict.Ancestor ?? conflict.Ours ?? conflict.Theirs).Path;
    }
}

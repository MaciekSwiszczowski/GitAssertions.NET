using System.Globalization;

using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// The NoUntrackedFiles check (docs/specification.md, Checks): fails for every file in the working tree that Git
/// neither tracks nor ignores. It reads an open repository: <see cref="GitAssert"/> opens one for this check alone,
/// also when <see cref="GitAssert.RunAll"/> calls it, and closes it when the check returns.
/// </summary>
internal static class NoUntrackedFilesCheck
{
    private const string Rule = "NoUntrackedFiles";

    private static readonly IReadOnlyList<FixStep> Fixes =
    [
        FixStep.WithPaths("Fix:", "git add"),
        FixStep.WithPaths("Or delete them:", "git clean -f"),
        FixStep.TextOnly("Or ignore them: add the paths to .gitignore."),
    ];

    public static CheckResult Run(Repository repository)
    {
        string[] paths =
        [
            .. repository.RetrieveStatus(CreateStatusOptions())
                .Where(static entry => entry.State.HasFlag(FileStatus.NewInWorkdir))
                .Select(static entry => entry.FilePath),
        ];

        if (paths.Length == 0)
        {
            return CheckResult.Healthy(Rule);
        }

        string summary = paths.Length == 1
            ? "1 untracked file found."
            : string.Create(CultureInfo.InvariantCulture, $"{paths.Length} untracked files found.");

        return CheckResult.Failing(Rule, summary, [.. paths.Select(FailureEntry.Path)], Fixes, paths);
    }

    /// <summary>
    /// A new instance per read: <see cref="StatusOptions"/> is mutable, and one <see cref="GitAssert"/> may be shared
    /// by tests that run in parallel, so no two reads may use the same instance.
    /// </summary>
    private static StatusOptions CreateStatusOptions()
    {
        return new StatusOptions
        {
            // Untracked files sit between the index and the working tree; HEAD against the index is not needed.
            Show = StatusShowOption.WorkDirOnly,
            IncludeUntracked = true,

            // Without this, a new folder is reported as "tmp/", not as the files in it, and the fix commands would
            // name a folder.
            RecurseUntrackedDirs = true,

            // Not needed for the result: an ignored file reads Ignored, never NewInWorkdir, so the filter drops it
            // either way. Off, libgit2 does not list ignored files at all, such as every file under bin/ and obj/.
            IncludeIgnored = false,

            // git status lists a file moved without git mv as a deleted path plus an untracked one. With rename
            // detection on, libgit2 would pair the two, and the new path would no longer read as untracked.
            DetectRenamesInWorkDir = false,
        };
    }
}

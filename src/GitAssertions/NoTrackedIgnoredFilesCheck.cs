using System.Globalization;

using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// The NoTrackedIgnoredFiles check (docs/specification.md, Checks): fails for every file in the index that an ignore
/// rule matches, which <c>git ls-files -i -c --exclude-standard</c> lists. A rule hides only files Git does not track,
/// so a file committed before its rule, or forced in with <c>git add -f</c>, stays tracked and is reported. libgit2
/// reads the same rule sources as that command (the repository's <c>.gitignore</c> files, <c>.git/info/exclude</c>, and
/// the file <c>core.excludesFile</c> names) and answers by the rules alone, whether or not the file is tracked. A file
/// a <c>!</c> rule brings back is not ignored, so it is not reported.
/// <para>
/// A folder that is itself ignored and holds no tracked file the check does not report, such as <c>bin/</c>, is one
/// entry, with its file count, and the fix command removes the folder recursively. A folder is never grouped when that
/// would untrack a file the message did not list (<c>bin/*</c> with <c>!bin/keep.txt</c>: the folder is not ignored,
/// only what is inside it). It reads an open repository, so <see cref="GitAssert"/> can run it on a repository it
/// opens for this check alone, and a later RunAll on one it opens once for every check.
/// </para>
/// </summary>
internal static class NoTrackedIgnoredFilesCheck
{
    private const string Rule = "NoTrackedIgnoredFiles";

    private static readonly IReadOnlyList<FixStep> Fixes =
    [
        FixStep.WithPaths("Fix (the files stay on disk), then commit:", "git rm -r --cached"),
    ];

    public static CheckResult Run(Repository repository)
    {
        // A conflicted file has up to three index entries under one path.
        string[] tracked = [.. repository.Index.Select(static entry => entry.Path).Distinct(StringComparer.Ordinal)];
        string[] ignored = [.. tracked.Where(repository.Ignore.IsPathIgnored)];

        if (ignored.Length == 0)
        {
            return CheckResult.Healthy(Rule);
        }

        // Every folder that holds a tracked file this check does not report. Such a folder is never one entry: the
        // fix command would untrack that file too.
        HashSet<string> foldersWithKeptFiles = new(
            tracked.Except(ignored, StringComparer.Ordinal).SelectMany(FoldersOf),
            StringComparer.Ordinal);

        Dictionary<string, bool> folderIgnored = new(StringComparer.Ordinal);
        Dictionary<string, int> groupedFileCounts = new(StringComparer.Ordinal);
        List<FailureEntry> entries = [];
        foreach (string path in ignored)
        {
            string? folder = TopmostGroupableFolder(repository, path, foldersWithKeptFiles, folderIgnored);
            if (folder is null)
            {
                entries.Add(FailureEntry.Path(path));
            }
            else
            {
                groupedFileCounts[folder] = groupedFileCounts.GetValueOrDefault(folder) + 1;
            }
        }

        entries.AddRange(groupedFileCounts.Select(static group => FailureEntry.Folder(group.Key, group.Value)));

        string summary = entries.Count == 1
            ? "1 tracked path matches ignore rules."
            : string.Create(CultureInfo.InvariantCulture, $"{entries.Count} tracked paths match ignore rules.");

        return CheckResult.Failing(Rule, summary, entries, Fixes, ignored);
    }

    /// <summary>
    /// The topmost folder above <paramref name="path"/> that can stand for all its files: ignored itself, and holding
    /// no tracked file that is not reported. Null when there is none, and the file is an entry of its own. Under Git's
    /// rules the first condition implies the second, because nothing inside an ignored folder can be brought back;
    /// the second is kept as a guard against libgit2 reading a rule differently from Git.
    /// </summary>
    private static string? TopmostGroupableFolder(
        Repository repository,
        string path,
        HashSet<string> foldersWithKeptFiles,
        Dictionary<string, bool> folderIgnored)
    {
        foreach (string folder in FoldersOf(path))
        {
            if (foldersWithKeptFiles.Contains(folder))
            {
                continue;
            }

            // Asked once per folder, not once per file in it: a build folder can hold thousands.
            if (!folderIgnored.TryGetValue(folder, out bool isIgnored))
            {
                isIgnored = repository.Ignore.IsPathIgnored(folder);
                folderIgnored[folder] = isIgnored;
            }

            if (isIgnored)
            {
                return folder;
            }
        }

        return null;
    }

    /// <summary>
    /// The folders above <paramref name="path"/>, outermost first, each with its trailing slash: <c>a/</c>, then
    /// <c>a/b/</c> for <c>a/b/c.txt</c>. Empty for a path in the repository root.
    /// </summary>
    private static IEnumerable<string> FoldersOf(string path)
    {
        for (int slash = path.IndexOf('/', StringComparison.Ordinal);
             slash >= 0;
             slash = path.IndexOf('/', slash + 1))
        {
            yield return path[..(slash + 1)];
        }
    }
}

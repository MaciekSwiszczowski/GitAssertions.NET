namespace GitAssertions;

/// <summary>
/// Looks for a network path in the files a repository brings (docs/specification.md, Security, Untrusted repositories
/// and libgit2, Network paths), before libgit2 reads any of them. On Windows, opening a file on a share can log in to
/// that server automatically and hand it the user's credentials, and a repository folder copied in whole, such as an
/// unpacked archive, brings its own <c>.git</c>, whose config can name such a file. libgit2 opens the files some keys
/// name, so this guard reads those files itself, and <see cref="GitAssert.Open"/> refuses a repository that names a
/// share.
/// <list type="bullet">
/// <item>The files: <c>.git/config</c> and every file it includes, followed to git's depth of
/// <see cref="MaxIncludeDepth"/> whatever an <c>includeIf</c> condition says; <c>objects/info/alternates</c>, through
/// every local alternate; and, when <c>.git</c> is a file, the folder its <c>gitdir:</c> line names, that folder's
/// <c>commondir</c>, and its <c>config.worktree</c>.</item>
/// <item>The values: <c>include.path</c>, <c>includeIf.&lt;condition&gt;.path</c>, <c>core.excludesFile</c>, and
/// <c>core.attributesFile</c>; every line of an alternates file; and the <c>gitdir:</c> and <c>commondir</c> paths. A
/// remote's URL is not one: the checks never contact a remote.</item>
/// </list>
/// The guard reads more than libgit2 does on purpose: every path key, every include, and any condition. A value it
/// reads differently from libgit2 can only make it refuse more, never less. It does not follow a symlink inside
/// <c>.git</c> to a share, such as <c>.git/config</c> itself. It never touches a network path itself: a path it finds
/// is reported, not opened, and a file it follows is resolved from a local folder.
/// </summary>
internal static class NetworkPathGuard
{
    /// <summary>
    /// Git's limit for includes inside includes. The config file is depth 0 and the file it includes is depth 1, so
    /// a chain of this many includes is followed to its end and a longer one is not.
    /// </summary>
    private const int MaxIncludeDepth = 10;

    private const string GitDirLinePrefix = "gitdir:";

    private const string IncludeIfPrefix = "includeif.";

    private const string PathSuffix = ".path";

    /// <summary>The prefix of a local path in the long form: <c>\\?\C:\...</c>, which is no share.</summary>
    private const string LongPathPrefix = @"\\?\";

    /// <summary>The same long form in the NT spelling, <c>\??\C:\...</c>, which Windows reads as a path too.</summary>
    private const string NtPathPrefix = @"\??\";

    /// <summary>
    /// The first network path the repository at <paramref name="repositoryRoot"/> names, or null when it names none,
    /// and also when there is no <c>.git</c> folder or file under it. Reads files and nothing else, so it is safe to
    /// call on any folder.
    /// </summary>
    /// <param name="repositoryRoot">The full path of the folder that holds <c>.git</c>.</param>
    /// <param name="homeFolder">The folder <c>~/</c> stands for in an include path.</param>
    public static NetworkPathReference? FindFirst(string repositoryRoot, string homeFolder)
    {
        string dotGit = Path.Combine(repositoryRoot, ".git");
        string gitDirectory;
        if (Directory.Exists(dotGit))
        {
            gitDirectory = dotGit;
        }
        else if (TryReadFile(dotGit) is { } gitFileText)
        {
            // A worktree's or a submodule's .git is a file with one line: gitdir: <path>.
            string? target = ReadGitDirLine(gitFileText);
            if (target is null)
            {
                return null;
            }

            if (IsNetworkPath(target))
            {
                return new NetworkPathReference(dotGit, "gitdir", target);
            }

            if (Resolve(target, repositoryRoot, homeFolder: null) is not { } resolvedGitDirectory)
            {
                return null;
            }

            gitDirectory = resolvedGitDirectory;
        }
        else
        {
            return null;
        }

        if (FindInCommonDirectory(gitDirectory, out string commonDirectory) is { } commonDirectoryReference)
        {
            return commonDirectoryReference;
        }

        return FindInConfigs(gitDirectory, commonDirectory, homeFolder) ?? FindInAlternates(commonDirectory);
    }

    /// <summary>
    /// True when <paramref name="value"/> names a network path: it starts with two path separators, each <c>\</c> or
    /// <c>/</c> in any mix, or with the NT prefix <c>\??\</c>, on every platform, except a local path in its long form,
    /// <c>\\?\</c> or <c>\??\</c> followed by a drive letter and a colon. Windows reads all four mixes of two
    /// separators as the start of a share.
    /// </summary>
    public static bool IsNetworkPath(string value)
    {
        if (value.StartsWith(NtPathPrefix, StringComparison.Ordinal))
        {
            return !HasDriveAt(value, NtPathPrefix.Length);
        }

        if (value.Length >= 2 && IsSeparator(value[0]) && IsSeparator(value[1]))
        {
            return !(value.StartsWith(LongPathPrefix, StringComparison.Ordinal)
                     && HasDriveAt(value, LongPathPrefix.Length));
        }

        return false;
    }

    /// <summary>
    /// Reads the <c>commondir</c> file of the git folder, which a linked worktree has and which names the folder the
    /// repository's config, objects, and refs are in, relative to the git folder. <paramref name="commonDirectory"/>
    /// is that folder, or the git folder itself when there is no such file. Returns the reference when the file names
    /// a network path.
    /// </summary>
    private static NetworkPathReference? FindInCommonDirectory(string gitDirectory, out string commonDirectory)
    {
        commonDirectory = gitDirectory;

        string file = Path.Combine(gitDirectory, "commondir");
        string? value = TryReadFile(file) is { } text ? FirstLine(text) : null;
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (IsNetworkPath(value))
        {
            return new NetworkPathReference(file, "commondir", value);
        }

        commonDirectory = Resolve(value, gitDirectory, homeFolder: null) ?? gitDirectory;
        return null;
    }

    /// <summary>
    /// Reads the config of the common folder and the <c>config.worktree</c> of the git folder, and every file they
    /// include, breadth first, so a file is first reached by the shortest chain of includes and a chain is followed to
    /// <see cref="MaxIncludeDepth"/> exactly. Each file is read once, which ends a cycle of includes.
    /// </summary>
    private static NetworkPathReference? FindInConfigs(string gitDirectory, string commonDirectory, string homeFolder)
    {
        Queue<(string File, int Depth)> pending = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] startingFiles =
        [
            Path.Combine(commonDirectory, "config"),
            Path.Combine(gitDirectory, "config.worktree"),
        ];
        foreach (string file in startingFiles)
        {
            if (seen.Add(file))
            {
                pending.Enqueue((file, 0));
            }
        }

        while (pending.TryDequeue(out (string File, int Depth) current))
        {
            if (TryReadFile(current.File) is not { } text)
            {
                continue;
            }

            string folder = Path.GetDirectoryName(current.File) ?? string.Empty;
            foreach (GitConfigEntry entry in GitConfigReader.Read(text))
            {
                if (entry.Value is not { } value || !IsPathKey(entry.Name))
                {
                    continue;
                }

                if (IsNetworkPath(value))
                {
                    return new NetworkPathReference(current.File, entry.Name, value);
                }

                if (IsIncludeKey(entry.Name)
                    && current.Depth < MaxIncludeDepth
                    && Resolve(value, folder, homeFolder) is { } included
                    && seen.Add(included))
                {
                    pending.Enqueue((included, current.Depth + 1));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the alternates file of the objects folder in <paramref name="commonDirectory"/>, and of every local
    /// alternate it names, which is again an objects folder. A line is a path, relative to the objects folder of the
    /// repository that holds the file; a line that starts with <c>#</c> is a comment.
    /// </summary>
    private static NetworkPathReference? FindInAlternates(string commonDirectory)
    {
        Queue<string> pending = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        string objects = Path.Combine(commonDirectory, "objects");
        seen.Add(objects);
        pending.Enqueue(objects);

        while (pending.TryDequeue(out string? folder))
        {
            string file = Path.Combine(folder, "info", "alternates");
            if (TryReadFile(file) is not { } text)
            {
                continue;
            }

            foreach (string raw in text.Split(LineBreak.Lf))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (IsNetworkPath(line))
                {
                    return new NetworkPathReference(file, "alternate", line);
                }

                if (Resolve(line, folder, homeFolder: null) is { } alternate && seen.Add(alternate))
                {
                    pending.Enqueue(alternate);
                }
            }
        }

        return null;
    }

    /// <summary>The keys whose value is a file libgit2 opens: an include, or an excludes or attributes file.</summary>
    private static bool IsPathKey(string name)
    {
        return IsIncludeKey(name)
               || name.Equals("core.excludesfile", StringComparison.Ordinal)
               || name.Equals("core.attributesfile", StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>include.path</c>, and <c>includeIf.&lt;condition&gt;.path</c> in any form git reads it in, such as the old
    /// <c>[includeIf.condition]</c> header.
    /// </summary>
    private static bool IsIncludeKey(string name)
    {
        return name.Equals("include.path", StringComparison.Ordinal)
               || (name.StartsWith(IncludeIfPrefix, StringComparison.Ordinal)
                   && name.EndsWith(PathSuffix, StringComparison.Ordinal));
    }

    /// <summary>
    /// The path of the first line of a <c>.git</c> file that starts with <c>gitdir:</c>, with the white space around it
    /// trimmed; null when no line does.
    /// </summary>
    private static string? ReadGitDirLine(string text)
    {
        foreach (string line in text.Split(LineBreak.Lf))
        {
            if (line.StartsWith(GitDirLinePrefix, StringComparison.Ordinal))
            {
                return line[GitDirLinePrefix.Length..].Trim();
            }
        }

        return null;
    }

    private static string FirstLine(string text)
    {
        int end = text.IndexOf(LineBreak.Lf, StringComparison.Ordinal);
        return (end < 0 ? text : text[..end]).Trim();
    }

    /// <summary>
    /// The full path <paramref name="value"/> names when it is read in <paramref name="baseFolder"/>, as git reads an
    /// include or a git folder: <c>~/</c> stands for <paramref name="homeFolder"/>, if there is one, and any other
    /// relative path is relative to <paramref name="baseFolder"/>. Null when the text is no path this system can
    /// name.
    /// </summary>
    private static string? Resolve(string value, string baseFolder, string? homeFolder)
    {
        try
        {
            string path = value.StartsWith("~/", StringComparison.Ordinal) && !string.IsNullOrEmpty(homeFolder)
                ? Path.Join(homeFolder, value.AsSpan(2))
                : value;
            return Path.GetFullPath(path, baseFolder);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// The text of the file, or null when there is no such file or it cannot be read: git skips an include it cannot
    /// read, and a file the guard cannot read, libgit2 cannot either.
    /// </summary>
    private static string? TryReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsSeparator(char c)
    {
        return c is '\\' or '/';
    }

    private static bool HasDriveAt(string value, int index)
    {
        return value.Length >= index + 2 && char.IsAsciiLetter(value[index]) && value[index + 1] == ':';
    }
}

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Six traps in a <see cref="TempRepo"/>, the things a repository can ask git to run: two hooks, a clean/smudge
/// filter driver, a process filter driver, and an fsmonitor hook (docs/tasks.md, T2.4). Each trap, when it runs,
/// creates a marker file named after itself in <see cref="MarkerFolder"/>, so a test reads which traps ran from the
/// folder. The marker folder is under <c>.git</c>, never in the working tree, so a marker cannot show up as an
/// untracked file, and it is deleted with the <see cref="TempRepo"/>.
/// </summary>
/// <remarks>
/// <para>
/// The traps are plain git configuration and shell scripts, so they fire when git is the reader: the control tests
/// prove it for each one, and the safety test reads the same repository with the library and finds every marker
/// missing. A trap built wrong (a CR after the shebang, no executable bit, a path the shell misreads) would never
/// fire, and the safety test would pass for nothing.
/// </para>
/// <para>
/// Every script names its marker by absolute path, with forward slashes and in single quotes, so a trap that ran from
/// another working directory still writes where the test looks, and <c>sh</c> on Windows reads <c>C:/...</c>.
/// </para>
/// </remarks>
internal sealed class CommandTraps
{
    public const string PreCommit = "pre-commit";

    public const string PostCheckout = "post-checkout";

    public const string Clean = "clean";

    public const string Smudge = "smudge";

    public const string Process = "process";

    public const string FsMonitor = "fsmonitor";

    /// <summary>The file the clean/smudge driver <c>trap</c> applies to.</summary>
    public const string TrappedFile = "trapped.txt";

    /// <summary>The file the process driver <c>trapprocess</c> applies to.</summary>
    public const string ProcessedFile = "processed.txt";

    /// <summary>
    /// The fsmonitor hook script, as <c>core.fsmonitor</c> holds it: relative to the working tree, which git runs the
    /// hook from, so the value needs no quoting.
    /// </summary>
    public const string FsMonitorHook = ".git/trap-fsmonitor";

    /// <summary>
    /// What the process driver answers to git's greeting: one packet, in git's packet-line format (four hex digits for
    /// the length with the digits themselves, then the text), holding <c>x</c>, where git expects
    /// <c>git-filter-server</c>. Git reports "Unexpected line" and carries on. A driver that exits without a word makes
    /// git 2.34 stop with "fatal: the remote end hung up unexpectedly" (measured on Ubuntu 22.04 in WSL), before the
    /// clean filter and the hooks of the same command have run, and the other controls would fail there. Newer git only
    /// reports an error.
    /// https://git-scm.com/docs/gitattributes#_long_running_filter_process
    /// </summary>
    private const string ProcessReply = "0005x";

    private const string CommittedContent = $"one{Lf}";

    private const string EditedContent = $"two{Lf}";

    private CommandTraps(string markerFolder)
    {
        MarkerFolder = markerFolder;
    }

    /// <summary>The folder every trap writes its marker to; empty while no trap has run.</summary>
    public string MarkerFolder { get; }

    /// <summary>
    /// Arms <paramref name="repo"/> with all six traps, in the order that makes a reader like <c>git status</c> run the
    /// filters: the two filtered files and <c>.gitattributes</c> are committed first, then the hooks and the config are
    /// written, then both files are edited, so their stat no longer matches the index and git must read them through
    /// their drivers.
    /// </summary>
    public static async Task<CommandTraps> ArmAsync(TempRepo repo)
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;

        repo.AddFile(TrappedFile, CommittedContent)
            .AddFile(ProcessedFile, CommittedContent)
            .AddFile(".gitattributes", $"{TrappedFile} filter=trap{Lf}{ProcessedFile} filter=trapprocess{Lf}")
            .Stage(TrappedFile, ProcessedFile, ".gitattributes")
            .Commit("Add the files the traps watch");

        string gitFolder = Path.Combine(repo.Root, ".git");
        string markerFolder = Path.Combine(gitFolder, "trap-markers");
        Directory.CreateDirectory(markerFolder);
        CommandTraps traps = new(markerFolder);

        Directory.CreateDirectory(Path.Combine(gitFolder, "hooks"));
        await traps.WriteScriptAsync(Path.Combine(gitFolder, "hooks", PreCommit), PreCommit, cancellation);
        await traps.WriteScriptAsync(Path.Combine(gitFolder, "hooks", PostCheckout), PostCheckout, cancellation);
        await traps.WriteScriptAsync(Path.Combine(gitFolder, "trap-fsmonitor"), FsMonitor, cancellation);

        await repo.GitAsync("config", "filter.trap.clean", $"touch {traps.ShellMarkerPath(Clean)}; cat");
        await repo.GitAsync("config", "filter.trap.smudge", $"touch {traps.ShellMarkerPath(Smudge)}; cat");
        await repo.GitAsync(
            "config",
            "filter.trapprocess.process",
            $"touch {traps.ShellMarkerPath(Process)}; printf {ProcessReply}");
        await repo.GitAsync("config", "core.fsmonitor", FsMonitorHook);

        repo.AddFile(TrappedFile, EditedContent).AddFile(ProcessedFile, EditedContent);

        return traps;
    }

    /// <summary>Where the trap named <paramref name="trap"/> leaves its marker, as this system spells a path.</summary>
    public string MarkerPath(string trap)
    {
        return Path.Combine(MarkerFolder, trap);
    }

    /// <summary>
    /// The marker path as one word for <c>sh</c>: forward slashes, in single quotes, and each apostrophe in the path
    /// written as <c>'\''</c> (close the quote, an escaped apostrophe, open it again). The temp path comes from the
    /// machine, and a user name can hold an apostrophe, such as <c>C:\Users\O'Brien\AppData\Local\Temp</c>; an unescaped
    /// one ends the quote early, no trap fires, and the safety test would pass for nothing.
    /// </summary>
    private string ShellMarkerPath(string trap)
    {
        var normalizedPath = MarkerPath(trap).Replace('\\', '/').Replace("'", @"'\''");
        return $"'{normalizedPath}'";
    }

    /// <summary>
    /// Writes a <c>#!/bin/sh</c> script that creates the marker of <paramref name="trap"/>, with LF line breaks and no
    /// byte order mark, and makes it executable where the file system has the bit.
    /// </summary>
    private async Task WriteScriptAsync(string path, string trap, CancellationToken cancellation)
    {
        await File.WriteAllTextAsync(path, $"#!/bin/sh{Lf}touch {ShellMarkerPath(trap)}{Lf}", cancellation);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}

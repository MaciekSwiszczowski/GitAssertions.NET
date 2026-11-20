using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using CliWrap;

using Meziantou.Framework;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Runs the real git CLI for the integration tests (docs/specification.md, Test tooling; docs/tasks.md, T5.1). Only the
/// tests run git: the library never does. A test sets up what libgit2 cannot, such as a line-ending conversion or an
/// operation left in progress, with <see cref="TempRepo.GitAsync"/>, then checks what libgit2 reads against real Git.
/// </summary>
/// <remarks>
/// <para>
/// Process handling is CliWrap's alone, with no job object, so the runner works on Linux too. The forceful token is the
/// test's token linked with the runner's timeout, and cancelling it kills git's whole process tree and waits until it
/// has exited. The graceful token is never used: on Windows it starts a helper program to send Ctrl+C. Standard output
/// and standard error are read at the same time, because line-ending warnings fill the standard error pipe, and a
/// reader that finished one stream before the other would deadlock. Standard input stays CliWrap's default, empty and
/// closed, so git never waits for input. A tree kill misses a child whose parent has already exited, and the children
/// git is known to leave running are background maintenance, the fsmonitor daemon, and a credential-cache helper:
/// <see cref="ConfigArguments"/> turn off the first two and the isolation in <see cref="CreateEnvironment"/> removes the
/// third.
/// </para>
/// <para>
/// Git is cut off from the machine's and the user's config, as libgit2 is in the unit tests (<see cref="LibGit2Isolation"/>),
/// so a scenario gives the same result on every machine. A missing git, or one older than <see cref="MinimumVersion"/>,
/// fails the test and says to install or update it: it never skips, because a skipped layer goes unnoticed.
/// </para>
/// </remarks>
internal static partial class GitCli
{
    /// <summary>
    /// The first git that reads <c>GIT_CONFIG_GLOBAL</c>. An older one ignores it, and the tests would read the user's
    /// own config. https://github.com/git/git/blob/master/Documentation/RelNotes/2.32.0.adoc
    /// </summary>
    private static readonly Version MinimumVersion = new(2, 32);

    private const string Executable = "git";

    private const string InstallUrl = "https://git-scm.com/downloads";

    /// <summary>
    /// The author and committer date of every commit git makes here: noon on the day <see cref="TempRepo"/> stamps its
    /// own commits from, a minute apart from midnight on, so a commit git makes comes after the commits of any scenario
    /// built with fewer than 720 libgit2 commits, and is the same on every machine.
    /// </summary>
    private const string CommitDate = "2020-01-01T12:00:00+00:00";

    // What Process.Start reports when the executable is not on PATH: ERROR_FILE_NOT_FOUND and ERROR_PATH_NOT_FOUND on
    // Windows, ENOENT on Linux. Anything else is not "git is missing".
    private const int ErrorFileNotFound = 2;

    private const int ErrorPathNotFound = 3;

    /// <summary>
    /// Long enough for a loaded build agent and for antivirus scanning a freshly written <c>.git</c> folder; a git call
    /// in a throwaway repository takes well under a second.
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Set on every git call, in front of its own arguments, as <c>-c name=value</c>. No background maintenance
    /// (<c>gc.auto</c>, <c>maintenance.auto</c>), and no fsmonitor, because that process outlives git and keeps the temp
    /// folder locked. <c>init.defaultBranch</c> and <c>commit.gpgsign</c> make <c>git init</c> and <c>git commit</c> behave
    /// the same on every machine and git version.
    /// </summary>
    private static readonly string[] ConfigArguments =
    [
        "-c", "gc.auto=0",
        "-c", "maintenance.auto=false",
        "-c", "core.fsmonitor=false",
        "-c", "init.defaultBranch=main",
        "-c", "commit.gpgsign=false",
    ];

    /// <summary>
    /// Whether git is installed and new enough, found out on first use, not when the fixture is built, and kept for the
    /// run. The probe takes no cancellation token: a run that is being cancelled must not record "no git" for every test
    /// after it. Each caller still stops waiting for it when its own token fires. The probe's own timeout bounds it.
    /// </summary>
    [SuppressMessage(
        "Usage",
        "VSTHRD011:Use AsyncLazy<T>",
        Justification = "Lazy<Task<T>> deadlocks when its factory needs a thread a caller is blocking, such as a UI "
                        + "thread. The factory is an async method that returns a running task at its first await, "
                        + "xUnit has no synchronization context, and every caller awaits the task instead of blocking.")]
    private static readonly Lazy<Task<Version>> InstalledVersion = new(static () => ProbeAsync(Executable));

    /// <summary>
    /// An empty folder for <c>XDG_CONFIG_HOME</c>, made on first use and deleted when the process exits. Empty and never
    /// written to, so every git call, in parallel tests too, can share it.
    /// </summary>
    private static readonly Lazy<string> EmptyConfigHome = new(CreateEmptyConfigHome);

    /// <summary>
    /// Runs git in <paramref name="workingDirectory"/> and returns what it wrote to standard output. Throws when git
    /// exits with a failure, or does not finish within the default timeout.
    /// </summary>
    public static Task<string> RunAsync(string workingDirectory, params string[] arguments)
    {
        return RunAsync(workingDirectory, DefaultTimeout, arguments);
    }

    /// <summary>
    /// Runs git in <paramref name="workingDirectory"/> and returns what it wrote to standard output.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// git is missing or too old, or exited with a failure. The message names the command and what git said.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// git did not finish within <paramref name="timeout"/>. It and everything it started were killed first.
    /// </exception>
    public static async Task<string> RunAsync(string workingDirectory, TimeSpan timeout, params string[] arguments)
    {
        CancellationToken runCancellation = TestContext.Current.CancellationToken;
        await InstalledVersion.Value.WaitAsync(runCancellation);

        string command = $"{Executable} {string.Join(' ', arguments)}";
        StringBuilder standardOutput = new();
        StringBuilder standardError = new();
        IReadOnlyDictionary<string, string?> environment = CreateEnvironment(InheritedNames(), EmptyConfigHome.Value);

        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource forceful =
            CancellationTokenSource.CreateLinkedTokenSource(runCancellation, timeoutSource.Token);

        CommandTask<CommandResult> task = Cli.Wrap(Executable)
            .WithWorkingDirectory(workingDirectory)
            .WithArguments([.. ConfigArguments, .. arguments])
            .WithEnvironmentVariables(builder => builder.Set(environment))
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(standardOutput))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(standardError))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(forceful.Token);
        int processId = task.ProcessId;

        try
        {
            CommandResult result = await task;
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{command} exited with code {result.ExitCode}.\n{standardError.ToString().Trim()}");
            }

            return standardOutput.ToString();
        }
        catch (OperationCanceledException) when (
            !runCancellation.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(string.Create(
                CultureInfo.InvariantCulture,
                $"{command} (PID {processId}) did not finish within {timeout.TotalSeconds:0.##} s and was killed."));
        }
    }

    /// <summary>
    /// The environment variables to set on every git call: every inherited <c>GIT_</c> variable removed (a null
    /// value), then the isolation and the fixed identity. Inside a git hook, git can set <c>GIT_DIR</c> and
    /// <c>GIT_INDEX_FILE</c>, and they would send the command to the repository the hook runs in instead of the
    /// <see cref="TempRepo"/>. A variable named in both lists keeps the runner's value.
    /// </summary>
    /// <param name="inheritedNames">The names of the variables this process has.</param>
    /// <param name="emptyConfigHome">
    /// An empty folder for <c>XDG_CONFIG_HOME</c>. <c>GIT_CONFIG_GLOBAL</c> does not cover the user's global ignore and
    /// attributes files, <c>git/ignore</c> and <c>git/attributes</c> under <c>$XDG_CONFIG_HOME</c> or under
    /// <c>~/.config</c>, and a global ignore rule changes what <c>git status</c> reports.
    /// </param>
    public static IReadOnlyDictionary<string, string?> CreateEnvironment(
        IEnumerable<string> inheritedNames,
        string emptyConfigHome)
    {
        // Variable names are case-insensitive on Windows, and removing a lower-case git_ variable elsewhere is harmless.
        Dictionary<string, string?> environment = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in inheritedNames.Where(
                     static name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)))
        {
            environment[name] = null;
        }

        // The CLI's version of LibGit2Isolation: no system config or attributes, and a global config that is the null
        // device, which is always empty.
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
        environment["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        environment["GIT_ATTR_NOSYSTEM"] = "1";
        environment["XDG_CONFIG_HOME"] = emptyConfigHome;

        // Git never waits for a person: no credential prompt, and no editor, which on Windows can open a window on
        // the user's desktop. A test that needs to reorder or drop commits in an interactive rebase sets its own
        // GIT_SEQUENCE_EDITOR.
        environment["GIT_TERMINAL_PROMPT"] = "0";
        environment["GIT_EDITOR"] = "true";
        environment["GIT_SEQUENCE_EDITOR"] = "true";

        // git status otherwise rewrites the index.
        environment["GIT_OPTIONAL_LOCKS"] = "0";

        // A commit is the same on every machine.
        environment["GIT_AUTHOR_NAME"] = TempRepo.AuthorName;
        environment["GIT_AUTHOR_EMAIL"] = TempRepo.AuthorEmail;
        environment["GIT_AUTHOR_DATE"] = CommitDate;
        environment["GIT_COMMITTER_NAME"] = TempRepo.AuthorName;
        environment["GIT_COMMITTER_EMAIL"] = TempRepo.AuthorEmail;
        environment["GIT_COMMITTER_DATE"] = CommitDate;

        return environment;
    }

    /// <summary>
    /// Reads the version out of the output of <c>git --version</c>, such as <c>git version 2.56.0.windows.4</c>, and
    /// checks it against <see cref="MinimumVersion"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The output holds no version, or the version is too old. The message says to install or update git.
    /// </exception>
    public static Version RequireSupported(string versionOutput)
    {
        Match match = VersionInOutput().Match(versionOutput);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"Could not read the git version from \"{versionOutput.Trim()}\". Install git {MinimumVersion} or "
                + $"later: {InstallUrl}");
        }

        Version version = Version.Parse(match.Groups["version"].Value);
        if (version < MinimumVersion)
        {
            throw new InvalidOperationException(
                $"git {version} is older than {MinimumVersion}, the first version that reads GIT_CONFIG_GLOBAL; an "
                + $"older git would read your own Git config in the integration tests. Update git: {InstallUrl}");
        }

        return version;
    }

    /// <summary>Runs <c>&lt;executable&gt; --version</c> and checks what answers.</summary>
    /// <exception cref="InvalidOperationException">
    /// The executable is not on PATH, fails, or is too old. The message says to install or update git.
    /// </exception>
    /// <exception cref="TimeoutException"><c>--version</c> did not finish within the probe's timeout.</exception>
    public static async Task<Version> ProbeAsync(string executable)
    {
        StringBuilder output = new();
        using CancellationTokenSource timeout = new(ProbeTimeout);

        try
        {
            CommandResult result = await Cli.Wrap(executable)
                .WithArguments("--version")
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(timeout.Token);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"\"{executable} --version\" exited with code {result.ExitCode}. Install git {MinimumVersion} or "
                    + $"later: {InstallUrl}");
            }
        }
        catch (Win32Exception exception) when (IsExecutableNotFound(exception))
        {
            throw new InvalidOperationException(
                $"\"{executable}\" was not found on PATH. Install git {MinimumVersion} or later: {InstallUrl}",
                exception);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(string.Create(
                CultureInfo.InvariantCulture,
                $"\"{executable} --version\" did not finish within {ProbeTimeout.TotalSeconds:0.##} s."));
        }

        return RequireSupported(output.ToString());
    }

    [GeneratedRegex(@"^git version (?<version>\d+\.\d+(\.\d+)?)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionInOutput();

    /// <summary>
    /// True when starting the process failed because no such executable is on PATH. CliWrap wraps the exception
    /// <see cref="System.Diagnostics.Process.Start()"/> throws in a new <see cref="Win32Exception"/> of its own, which
    /// has lost the native error code, so the code is read from the inner exception as well.
    /// </summary>
    private static bool IsExecutableNotFound(Win32Exception exception)
    {
        return exception.NativeErrorCode is ErrorFileNotFound or ErrorPathNotFound
               || exception.InnerException is Win32Exception { NativeErrorCode: ErrorFileNotFound or ErrorPathNotFound };
    }

    private static IEnumerable<string> InheritedNames()
    {
        return Environment.GetEnvironmentVariables().Keys.Cast<string>();
    }

    private static string CreateEmptyConfigHome()
    {
        TemporaryDirectory directory = TemporaryDirectory.Create();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => directory.Dispose();
        return directory.FullPath;
    }
}

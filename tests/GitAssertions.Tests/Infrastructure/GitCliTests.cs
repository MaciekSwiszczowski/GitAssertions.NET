using System.Diagnostics;
using System.Text.RegularExpressions;

// An alias, not "using LibGit2Sharp;": that namespace has its own Version, which hides System.Version here.
using FileStatus = LibGit2Sharp.FileStatus;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Pins the runner that sets up scenarios with the real git CLI (<see cref="GitCli"/>, docs/tasks.md, T5.1): a command
/// run in a <see cref="TempRepo"/> changes what libgit2 reads; git is cut off from the machine's and the user's
/// config, as libgit2 is in the unit tests; a command that fails, or outlives its timeout, fails the test with a message
/// that says what ran; and a missing or too old git fails the test, saying to install or update it. The tests that run
/// git are tagged <see cref="TestCategories.Integration"/>; the ones that read only the runner's own values are not.
/// </summary>
public sealed partial class GitCliTests
{
    private const string FixtureAuthor = "GitAssertions Tests <tests@gitassertions.invalid>";

    [GeneratedRegex(@"\(PID (?<pid>\d+)\)")]
    private static partial Regex ProcessIdInMessage { get; }

    // Trigger: A test author adds a new file to a TempRepo, and has git stage it, because the scenario needs a step
    //          libgit2 cannot do.
    // Behavior: The git command changes the repository on disk, and the libgit2 handle the TempRepo holds reads the
    //           change.
    // Assertion: The path's status reads NewInIndex.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task GitAsync_AddNewFile_ChangesStatusToNewInIndex()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("new.txt");

        // Act
        await repo.GitAsync("add", "new.txt");

        // Assert
        repo.Repo.RetrieveStatus("new.txt").ShouldBe(FileStatus.NewInIndex);
    }

    // Trigger: A test author has git commit in a TempRepo, with no author or committer given, on a machine whose own
    //          Git config names someone else.
    // Behavior: The commit carries the fixed author and committer, with the fixed date, so it is the same on every
    //           machine.
    // Assertion: The log line reads the fixture name, email, and date twice, once for the author and once for the
    //            committer. The date is the Unix time 1577880000, noon UTC on 2020-01-01, which every git version prints
    //            alike; the ISO form differs between versions (+00:00 or Z).
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task GitAsync_Commit_UsesTheFixedAuthorAndCommitter()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        await repo.GitAsync("commit", "--allow-empty", "-m", "Empty");
        string log = await repo.GitAsync("log", "-1", "--date=unix", "--format=%an <%ae> %ad | %cn <%ce> %cd");

        // Assert
        log.TrimEnd().ShouldBe($"{FixtureAuthor} 1577880000 | {FixtureAuthor} 1577880000");
    }

    // Trigger: A test author runs git in a TempRepo on a machine with a system, global, or XDG Git config of its own,
    //          such as Git for Windows' core.autocrlf=true.
    // Behavior: Git reads none of them: the repository's own config and the runner's command-line values are all
    //           there is, so a scenario gives the same result on every machine.
    // Assertion: Every line of the config listing has the origin "command line:" or the repository's .git/config.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task GitAsync_ConfigList_ReadsOnlyTheRepositoryAndTheCommandLine()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        string[] lines = await ConfigListAsync(repo);

        // Assert
        lines.ShouldNotBeEmpty();
        lines.ShouldAllBe(static line => line.StartsWith("command line:", StringComparison.Ordinal)
                                         || line.StartsWith("file:.git/config", StringComparison.Ordinal));
    }

    // Trigger: A test author runs git in a TempRepo, and expects no background maintenance, no fsmonitor process, no
    //          commit signing, and a main branch.
    // Behavior: The runner passes those five settings on the command line of every git call.
    // Assertion: The config listing's command-line entries are exactly gc.auto=0, maintenance.auto=false,
    //            core.fsmonitor=false, init.defaultbranch=main, and commit.gpgsign=false. The listing prints the
    //            variable name in lower case, https://git-scm.com/docs/git-config#_configuration_file
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task GitAsync_ConfigList_HoldsTheFiveCommandLineValues()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        string[] lines = await ConfigListAsync(repo);

        // Assert
        lines.Where(static line => line.StartsWith("command line:", StringComparison.Ordinal))
            .Select(static line => line.Split('\t')[1].TrimEnd())
            .ShouldBe(
                [
                    "gc.auto=0",
                    "maintenance.auto=false",
                    "core.fsmonitor=false",
                    "init.defaultbranch=main",
                    "commit.gpgsign=false",
                ],
                ignoreOrder: true);
    }

    // Trigger: A test author has git commit with no message on the command line, so git would open an editor, here with
    //          an empty message allowed.
    // Behavior: The runner names `true` as the editor, which leaves the message as it is, so git never waits for a person
    //           and no editor window opens on the desktop.
    // Assertion: The commit is made, and its subject is empty.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task GitAsync_CommitWithoutMessage_DoesNotWaitForAnEditor()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        await repo.GitAsync("commit", "--allow-empty", "--allow-empty-message");
        string subject = await repo.GitAsync("log", "-1", "--format=%s");

        // Assert
        subject.Trim().ShouldBeEmpty();
    }

    // Trigger: A test author runs a git command that fails, here one git does not have.
    // Behavior: The test fails with a message that names the command, its exit code, and what git said, so the author
    //           need not run it again by hand.
    // Assertion: An InvalidOperationException whose message holds "git no-such-command", "exited with code 1", and
    //            git's own "is not a git command".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task GitAsync_FailingCommand_ThrowsNamingTheCommandItsExitCodeAndWhatGitSaid()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(repo.GitAsync("no-such-command"));

        // Assert
        exception.Message.ShouldContain("git no-such-command");
        exception.Message.ShouldContain("exited with code 1");
        exception.Message.ShouldContain("is not a git command");
    }

    // Trigger: A test author runs a git command that never ends, here an alias that sleeps for ten minutes, with a one
    //          second limit.
    // Behavior: The runner kills git and everything it started, waits until it is gone, and fails the test, so no
    //           process is left running on the machine.
    // Assertion: A TimeoutException whose message names the command and the process id, and no process has that id
    //            when the exception arrives.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task RunAsync_CommandOutlivesTheTimeout_KillsGitAndThrowsTimeoutException()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        TimeoutException exception = await Should.ThrowAsync<TimeoutException>(
            GitCli.RunAsync(repo.Root, TimeSpan.FromSeconds(1), "-c", "alias.hang=!sleep 600", "hang"));

        // Assert
        exception.Message.ShouldContain("git -c alias.hang=!sleep 600 hang");
        Match processId = ProcessIdInMessage.Match(exception.Message);
        processId.Success.ShouldBeTrue(exception.Message);
        Should.Throw<ArgumentException>(() => Process.GetProcessById(int.Parse(processId.Groups["pid"].Value)));
    }

    // Trigger: A test author runs a git command that writes 300,000 bytes to standard error, as git does with a
    //          line-ending warning for each of many files.
    // Behavior: The runner reads standard error while it reads standard output, so a full pipe never stops git, and
    //           the command finishes.
    // Assertion: The command returns, with nothing on standard output.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    [Trait("Category", TestCategories.Integration)]
    public async Task GitAsync_CommandFillsStandardErrorPipe_CompletesInsteadOfDeadlocking()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        string output = await repo.GitAsync("-c", "alias.noisy=!yes error | head -c 300000 >&2", "noisy");

        // Assert
        output.ShouldBeEmpty();
    }

    // Trigger: A test author runs git from inside a git hook, or a shell where GIT_DIR, GIT_INDEX_FILE, and other GIT_
    //          variables are set, which would send the command to another repository.
    // Behavior: The runner removes every inherited variable whose name starts with GIT_, and nothing else, so
    //           GITHUB_ACTIONS and PATH reach git as they are.
    // Assertion: The three GIT_ names are in the environment with no value, and PATH and GITHUB_ACTIONS are not in it.
    [Fact]
    public void CreateEnvironment_InheritedGitVariables_AreRemovedAndNothingElseIsTouched()
    {
        // Act
        IReadOnlyDictionary<string, string?> environment = GitCli.CreateEnvironment(
            ["GIT_DIR", "GIT_INDEX_FILE", "git_work_tree", "GITHUB_ACTIONS", "PATH"],
            "empty-config-home");

        // Assert
        environment.Where(static pair => pair.Value is null).Select(static pair => pair.Key)
            .ShouldBe(["GIT_DIR", "GIT_INDEX_FILE", "git_work_tree"], ignoreOrder: true);
        environment.Keys.ShouldNotContain("GITHUB_ACTIONS");
        environment.Keys.ShouldNotContain("PATH");
    }

    // Trigger: A test author runs git while GIT_EDITOR and GIT_AUTHOR_NAME are inherited from the shell.
    // Behavior: The runner's own values win over inherited ones, and set everything the isolation needs: no system
    //           config or attributes, an empty XDG folder, no prompt, no editor, no optional locks, and a fixed author
    //           and committer with a fixed date.
    // Assertion: The values the runner sets, other than the null device it names as the global config, are exactly
    //            these thirteen.
    [Fact]
    public void CreateEnvironment_InheritedVariableTheRunnerSets_KeepsTheRunnersValue()
    {
        // Act
        IReadOnlyDictionary<string, string?> environment =
            GitCli.CreateEnvironment(["GIT_EDITOR", "GIT_AUTHOR_NAME"], "empty-config-home");

        // Assert
        environment.Where(static pair => pair.Value is not null && pair.Key != "GIT_CONFIG_GLOBAL")
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key}={pair.Value}")
            .ShouldBe(
            [
                "GIT_ATTR_NOSYSTEM=1",
                "GIT_AUTHOR_DATE=2020-01-01T12:00:00+00:00",
                "GIT_AUTHOR_EMAIL=tests@gitassertions.invalid",
                "GIT_AUTHOR_NAME=GitAssertions Tests",
                "GIT_COMMITTER_DATE=2020-01-01T12:00:00+00:00",
                "GIT_COMMITTER_EMAIL=tests@gitassertions.invalid",
                "GIT_COMMITTER_NAME=GitAssertions Tests",
                "GIT_CONFIG_NOSYSTEM=1",
                "GIT_EDITOR=true",
                "GIT_OPTIONAL_LOCKS=0",
                "GIT_SEQUENCE_EDITOR=true",
                "GIT_TERMINAL_PROMPT=0",
                "XDG_CONFIG_HOME=empty-config-home",
            ]);
    }

    // Trigger: A test author runs git on Windows or on Linux.
    // Behavior: The global config is replaced by the platform's null device, a file that is always empty, because
    //           GIT_CONFIG_GLOBAL needs a path and git reads no config from the null device.
    // Assertion: GIT_CONFIG_GLOBAL is NUL or /dev/null. That git then reads no global config is pinned end to end by
    //            the config listing test above.
    [Fact]
    public void CreateEnvironment_Always_PointsTheGlobalConfigAtTheNullDevice()
    {
        // Act
        IReadOnlyDictionary<string, string?> environment = GitCli.CreateEnvironment([], "empty-config-home");

        // Assert
        environment["GIT_CONFIG_GLOBAL"].ShouldBeOneOf("NUL", "/dev/null");
    }

    // Trigger: A test author runs the integration tests on a machine with git 2.32 or later, which is the first
    //          version that reads GIT_CONFIG_GLOBAL, whatever its vendor suffix.
    // Behavior: The runner accepts it and reports the version.
    // Assertion: The returned version has the major, minor, and patch of the output, and the suffix is dropped.
    [Theory]
    [InlineData("git version 2.32.0", 2, 32, 0)]
    [InlineData("git version 2.56.0.windows.4", 2, 56, 0)]
    [InlineData($"git version 3.0.1{Lf}", 3, 0, 1)]
    public void RequireSupported_GitFrom2_32_ReturnsItsVersion(string output, int major, int minor, int patch)
    {
        // Act
        Version version = GitCli.RequireSupported(output);

        // Assert
        version.ShouldBe(new Version(major, minor, patch));
    }

    // Trigger: A test author runs the integration tests on a machine with git 2.31, which ignores GIT_CONFIG_GLOBAL
    //          and would read the user's own global config.
    // Behavior: The test fails, and the message says which version it found and to update git, instead of letting the
    //           tests read the user's config.
    // Assertion: An InvalidOperationException whose message holds "git 2.31.1 is older than 2.32" and "Update git".
    [Fact]
    public void RequireSupported_GitOlderThan2_32_ThrowsSayingToUpdateGit()
    {
        // Assert
        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(static () => GitCli.RequireSupported("git version 2.31.1"));
        exception.Message.ShouldContain("git 2.31.1 is older than 2.32");
        exception.Message.ShouldContain("Update git");
    }

    // Trigger: A test author runs the integration tests, and what answers to "git --version" is not a git.
    // Behavior: The test fails, and the message quotes what was printed and says to install git.
    // Assertion: An InvalidOperationException whose message holds "Could not read the git version from", the quoted
    //            output, and "Install git".
    [Fact]
    public void RequireSupported_OutputWithoutAVersion_ThrowsSayingToInstallGit()
    {
        // Assert
        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(static () => GitCli.RequireSupported("hello world"));
        exception.Message.ShouldContain("Could not read the git version from \"hello world\"");
        exception.Message.ShouldContain("Install git");
    }

    // Trigger: A test author runs the integration tests on a machine, or a CI image, with no git on PATH.
    // Behavior: The test fails, and the message says git was not found and to install it. It is never skipped, so the
    //           layer cannot drop out of a run unnoticed.
    // Assertion: An InvalidOperationException whose message holds "was not found on PATH" and "Install git 2.32".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task ProbeAsync_ExecutableNotOnPath_ThrowsSayingToInstallGit()
    {
        // Act
        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(GitCli.ProbeAsync("git-that-is-not-installed"));

        // Assert
        exception.Message.ShouldContain("\"git-that-is-not-installed\" was not found on PATH");
        exception.Message.ShouldContain("Install git 2.32");
    }

    /// <summary>
    /// The lines of <c>git config --list --show-origin</c>: each setting git reads, with the place it read it from.
    /// </summary>
    private static async Task<string[]> ConfigListAsync(TempRepo repo)
    {
        string output = await repo.GitAsync("config", "--list", "--show-origin");
        return output.Split(LineBreak.Lf, StringSplitOptions.RemoveEmptyEntries);
    }
}

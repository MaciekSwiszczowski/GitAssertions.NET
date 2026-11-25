using GitAssertions.Tests.Infrastructure;

using LibGit2Sharp;

using Meziantou.Framework;

namespace GitAssertions.Tests;

/// <summary>
/// Pins that the library never runs anything a repository asks for (docs/specification.md, Security, Untrusted
/// repositories and libgit2). A <see cref="TempRepo"/> is armed with six traps (<see cref="HostileTraps"/>: two hooks,
/// a clean/smudge driver, a process driver, and an fsmonitor hook), each of which creates a marker file when it runs.
/// The control tests prove that real git sets every trap off, because a trap built wrong never fires and the safety
/// test would pass for nothing. The safety test then opens the same kind of repository with the library and runs every
/// check, and finds no marker. These are value tests, not snapshots: the safety test runs the checks only to drive the
/// library through a scenario, and asserts nothing about their results, which the check tests pin.
/// </summary>
public sealed class HostileRepositoryTests
{
    private const string FolderWithApostrophe = "o'brien";

    /// <summary>
    /// Each trap and the git command that sets it off, measured on git 2.56 and 2.34.1. The fsmonitor row's own
    /// <c>-c</c> comes after the <c>core.fsmonitor=false</c> that <see cref="GitCli"/> puts in front of every command,
    /// and the last one wins. The same table drives the control tests in a repository under the temp folder and in one
    /// under a folder with an apostrophe.
    /// </summary>
    public static TheoryData<string, string> Triggers()
    {
        return new TheoryData<string, string>
        {
            { HostileTraps.PreCommit, "commit --allow-empty -m trap" },
            { HostileTraps.PostCheckout, "checkout -- " + HostileTraps.TrappedFile },
            { HostileTraps.Clean, "status" },
            { HostileTraps.Smudge, "checkout -- " + HostileTraps.TrappedFile },
            { HostileTraps.Process, "status" },
            { HostileTraps.FsMonitor, "-c core.fsmonitor=" + HostileTraps.FsMonitorHook + " status" },
        };
    }

    // Trigger: A test author arms a repository with the six traps and then reads it with real git, the way an agent or
    //          a developer would, to be sure each trap works.
    // Behavior: Each git command sets its trap off, and the trap leaves its marker. `git status` reads the two edited
    //           files through their drivers and runs the fsmonitor hook (the command's own `-c` beats the
    //           `core.fsmonitor=false` the test runner puts in front of every command), a commit runs the pre-commit
    //           hook, and a checkout of one file runs its smudge filter and the post-checkout hook. The process driver
    //           answers the filter protocol's handshake with a line git does not expect, so git reports an error and
    //           goes on, and the test ignores the error.
    // Assertion: The marker folder holds the marker named after the trap.
    // https://git-scm.com/docs/githooks and https://git-scm.com/docs/gitattributes#_filter
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [MemberData(nameof(Triggers))]
    public async Task Trap_GitRunsItsCommand_LeavesItsMarker(string trap, string arguments)
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        HostileTraps traps = await HostileTraps.ArmAsync(repo);

        // Act
        await GitCli.RunLineAsync(repo.Root, arguments);

        // Assert
        Directory.GetFiles(traps.MarkerFolder).ShouldContain(traps.MarkerPath(trap));
    }

    // Trigger: A developer's profile folder holds an apostrophe, as a Windows user name can (C:\Users\O'Brien), so the
    //          machine's temp folder, and every repository under it, has one in its path.
    // Behavior: The traps still fire when git reads the repository, because each marker path reaches the shell quoted
    //           with its apostrophe escaped. An unescaped one ends the quote early, no trap fires, and the safety test
    //           below would pass for nothing on that machine. The repository is made inside a folder of its own with an
    //           apostrophe in its name, so the machine's temp variables stay as they are.
    // Assertion: The marker folder holds the marker named after the trap, as in the control tests above.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [MemberData(nameof(Triggers))]
    public async Task Trap_RepositoryPathHoldsApostrophe_LeavesItsMarker(string trap, string arguments)
    {
        // Arrange
        using TemporaryDirectory parent = TemporaryDirectory.Create();
        string folder = Directory.CreateDirectory(parent.GetFullPath(FolderWithApostrophe)).FullName;
        using TempRepo repo = TempRepo.Create(folder);
        HostileTraps traps = await HostileTraps.ArmAsync(repo);

        // Act
        await GitCli.RunLineAsync(repo.Root, arguments);

        // Assert
        Directory.GetFiles(traps.MarkerFolder).ShouldContain(traps.MarkerPath(trap));
    }

    // Trigger: A developer runs every check on a repository folder copied from elsewhere, an unpacked archive say,
    //          which carries hooks, filter drivers and an fsmonitor command, and whose edited files git would have to
    //          read through those drivers.
    // Behavior: libgit2 reads the repository as data: it runs no hook, no clean, smudge, or process driver, and no
    //           fsmonitor hook, so the checks cannot be made to run a command by the repository they read. A result,
    //           healthy or failing, is not this test's business, but an exception from Open or RunAll still fails it.
    // Assertion: The marker folder holds no file, so a failure names the traps that fired.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task OpenAndRunAll_RepositoryCarriesTraps_RunsNoneOfThem()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        HostileTraps traps = await HostileTraps.ArmAsync(repo);

        // Act
        GitAssert.Open(repo.Root).RunAll();

        // Assert
        Directory.GetFiles(traps.MarkerFolder).ShouldBeEmpty();
    }

    // Trigger: A developer opens a repository and runs every check, and the process has not touched libgit2's
    //          repository owner validation (`safe.directory`).
    // Behavior: The library leaves the validation switched on, so a repository owned by another user is still refused
    //           by libgit2. The setting is process-wide, so no test may change it. A repository owned by another user,
    //           which would show the validation at work, needs root or admin to set up and is not tested.
    // Assertion: GlobalSettings.GetOwnerValidation() is true after Open and RunAll.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void OpenAndRunAll_Always_LeavesOwnerValidationOn()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        GitAssert.Open(repo.Root).RunAll();


        // Assert
        GlobalSettings.GetOwnerValidation().ShouldBeTrue();
    }
}

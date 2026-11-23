using GitAssertions.Tests.Infrastructure;

using LibGit2Sharp;

using Meziantou.Framework;

namespace GitAssertions.Tests;

/// <summary>
/// Pins the refusal of <see cref="GitAssert.Open"/> for a repository whose own files name a network path
/// (docs/specification.md, Security, Untrusted repositories and libgit2, Network paths): what is refused, what is
/// still opened, and what the message says. Every network path here uses the host <c>gitassertions.invalid</c>, a name
/// that can never resolve (RFC 2606), so a check that misses one fails visibly instead of reaching a server. What the
/// guard follows, and what counts as a network path, is pinned in <see cref="NetworkPathGuardTests"/>, and how a
/// config file is read in <see cref="GitConfigReaderTests"/>.
/// </summary>
public sealed class OpenNetworkPathTests
{
    private const string Host = "gitassertions.invalid";

    private const string BackslashShare = @"\\" + Host + @"\share\x";

    private const string SlashShare = "//" + Host + "/share/x";

    /// <summary>The only difference between the refusals: the line the message ends with.</summary>
    private const string MessageEnding =
        "Opening it could send your credentials to that server. If the repository came from somewhere you do not "
        + "trust, remove the setting.";

    /// <summary>
    /// The four keys libgit2 opens as files, as given to <c>git config</c> and as git prints them, each with the two
    /// spellings of a share the spec names.
    /// </summary>
    public static TheoryData<string, string, string> ConfigKeysAndShares()
    {
        TheoryData<string, string, string> data = [];
        foreach ((string given, string printed) in new[]
                 {
                     ("include.path", "include.path"),
                     ("includeIf.gitdir:**.path", "includeif.gitdir:**.path"),
                     ("core.excludesFile", "core.excludesfile"),
                     ("core.attributesFile", "core.attributesfile"),
                 })
        {
            data.Add(given, printed, BackslashShare);
            data.Add(given, printed, SlashShare);
        }

        return data;
    }

    // Trigger: A developer opens a repository folder copied from elsewhere, whose config sets a key libgit2 opens as a
    //          file to a share, written by git itself, and with either kind of slash.
    // Behavior: Open refuses it before libgit2 reads anything, and the message names the config file, the key, and
    //           the value.
    // Assertion: An ArgumentException for repoPath whose message holds .git/config, the key as git prints it, and the
    //            value as git reads it.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [MemberData(nameof(ConfigKeysAndShares))]
    public async Task Open_ConfigKeyNamesShare_RefusesNamingFileKeyAndValue(string key, string printedKey, string share)
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        await repo.GitAsync("config", key, share);

        // Assert
        ShouldRefuse(repo.Root, Path.Combine(repo.Root, ".git", "config"), printedKey, share);
    }

    // Trigger: A hostile config names a share with the other spellings Windows reads as one: a mix of the two slashes,
    //          and the NT prefix.
    // Behavior: Open refuses them like the plain ones, because libgit2 would open them the same way.
    // Assertion: An ArgumentException for repoPath whose message holds .git/config, core.excludesfile, and the value.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData(@"\/" + Host + "/share/x")]
    [InlineData(@"/\" + Host + @"\share\x")]
    [InlineData(@"\??\UNC\" + Host + @"\share\x")]
    [InlineData(@"\\?\UNC\" + Host + @"\share\x")]
    public async Task Open_ConfigKeyNamesShareInAnotherSpelling_Refuses(string share)
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        await repo.GitAsync("config", "core.excludesFile", share);

        // Assert
        ShouldRefuse(repo.Root, Path.Combine(repo.Root, ".git", "config"), "core.excludesfile", share);
    }

    // Trigger: A developer opens a repository folder whose config includes a local file, and that file sets a key to a
    //          share.
    // Behavior: Open follows the include and refuses, naming the included file, not the config.
    // Assertion: An ArgumentException whose message holds the included file, core.excludesfile, and the value.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_IncludedFileNamesShare_RefusesNamingTheIncludedFile()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string included = Path.Combine(repo.Root, ".git", "work.cfg");
        await repo.GitAsync("config", "--file", included, "core.excludesFile", SlashShare);
        await repo.GitAsync("config", "include.path", "work.cfg");

        // Assert
        ShouldRefuse(repo.Root, included, "core.excludesfile", SlashShare);
    }

    // Trigger: A hostile config includes a file under an includeIf condition that this repository never meets.
    // Behavior: Open follows the include whatever the condition says, because the condition is the hostile author's to
    //           write, and refuses.
    // Assertion: An ArgumentException whose message holds the included file, core.attributesfile, and the value.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_IncludeIfConditionNeverMatches_StillFollowsTheFile()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string included = Path.Combine(repo.Root, ".git", "never.cfg");
        await repo.GitAsync("config", "--file", included, "core.attributesFile", BackslashShare);
        await repo.GitAsync("config", "includeIf.gitdir:/no/such/folder/.path", "never.cfg");

        // Assert
        ShouldRefuse(repo.Root, included, "core.attributesfile", BackslashShare);
    }

    // Trigger: A hostile config includes a file in a folder under .git, and that file includes another with a relative
    //          path.
    // Behavior: A relative include is resolved against the folder of the file that includes it, as git does, so the
    //           guard reaches the second file and refuses.
    // Assertion: An ArgumentException whose message holds the inner file, core.excludesfile, and the value.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_RelativeIncludeInsideIncludedFile_IsResolvedAgainstThatFile()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string gitFolder = Path.Combine(repo.Root, ".git");
        Directory.CreateDirectory(Path.Combine(gitFolder, "outer", "inner"));
        string inner = Path.Combine(gitFolder, "outer", "inner", "inner.cfg");
        string outer = Path.Combine(gitFolder, "outer", "outer.cfg");
        await repo.GitAsync("config", "--file", inner, "core.excludesFile", SlashShare);
        await repo.GitAsync("config", "--file", outer, "include.path", "inner/inner.cfg");
        await repo.GitAsync("config", "include.path", "outer/outer.cfg");

        // Assert
        ShouldRefuse(repo.Root, inner, "core.excludesfile", SlashShare);
    }

    // Trigger: A hostile folder has an alternates file with a line that names a share.
    // Behavior: Open refuses, and the message names the alternates file, the key "alternate", and the line.
    // Assertion: An ArgumentException whose message holds .git/objects/info/alternates, alternate, and the line.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_AlternatesLineNamesShare_RefusesNamingTheLine()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string alternates = Path.Combine(repo.Root, ".git", "objects", "info", "alternates");
        File.WriteAllText(alternates, $"{SlashShare}{Lf}");

        // Assert
        ShouldRefuse(repo.Root, alternates, "alternate", SlashShare);
    }

    // Trigger: A hostile folder's alternates file names a local folder, and that folder has an alternates file of its
    //          own that names a share.
    // Behavior: Open follows the local alternate, with a relative path taken from the objects folder, and refuses.
    // Assertion: An ArgumentException whose message holds the alternates file of the local folder, and the line.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_LocalAlternateNamesShareInItsOwnAlternates_Refuses()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        File.WriteAllText(Path.Combine(repo.Root, ".git", "objects", "info", "alternates"), $"../../other-objects{Lf}");
        string nested = Path.Combine(repo.Root, "other-objects", "info", "alternates");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, $"{BackslashShare}{Lf}");

        // Assert
        ShouldRefuse(repo.Root, nested, "alternate", BackslashShare);
    }

    // Trigger: A hostile folder has a .git file, as a worktree or a submodule has, and its gitdir line names a share.
    // Behavior: Open refuses, and the message names the .git file, the key gitdir, and the path.
    // Assertion: An ArgumentException whose message holds the .git file, gitdir, and the path.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_GitFileNamesShare_RefusesNamingTheGitFile()
    {
        // Arrange
        using TemporaryDirectory folder = TemporaryDirectory.Create();
        string gitFile = folder.GetFullPath(".git");
        File.WriteAllText(gitFile, $"gitdir: {SlashShare}{Lf}");

        // Assert
        ShouldRefuse(folder.FullPath, gitFile, "gitdir", SlashShare);
    }

    // Trigger: A linked worktree's folder in the repository it belongs to has its commondir file, or its
    //          config.worktree file, rewritten to name a share.
    // Behavior: Open follows the worktree's .git file to its folder and refuses for either file.
    // Assertion: An ArgumentException whose message holds the rewritten file, the key, and the value.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("commondir", "commondir", SlashShare)]
    [InlineData("config.worktree", "core.excludesfile", SlashShare)]
    public async Task Open_LinkedWorktreeFolderNamesShare_RefusesNamingTheFile(
        string fileName,
        string printedKey,
        string share)
    {
        // Arrange
        using TempRepo main = TempRepo.Create();
        using TemporaryDirectory outside = TemporaryDirectory.Create();
        string worktree = outside.GetFullPath("worktree");
        await main.GitAsync("worktree", "add", worktree, "-b", "feature");
        string worktreeFolder = Path.Combine(main.Root, ".git", "worktrees", "worktree");
        string content = fileName == "commondir" ? $"{share}{Lf}" : $"[core]{Lf}excludesFile = {share}{Lf}";
        await File.WriteAllTextAsync(
            Path.Combine(worktreeFolder, fileName),
            content,
            TestContext.Current.CancellationToken);

        // Assert
        // The message names the file by the folder the worktree's .git file gives, which git writes in its long form
        // where the temp path is a short one (RUNNER~1), so only the end of the path is compared.
        ShouldRefuse(worktree, Path.Combine("worktrees", "worktree", fileName), printedKey, share);
    }

    /// <summary>The config text, and the key and value Open must name for it: how git accepts a value.</summary>
    public static TheoryData<string, string, string> HandWrittenConfigs()
    {
        return new TheoryData<string, string, string>
        {
            { $"[core]{Lf}    excludesFile = \"{SlashShare}\"{Lf}", "core.excludesfile", SlashShare },
            {
                $@"[core]{Lf}    excludesFile = \\\\{Host}\\share\\x{Lf}",
                "core.excludesfile",
                BackslashShare
            },
            { $"[core]{Lf}    excludesFile = //{Host}/\\{Lf}share/x{Lf}", "core.excludesfile", SlashShare },
            { $"[CORE]{Lf}    EXCLUDESFILE = {SlashShare}{Lf}", "core.excludesfile", SlashShare },
            { $"[core] excludesFile = {SlashShare}{Lf}", "core.excludesfile", SlashShare },
            { $"[core]{Lf}    excludesFile = {SlashShare} # a comment{Lf}", "core.excludesfile", SlashShare },
            { $"[core]{CrLf}    attributesFile = {SlashShare}{CrLf}", "core.attributesfile", SlashShare },
            { $"[includeif.work]{Lf}    path = {SlashShare}{Lf}", "includeif.work.path", SlashShare },
            { $"[include]{Lf}    path = \"{SlashShare}\"{Lf}", "include.path", SlashShare },
        };
    }

    // Trigger: A hostile config writes a share the way git lets a file write a value: quoted, with escaped
    //          backslashes, split over two lines by a trailing backslash, in other case, on the line of its header,
    //          before a comment, with CRLF line breaks, or in the old section.subsection form.
    // Behavior: Open reads the value as git does, and refuses with the value as git reads it, not as it was written.
    // Assertion: An ArgumentException whose message holds .git/config, the key as git prints it, and the value read.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [MemberData(nameof(HandWrittenConfigs))]
    public void Open_ConfigWritesShareInAnotherWay_RefusesWithTheValueAsRead(string config, string key, string share)
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string configPath = Path.Combine(repo.Root, ".git", "config");
        File.AppendAllText(configPath, config);

        // Assert
        ShouldRefuse(repo.Root, configPath, key, share);
    }

    // Trigger: A hostile config names a share with a line feed and an escape sequence in it, hoping the message that
    //          refuses it breaks into lines or reaches a terminal as a command.
    // Behavior: The message prints control characters as escapes, as a failure message does for a path.
    // Assertion: The message holds the value with \n and \u{001B} written out, and holds no line feed or escape.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_ValueHoldsControlCharacters_MessageEscapesThem()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        File.AppendAllText(
            Path.Combine(repo.Root, ".git", "config"),
            $"[core]{Lf}    excludesFile = //{Host}/a\\nb\u001b[31m{Lf}");

        // Act
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(repo.Root));

        // Assert
        exception.Message.ShouldContain($"//{Host}/a\\nb\\u{{001B}}[31m");
        exception.Message.ShouldNotContain(Lf);
        exception.Message.ShouldNotContain("\u001b");
    }

    // Trigger: A developer opens a repository whose folder name holds a line break, which a Unix folder name can, and
    //          whose config names a share.
    // Behavior: The message prints the folder path escaped, so the refusal cannot split a line of a log.
    // Assertion: The message starts with the folder path with \n written out, and holds no line feed.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_FolderNameHoldsLineBreak_MessageEscapesTheFolderPath()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "A Windows folder name cannot hold a line break.");
        using TemporaryDirectory parent = TemporaryDirectory.Create();
        string folder = Path.Combine(parent.FullPath, $"a{Lf}b");
        Repository.Init(folder);
        File.AppendAllText(Path.Combine(folder, ".git", "config"), $"[core]{Lf}    excludesFile = {SlashShare}{Lf}");

        // Act
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(folder));

        // Assert
        exception.Message.ShouldStartWith($"\"{parent.FullPath}/a\\nb\" is refused: ");
        exception.Message.ShouldNotContain(Lf);
    }

    // Trigger: A hostile config names a share with a value of thousands of characters.
    // Behavior: The message shortens it, as a failure message shortens a long path, so the exception stays small.
    // Assertion: The message is under a thousand characters and holds the first characters of the value, three dots,
    //            and its last ones.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Open_ValueIsVeryLong_MessageShortensIt()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string longShare = $"//{Host}/" + new string('a', 5_000) + "/tail";
        File.AppendAllText(Path.Combine(repo.Root, ".git", "config"), $"[core]{Lf}    excludesFile = {longShare}{Lf}");

        // Act
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(repo.Root));

        // Assert
        exception.Message.Length.ShouldBeLessThan(1_000);
        // The first 100 characters are the 24 of "//gitassertions.invalid/" and 76 of a, then three dots, then the last
        // 60: 55 of a and "/tail".
        exception.Message.ShouldContain($"//{Host}/{new string('a', 76)}...{new string('a', 55)}/tail");
        exception.Message.ShouldNotContain(new string('a', 77));
    }

    // Trigger: A developer opens a repository folder that names a share in its config, with the shape of the message in
    //          mind.
    // Behavior: The message says which repository is refused, where the share is named, and why, and what to do.
    // Assertion: The message is exactly: the path, the file, the key, the value, the reason, and the advice, followed
    //            by the parameter name that .NET adds to an ArgumentException.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_ConfigKeyNamesShare_MessageSaysWhatWhyAndWhatToDo()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        await repo.GitAsync("config", "include.path", SlashShare);
        string configPath = Path.Combine(repo.Root, ".git", "config");

        // Act
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(repo.Root));

        // Assert
        exception.Message.ShouldBe(
            $"\"{repo.Root}\" is refused: {configPath} names a network path in include.path: {SlashShare}. "
            + MessageEnding
            + " (Parameter 'repoPath')");
    }

    // Trigger: A developer opens a repository whose origin remote has a URL that starts with two slashes.
    // Behavior: Open does not refuse it: the URL is no file libgit2 opens, and the checks never contact a remote.
    // Assertion: Open returns an instance whose RunAll finds the repository healthy.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_ShareOnlyInRemoteUrl_OpensTheRepository()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        await repo.GitAsync("config", "remote.origin.url", SlashShare);

        // Act
        GitAssert git = GitAssert.Open(repo.Root);

        // Assert
        git.RunAll().IsHealthy.ShouldBeTrue();
    }

    // Trigger: A repository's config includes a file that is not there.
    // Behavior: Open does not refuse it: git skips an include it cannot find, and so does the guard.
    // Assertion: Open returns an instance whose RunAll finds the repository healthy.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_IncludeFileIsMissing_OpensTheRepository()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        await repo.GitAsync("config", "include.path", "no-such-file.cfg");

        // Act
        GitAssert git = GitAssert.Open(repo.Root);

        // Assert
        git.RunAll().IsHealthy.ShouldBeTrue();
    }

    // Trigger: A developer sets core.excludesFile to a local path in its long form, as Windows writes a path longer
    //          than 260 characters.
    // Behavior: Open opens the repository: \\?\ followed by a drive letter is a local path, not a share.
    // Assertion: Open returns an instance whose RunAll finds the repository healthy.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_ExcludesFileInLongLocalForm_OpensTheRepository()
    {
        // Arrange
        Assert.SkipUnless(
            OperatingSystem.IsWindows(),
            @"\\?\C:\ignore is a Windows long path; on other systems the value is no path at all.");
        using TempRepo repo = TempRepo.Create();
        await repo.GitAsync("config", "core.excludesFile", @"\\?\C:\ignore");

        // Act
        GitAssert git = GitAssert.Open(repo.Root);

        // Assert
        git.RunAll().IsHealthy.ShouldBeTrue();
    }

    // Trigger: A developer opens the folder of a linked worktree, whose .git file names a local folder.
    // Behavior: Open follows the .git file, finds nothing that names a share, and opens the worktree.
    // Assertion: Open returns an instance whose RunAll finds the worktree healthy.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_LinkedWorktree_OpensTheWorktree()
    {
        // Arrange
        using TempRepo main = TempRepo.Create();
        using TemporaryDirectory outside = TemporaryDirectory.Create();
        string worktree = outside.GetFullPath("worktree");
        await main.GitAsync("worktree", "add", worktree, "-b", "feature");

        // Act
        GitAssert git = GitAssert.Open(worktree);

        // Assert
        git.RunAll().IsHealthy.ShouldBeTrue();
    }

    // Trigger: A developer opens the folder of a submodule, whose .git file names a folder under the parent's .git.
    // Behavior: Open follows the .git file, finds nothing that names a share, and opens the submodule.
    // Assertion: Open returns an instance whose RunAll finds the submodule healthy.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Open_SubmoduleFolder_OpensTheSubmodule()
    {
        // Arrange
        using TempRepo source = TempRepo.Create();
        using TempRepo parent = TempRepo.Create();

        // Git refuses a submodule from a local path unless the file protocol is allowed, since git 2.38.1:
        // https://git-scm.com/docs/git-config#Documentation/git-config.txt-protocolallow
        await parent.GitAsync("-c", "protocol.file.allow=always", "submodule", "add", source.Root, "sub");

        // Act
        GitAssert git = GitAssert.Open(Path.Combine(parent.Root, "sub"));

        // Assert
        git.RunAll().IsHealthy.ShouldBeTrue();
    }

    /// <summary>
    /// What every refusal must say: an <see cref="ArgumentException"/> for <c>repoPath</c> that names the file, the key
    /// and the value, and ends as <see cref="MessageEnding"/> does.
    /// </summary>
    private static void ShouldRefuse(string repoPath, string file, string key, string value)
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => GitAssert.Open(repoPath));
        exception.ParamName.ShouldBe("repoPath");
        exception.Message.ShouldContain(file);
        exception.Message.ShouldContain(key);
        exception.Message.ShouldContain(value);
        exception.Message.ShouldContain(MessageEnding);
    }
}

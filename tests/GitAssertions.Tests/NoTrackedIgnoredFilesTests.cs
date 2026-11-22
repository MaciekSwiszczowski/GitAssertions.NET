using GitAssertions.Tests.Infrastructure;

namespace GitAssertions.Tests;

/// <summary>
/// Pins <see cref="GitAssert.NoTrackedIgnoredFiles"/> against real repositories (docs/specification.md, Checks,
/// NoTrackedIgnoredFiles): a tracked file that an ignore rule matches fails the check, whether it was committed before
/// the rule or forced in past it; a folder whose every tracked file is ignored, and which is itself ignored, is listed
/// as one entry; a file a rule does not match, an ignored file nobody tracks, and a file a <c>!</c> rule brings back
/// pass. Every test is a snapshot test with no other assertion: a reviewed <c>.verified.txt</c> file beside this class
/// holds the whole result, rule, health, files, and message. A failing scenario ends in
/// <see cref="CheckResultSnapshot.Verify(CheckResult, string)"/> and has a file of its own; every healthy one ends in
/// <see cref="CheckResultSnapshot.VerifyHealthy(string, CheckResult, string)"/> and shares
/// NoTrackedIgnoredFiles.IsHealthy.verified.txt.
/// </summary>
public sealed class NoTrackedIgnoredFilesTests
{
    // Trigger: An agent finishes its work in a repository whose only tracked file is the one from the first commit.
    // Behavior: The check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files and the message
    //            "NoTrackedIgnoredFiles: OK.".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_CleanRepository_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoTrackedIgnoredFiles), result);
    }

    // Trigger: A repository ignores *.log and tracks src/app.txt, which that rule does not match.
    // Behavior: A tracked file no rule matches is the normal case, so the check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_TrackedFileNoRuleMatches_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddGitIgnore("*.log").AddFile("src/app.txt").Stage(".gitignore", "src/app.txt").Commit("Add app");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoTrackedIgnoredFiles), result);
    }

    // Trigger: A build leaves debug.log, which a committed *.log rule matches, and nobody adds it.
    // Behavior: An ignored file that Git does not track is what ignore rules are for, so the check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_IgnoredFileNobodyTracks_IsHealthy()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        repo.AddGitIgnore("*.log").Stage(".gitignore").Commit("Ignore logs").AddFile("debug.log");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoTrackedIgnoredFiles), result);
    }

    // Trigger: debug.log was committed, and a *.log rule was added to .gitignore afterwards.
    // Behavior: An ignore rule does not untrack a file Git already tracks, so the check fails, lists the file, and
    //           offers git rm --cached, which leaves it on disk.
    // Assertion: The snapshot lists debug.log in Files and the one-path message, in the singular.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_FileCommittedBeforeIgnoreRule_FailsAndListsIt()
    {
        // Arrange
        // An ignore rule only hides untracked files; a file already in the index stays tracked:
        // https://git-scm.com/docs/gitignore
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("debug.log").Stage("debug.log").Commit("Add log");
        IgnoreAfterTheFact(repo, "*.log");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: An agent forces config/local.settings.json past a *.settings.json rule with git add -f and commits it.
    // Behavior: The file is tracked although a rule matches it, so the check fails and lists it.
    // Assertion: The snapshot lists config/local.settings.json in Files and in the message.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_FileForceAddedPastIgnoreRule_FailsAndListsIt()
    {
        // Arrange
        // git add -f stages a file an ignore rule matches: https://git-scm.com/docs/git-add
        using TempRepo repo = TempRepo.Create();
        repo.AddGitIgnore("*.settings.json").Stage(".gitignore").Commit("Ignore local settings");
        repo.AddFile("config/local.settings.json").StageForced("config/local.settings.json").Commit("Force settings");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: Twelve build files under bin/ and secrets.json were committed before .gitignore listed bin/ and
    //          secrets.json.
    // Behavior: The check fails and prints exactly the spec's NoTrackedIgnoredFiles example: bin/ is one entry that
    //           counts its files, secrets.json is its own entry, and one command untracks both.
    // Assertion: The snapshot's message is the spec example character for character, and Files lists all thirteen
    //            files, not the folder.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_BuildFolderAndSecretCommittedBeforeRules_PrintsSpecExample()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string[] buildFiles =
        [
            .. new[] { "Debug", "Release" }.SelectMany(static configuration =>
                Enumerable.Range(1, 6).Select(index => $"bin/{configuration}/lib{index}.dll")),
        ];
        foreach (string path in buildFiles)
        {
            repo.AddFile(path);
        }

        repo.AddFile("secrets.json").Stage([.. buildFiles, "secrets.json"]).Commit("Add build output and secrets");
        IgnoreAfterTheFact(repo, "bin/", "secrets.json");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A project at src/App has its bin/Debug and obj output committed, before .gitignore listed bin/ and obj/.
    // Behavior: Each build folder is one entry, the topmost ignored folder (src/App/bin/, not src/App/bin/Debug/),
    //           and one file prints as "(1 file)".
    // Assertion: The snapshot lists src/App/bin/ (2 files) and src/App/obj/ (1 file), and Files holds the three
    //            files; src/App/Program.cs, which no rule matches, is not mentioned.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_BinAndObjUnderProject_GroupsEachTopmostFolder()
    {
        // Arrange
        using TempRepo repo = TempRepo.Create();
        string[] paths =
        [
            "src/App/Program.cs",
            "src/App/bin/Debug/app.dll",
            "src/App/bin/Debug/app.pdb",
            "src/App/obj/project.assets.json",
        ];
        foreach (string path in paths)
        {
            repo.AddFile(path);
        }

        repo.Stage(paths).Commit("Add project and build output");
        IgnoreAfterTheFact(repo, "bin/", "obj/");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: A repository ignores *.log but brings keep.log back with a !keep.log rule, and tracks keep.log.
    // Behavior: A negated rule un-ignores the file, so it is not a tracked ignored file and the check passes.
    // Assertion: The shared healthy snapshot: a healthy result with no files.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_FileBroughtBackByNegatedRule_IsHealthy()
    {
        // Arrange
        // A rule that starts with ! re-includes a file an earlier rule excluded: https://git-scm.com/docs/gitignore
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("keep.log").Stage("keep.log").Commit("Add log");
        IgnoreAfterTheFact(repo, "*.log", "!keep.log");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.NoTrackedIgnoredFiles), result);
    }

    // Trigger: bin/keep.txt and bin/app.dll are tracked, and .gitignore says bin/* and !bin/keep.txt.
    // Behavior: The rule matches what is inside bin/, not bin/ itself, so the folder is not one entry: grouping it
    //           would untrack keep.txt too. keep.txt passes, and app.dll is an entry of its own.
    // Assertion: The snapshot lists bin/app.dll alone, in Files, the entry, and the command; keep.txt and the folder
    //            bin/ are not mentioned.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_FolderContentsIgnoredButOneFileKept_ListsOnlyTheIgnoredFile()
    {
        // Arrange
        // "bin/*" matches the files in bin/ and not the folder, which is why "!bin/keep.txt" can bring a file back; with
        // "bin/" the folder is ignored and nothing in it can be: https://git-scm.com/docs/gitignore
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("bin/keep.txt").AddFile("bin/app.dll").Stage("bin/keep.txt", "bin/app.dll").Commit("Add bin");
        IgnoreAfterTheFact(repo, "bin/*", "!bin/keep.txt");

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: notes.tmp was committed, and the rule *.tmp sits only in .git/info/exclude, this clone's own ignore file.
    // Behavior: The check reads every ignore source git ls-files -i -c --exclude-standard reads, so it fails and lists
    //           notes.tmp.
    // Assertion: The snapshot lists notes.tmp in Files and the one-path message.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_RuleOnlyInInfoExclude_FailsAndListsFile()
    {
        // Arrange
        // .git/info/exclude holds patterns that apply to this clone only: https://git-scm.com/docs/gitignore
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("notes.tmp").Stage("notes.tmp").Commit("Add notes");
        await File.AppendAllTextAsync(
            Path.Combine(repo.Root, ".git", "info", "exclude"),
            $"*.tmp{Lf}",
            TestContext.Current.CancellationToken);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    // Trigger: cache.bak was committed, and the rule *.bak sits only in the file core.excludesFile names, the user's
    //          own ignore file for every repository.
    // Behavior: The check reads every ignore source git ls-files -i -c --exclude-standard reads, so it fails and lists
    //           cache.bak.
    // Assertion: The snapshot lists cache.bak in Files and the one-path message.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task NoTrackedIgnoredFiles_RuleOnlyInCoreExcludesFile_FailsAndListsFile()
    {
        // Arrange
        // core.excludesFile names a file of patterns that apply in every repository of the user, and is read after the
        // repository's own sources: https://git-scm.com/docs/gitignore. It is set in the repository's own config here:
        // the test process hides the machine's and the user's config, which is where it normally lives.
        using TempRepo repo = TempRepo.Create();
        repo.AddFile("cache.bak").Stage("cache.bak").Commit("Add cache");
        string excludesFile = Path.Combine(repo.Root, ".git", "global-excludes");
        await File.WriteAllTextAsync(excludesFile, $"*.bak{Lf}", TestContext.Current.CancellationToken);
        repo.Repo.Config.Set("core.excludesFile", excludesFile);

        // Act
        CheckResult result = GitAssert.Open(repo.Root).NoTrackedIgnoredFiles();

        // Assert
        await CheckResultSnapshot.Verify(result);
    }

    /// <summary>
    /// Adds the patterns to .gitignore and commits it, after the files they match were committed, so the files are
    /// tracked although a rule matches them.
    /// </summary>
    private static void IgnoreAfterTheFact(TempRepo repo, params string[] patterns)
    {
        repo.AddGitIgnore(patterns).Stage(".gitignore").Commit("Add ignore rules");
    }
}

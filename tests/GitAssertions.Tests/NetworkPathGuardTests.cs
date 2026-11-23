using Meziantou.Framework;

namespace GitAssertions.Tests;

/// <summary>
/// Pins <see cref="NetworkPathGuard"/> itself: what counts as a network path, and how far it follows the files a
/// repository brings (docs/specification.md, Security, Untrusted repositories and libgit2, Network paths). The
/// refusal as <see cref="GitAssert.Open"/> gives it is pinned in <see cref="OpenNetworkPathTests"/>. The guard here
/// reads plain folders, with no Git repository behind them, because it reads files and nothing else.
/// </summary>
public sealed class NetworkPathGuardTests
{
    private const string ShareFile = "//gitassertions.invalid/share/x";

    /// <summary>
    /// The deepest include the guard follows: git's own limit, which the spec names. The config file is depth 0, so a
    /// chain of this many includes is followed to its end and one more is not.
    /// </summary>
    private const int GitsIncludeDepth = 10;

    // Trigger: A hostile config spells the start of a share in one of the ways Windows reads as one, or as a local
    //          path.
    // Behavior: Every spelling that starts a share is a network path, on every platform, and a local path in its long
    //           form, or any path that does not start with two separators, is not.
    // Assertion: The answer is true for the share spellings and false for the local ones.
    [Theory]
    [InlineData(@"\\host\share\x", true)]
    [InlineData("//host/share/x", true)]
    [InlineData(@"\/host/share/x", true)]
    [InlineData(@"/\host\share\x", true)]
    [InlineData(@"\\?\UNC\host\share\x", true)]
    [InlineData(@"\\.\pipe\x", true)]
    [InlineData(@"\??\UNC\host\share\x", true)]
    [InlineData(@"\\?\C:\ignore", false)]
    [InlineData(@"\\?\z:\ignore", false)]
    [InlineData(@"\??\C:\ignore", false)]
    [InlineData(@"\\?\C", true)]
    [InlineData(@"\\?\", true)]
    [InlineData(@"C:\ignore", false)]
    [InlineData("/home/user/ignore", false)]
    [InlineData(@"\ignore", false)]
    [InlineData("relative/ignore", false)]
    [InlineData("~/ignore", false)]
    [InlineData("/", false)]
    [InlineData(@"\", false)]
    [InlineData("", false)]
    public void IsNetworkPath_Value_AnswersForThatSpelling(string value, bool expected)
    {
        // Assert
        NetworkPathGuard.IsNetworkPath(value).ShouldBe(expected);
    }

    // Trigger: A repository's config includes a file under the user's home folder, written with ~/, and that file
    //          names a share.
    // Behavior: The guard resolves ~/ against the home folder it is given, as git does, and finds the share in that
    //           file.
    // Assertion: The reference names the file in the home folder, core.excludesfile, and the value as written.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void FindFirst_IncludeFromHomeFolder_FollowsItAndFindsTheShare()
    {
        // Arrange
        using TemporaryDirectory repository = CreateRepositoryFolder($"[include]{Lf}path = ~/shared.cfg{Lf}");
        using TemporaryDirectory home = TemporaryDirectory.Create();
        home.CreateTextFile("shared.cfg", $"[core]{Lf}excludesFile = {ShareFile}{Lf}");

        // Act
        NetworkPathReference? found = NetworkPathGuard.FindFirst(repository.FullPath, home.FullPath);

        // Assert
        found.ShouldBe(new NetworkPathReference(
            Path.Combine(home.FullPath, "shared.cfg"),
            "core.excludesfile",
            ShareFile));
    }

    // Trigger: A repository's config includes a file that does not exist, and one that is a folder.
    // Behavior: Git skips an include it cannot read, and so does the guard: it finds nothing and does not throw.
    // Assertion: The answer is null.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void FindFirst_IncludeIsMissingOrIsAFolder_FindsNothing()
    {
        // Arrange
        using TemporaryDirectory repository = CreateRepositoryFolder(
            $"[include]{Lf}path = no-such-file.cfg{Lf}path = a-folder{Lf}path = ~/no-such-home-file.cfg{Lf}");
        repository.CreateDirectory(".git/a-folder");
        using TemporaryDirectory home = TemporaryDirectory.Create();

        // Assert
        NetworkPathGuard.FindFirst(repository.FullPath, home.FullPath).ShouldBeNull();
    }

    // Trigger: A hostile config includes files that include each other, and one that includes itself twice.
    // Behavior: Every file is read once, so a cycle ends, and what a cycle holds is still checked.
    // Assertion: With no share anywhere the answer is null; with a share in the last file of the cycle, it is found.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void FindFirst_IncludesThatCircleBack_EndsAndStillFindsTheShare()
    {
        // Arrange
        using TemporaryDirectory repository = CreateRepositoryFolder($"[include]{Lf}path = a.cfg{Lf}");
        using TemporaryDirectory home = TemporaryDirectory.Create();
        repository.CreateTextFile(".git/a.cfg", $"[include]{Lf}path = b.cfg{Lf}path = a.cfg{Lf}path = a.cfg{Lf}");
        repository.CreateTextFile(".git/b.cfg", $"[include]{Lf}path = a.cfg{Lf}path = config{Lf}");

        // Act
        NetworkPathReference? withoutShare = NetworkPathGuard.FindFirst(repository.FullPath, home.FullPath);
        repository.CreateTextFile(
            ".git/b.cfg",
            $"[include]{Lf}path = a.cfg{Lf}[core]{Lf}attributesFile = {ShareFile}{Lf}");
        NetworkPathReference? withShare = NetworkPathGuard.FindFirst(repository.FullPath, home.FullPath);

        // Assert
        withoutShare.ShouldBeNull();
        withShare.ShouldBe(new NetworkPathReference(
            Path.Combine(repository.FullPath, ".git", "b.cfg"),
            "core.attributesfile",
            ShareFile));
    }

    // Trigger: A hostile config includes a chain of files, with the share in the last one.
    // Behavior: The guard follows includes as deep as git does, ten, and no deeper: libgit2 stops there as well.
    // Assertion: A share ten includes deep is found, and one eleven deep is not.
    [Theory]
    [InlineData(1, true)]
    [InlineData(GitsIncludeDepth, true)]
    [InlineData(GitsIncludeDepth + 1, false)]
    public void FindFirst_ChainOfIncludes_FollowsToGitsDepthOfTen(int chainLength, bool expectedFound)
    {
        // Arrange
        using TemporaryDirectory repository = CreateRepositoryFolder($"[include]{Lf}path = chain1.cfg{Lf}");
        using TemporaryDirectory home = TemporaryDirectory.Create();
        for (int link = 1; link < chainLength; link++)
        {
            repository.CreateTextFile($".git/chain{link}.cfg", $"[include]{Lf}path = chain{link + 1}.cfg{Lf}");
        }

        repository.CreateTextFile($".git/chain{chainLength}.cfg", $"[core]{Lf}excludesFile = {ShareFile}{Lf}");

        // Act
        NetworkPathReference? found = NetworkPathGuard.FindFirst(repository.FullPath, home.FullPath);

        // Assert
        (found is not null).ShouldBe(expectedFound);
    }

    // Trigger: A hostile repository folder has an alternates file with a comment, a blank line, and a local folder
    //          that has an alternates file of its own, the last one naming a share.
    // Behavior: Comments and blank lines are skipped, a relative entry is resolved against the objects folder of the
    //           repository that holds the file, and every local alternate is followed.
    // Assertion: The reference names the alternates file inside the local folder, the key "alternate", and the line.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void FindFirst_AlternateFolderHasItsOwnAlternates_FollowsItAndFindsTheShare()
    {
        // Arrange
        using TemporaryDirectory repository = CreateRepositoryFolder(string.Empty);
        using TemporaryDirectory home = TemporaryDirectory.Create();
        repository.CreateTextFile(
            ".git/objects/info/alternates",
            $"# {ShareFile}{Lf}{Lf}../../other-objects{Lf}");
        repository.CreateTextFile("other-objects/info/alternates", $"{ShareFile}{Lf}");

        // Act
        NetworkPathReference? found = NetworkPathGuard.FindFirst(repository.FullPath, home.FullPath);

        // Assert
        found.ShouldBe(new NetworkPathReference(
            Path.Combine(repository.FullPath, "other-objects", "info", "alternates"),
            "alternate",
            ShareFile));
    }

    /// <summary>
    /// A folder with a <c>.git</c> folder in it and the given text as its config, and nothing else a repository has.
    /// </summary>
    private static TemporaryDirectory CreateRepositoryFolder(string config)
    {
        TemporaryDirectory folder = TemporaryDirectory.Create();
        folder.CreateTextFile(".git/config", config);
        return folder;
    }
}

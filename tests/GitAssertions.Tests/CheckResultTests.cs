namespace GitAssertions.Tests;

/// <summary>
/// Pins the result type every check returns, and the rules its message builder applies to every check alike
/// (docs/specification.md, Failure messages, Common format): the rule line, the 20-entry limit, ordinal sorting,
/// quoting a path in a command, a read-only <see cref="CheckResult.Files"/>, and the healthy result. No repository is
/// read here: every input is written by hand, under a made-up rule name. What only some checks print, such as kinds of
/// change, renames, commands without paths, sentence-only fix lines, or a failure with no entries, is tested with the
/// checks that print it, against the spec's example messages.
/// </summary>
public sealed class CheckResultTests
{
    /// <summary>
    /// The most entries one message lists (spec, Common format, Limit). Past it, the rest are counted in one line.
    /// </summary>
    private const int ListedEntryLimit = 20;

    // Trigger: A check finds nothing wrong in the repository.
    // Behavior: The result is healthy, says so in one line, and names no files.
    // Assertion: Rule reads "SomeRule", IsHealthy is true, Message is "SomeRule: OK.", Files is empty.
    [Fact]
    public void Healthy_AnyRule_ReportsRuleOkAndNoFiles()
    {
        // Act
        CheckResult result = CheckResult.Healthy("SomeRule");

        // Assert
        result.Rule.ShouldBe("SomeRule");
        result.IsHealthy.ShouldBeTrue();
        result.Message.ShouldBe("SomeRule: OK.");
        result.Files.ShouldBeEmpty();
    }

    // Trigger: A check finds one path that breaks its rule.
    // Behavior: The result is unhealthy and keeps the rule's name; the message is the rule line, the path indented two
    //           spaces, and the fix command with "--" before the path, joined by LF with no line break at the end.
    // Assertion: Rule reads "SomeRule", IsHealthy is false, Message reads exactly as below, Files holds a.txt.
    [Fact]
    public void Failing_OnePath_ReportsUnhealthyRuleLineEntryAndFix()
    {
        // Act
        CheckResult result = CheckResult.Failing(
            "SomeRule", "1 path found.", [FailureEntry.Path("a.txt")], [FixStep.WithPaths("Fix:", "git add")],
            ["a.txt"]);

        // Assert
        result.Rule.ShouldBe("SomeRule");
        result.IsHealthy.ShouldBeFalse();
        result.Message.ShouldBe(
            $"SomeRule: 1 path found.{Lf}" +
            $"  a.txt{Lf}" +
            $"Fix:{Lf}" +
            "  git add -- a.txt");
        result.Files.ShouldBe(["a.txt"]);
    }

    // Trigger: A check finds 21 paths, one more than a message lists.
    // Behavior: The message lists the first 20, counts the rest in one line, and its command fixes exactly the 20
    //           it lists; Files still holds all 21.
    // Assertion: Entries f01 to f20, then "...and 1 more", a command naming f01 to f20; Files has 21, ending in f21.
    [Fact]
    public void Failing_OneEntryOverTheLimit_ListsTwentyAndCountsTheRest()
    {
        // Arrange
        string[] paths = [.. Enumerable.Range(1, ListedEntryLimit + 1).Select(static number => $"f{number:00}")];

        // Act
        CheckResult result = CheckResult.Failing(
            "SomeRule", "21 paths found.", [.. paths.Select(FailureEntry.Path)],
            [FixStep.WithPaths("Fix:", "git add")], paths);

        // Assert
        result.Message.ShouldBe(
            $"SomeRule: 21 paths found.{Lf}" +
            $"  f01{Lf}  f02{Lf}  f03{Lf}  f04{Lf}  f05{Lf}" +
            $"  f06{Lf}  f07{Lf}  f08{Lf}  f09{Lf}  f10{Lf}" +
            $"  f11{Lf}  f12{Lf}  f13{Lf}  f14{Lf}  f15{Lf}" +
            $"  f16{Lf}  f17{Lf}  f18{Lf}  f19{Lf}  f20{Lf}" +
            $"  ...and 1 more. Run the check again after fixing these.{Lf}" +
            $"Fix:{Lf}" +
            "  git add -- f01 f02 f03 f04 f05 f06 f07 f08 f09 f10 f11 f12 f13 f14 f15 f16 f17 f18 f19 f20");
        result.Files.Count.ShouldBe(21);
        result.Files[^1].ShouldBe("f21");
    }

    // Trigger: A check finds exactly 20 paths, as many as a message lists.
    // Behavior: All 20 are listed and no "...and n more" line is added.
    // Assertion: The last entry "  f20" is followed directly by "Fix:", and the message has no "...and" line.
    [Fact]
    public void Failing_EntriesAtTheLimit_ListsAllWithoutCountLine()
    {
        // Arrange
        string[] paths = [.. Enumerable.Range(1, ListedEntryLimit).Select(static number => $"f{number:00}")];

        // Act
        CheckResult result = CheckResult.Failing(
            "SomeRule", "20 paths found.", [.. paths.Select(FailureEntry.Path)],
            [FixStep.WithPaths("Fix:", "git add")], paths);

        // Assert
        result.Message.ShouldContain($"  f20{Lf}Fix:{Lf}");
        result.Message.ShouldNotContain("...and");
    }

    // Trigger: A check finds paths whose names differ only in letter case and in a leading underscore.
    // Behavior: Entries, the command, and Files all sort ordinally, by character code, so upper case comes before
    //           lower case and the order is the same on every machine whatever its culture.
    // Assertion: Entries and the command read B.txt, _x.txt, a.txt, b.txt, and so does Files.
    [Fact]
    public void Failing_NamesDifferingInCaseAndPunctuation_SortsOrdinally()
    {
        // Arrange
        string[] paths = ["b.txt", "a.txt", "_x.txt", "B.txt"];

        // Act
        CheckResult result = CheckResult.Failing(
            "SomeRule", "4 paths found.", [.. paths.Select(FailureEntry.Path)],
            [FixStep.WithPaths("Fix:", "git add")], paths);

        // Assert
        result.Message.ShouldBe(
            $"SomeRule: 4 paths found.{Lf}" +
            $"  B.txt{Lf}" +
            $"  _x.txt{Lf}" +
            $"  a.txt{Lf}" +
            $"  b.txt{Lf}" +
            $"Fix:{Lf}" +
            "  git add -- B.txt _x.txt a.txt b.txt");
        result.Files.ShouldBe(["B.txt", "_x.txt", "a.txt", "b.txt"]);
    }

    // Trigger: A test author casts a failing result's Files to a writable list and changes a path in it.
    // Behavior: Files refuses the change, so a result stored or shared between tests keeps what the check found.
    // Assertion: Setting the first path throws NotSupportedException, and Files still reads a.txt.
    [Fact]
    public void Failing_FilesCastToWritableList_RefusesChange()
    {
        // Arrange
        CheckResult result = CheckResult.Failing(
            "SomeRule", "1 path found.", [FailureEntry.Path("a.txt")],
            [FixStep.WithPaths("Fix:", "git add")], ["a.txt"]);
        IList<string> files = (IList<string>)result.Files;

        // Assert
        Should.Throw<NotSupportedException>(() => files[0] = "changed.txt");
        result.Files.ShouldBe(["a.txt"]);
    }

    // Trigger: A check reports a path that holds a character other than an ASCII letter, a digit, ".", "_", "-", "/".
    // Behavior: The command wraps that path in double quotes, where the character is literal in bash, PowerShell and
    //           cmd alike; a path of only those characters stays bare.
    // Assertion: The command line reads exactly as given in each row.
    [Theory]
    [InlineData("src/Plain_name-1.0.cs", "  git add -- src/Plain_name-1.0.cs")]
    [InlineData("docs/my notes.md", "  git add -- \"docs/my notes.md\"")]
    [InlineData("docs/café.md", "  git add -- \"docs/café.md\"")]
    [InlineData("c++/main.cpp", "  git add -- \"c++/main.cpp\"")]
    [InlineData("a,b.txt", "  git add -- \"a,b.txt\"")]
    [InlineData("key=value.txt", "  git add -- \"key=value.txt\"")]
    [InlineData("user@host.txt", "  git add -- \"user@host.txt\"")]
    public void Failing_PathInCommand_QuotedUnlessPlain(string path, string expectedCommandLine)
    {
        // Act
        CheckResult result = CheckResult.Failing(
            "SomeRule", "1 path found.", [FailureEntry.Path(path)],
            [FixStep.WithPaths("Fix:", "git add")], [path]);

        // Assert
        result.Message.Split(LineBreak.Lf)[^1].ShouldBe(expectedCommandLine);
    }
}

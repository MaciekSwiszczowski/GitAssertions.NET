namespace GitAssertions.Tests;

/// <summary>
/// Pins what the message builder does with an unsafe file name and with a huge message (docs/specification.md,
/// Security, Shell-safe paths and Output limits), for every check alike: a name with a shell character stays out of
/// every command and is named in the note; a control or format character prints escaped, so an entry stays on one
/// line; a long path is shortened; a message past 16,000 characters is cut at a full line. No repository is read:
/// Windows cannot hold most of these names on disk (<c>"</c>, <c>*</c>, <c>|</c>, a leading <c>:</c>, a newline), so
/// every input is written by hand under a made-up rule name, the way <see cref="CheckResultTests"/> does. The names
/// Windows can hold also run through real checks, in the snapshot tests of the checks.
/// </summary>
public sealed class FailureMessageSafetyTests
{
    private const string Rule = "SomeRule";

    private const string Summary = "Some paths found.";

    /// <summary>
    /// A shell-safe path that sorts after every unsafe path of these tests, so it is the last entry and the last
    /// path of a command.
    /// </summary>
    private const string PlainPath = "z-safe.txt";

    private const string UnsafeNote =
        "Not included in the commands above because the path contains characters that are unsafe in a shell; " +
        "handle it by hand:";

    private const string TruncationLine = "  ...message truncated.";

    /// <summary>The longest path an entry prints whole (spec, Output limits); a longer one is shortened.</summary>
    private const int LongestWholePath = 200;

    private const int ShortenedHead = 100;

    private const int ShortenedTail = 60;

    /// <summary>The most characters one message holds (spec, Output limits).</summary>
    private const int MessageLimit = 16_000;

    /// <summary>A long path as an entry prints it: its first 100 characters, three dots, its last 60.</summary>
    private static readonly string Shortened = new string('a', ShortenedHead) + "..." + new string('z', ShortenedTail);

    // Trigger: A check reports a file whose name holds a character a shell runs or expands, next to a plain file.
    // Behavior: The name is listed as an entry, but no command takes it: the plain file alone is in each command, and
    //           a note after the commands names the file to handle by hand.
    // Assertion: The message reads exactly as below in every row: the name in the entries and in the note, the plain
    //            file in the entries and in the command.
    [Theory]
    [InlineData("$(touch pwned).txt")]
    [InlineData("a`touch pwned`.txt")]
    [InlineData("a;b.txt")]
    [InlineData("a&b.txt")]
    [InlineData("a|b.txt")]
    [InlineData("100%.txt")]
    [InlineData("say \"hi\".txt")]
    [InlineData("it's.txt")]
    [InlineData("*.txt")]
    [InlineData(":(top)notes.txt")]
    [InlineData("a!b.txt")]
    [InlineData("a^b.txt")]
    [InlineData("a<b.txt")]
    [InlineData("a>b.txt")]
    [InlineData("a(b).txt")]
    [InlineData("a?b.txt")]
    [InlineData("a[b].txt")]
    [InlineData("a{b}.txt")]
    [InlineData("a#b.txt")]
    [InlineData("a~b.txt")]
    public void Failing_PathWithShellCharacter_IsListedButLeftOutOfCommands(string path)
    {
        // Act
        CheckResult result = Fail(path, PlainPath);

        // Assert
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  {path}{Lf}" +
            $"  {PlainPath}{Lf}" +
            $"Fix:{Lf}" +
            $"  git add -- {PlainPath}{Lf}" +
            $"{UnsafeNote}{Lf}" +
            $"  {path}");
    }

    // Trigger: A check reports a file whose name holds a control or format character, or a backslash, next to a plain
    //          file.
    // Behavior: The entry and the note print the character as an escape, so the path stays on one line and nothing
    //           reaches a terminal: \n, \r, \t, \u{XXXX} for the rest, \\ for a backslash. The name stays out of the
    //           commands.
    // Assertion: The entry and the note read as the escaped form in each row, on one line.
    [Theory]
    [InlineData($"a{Lf}b.txt", "a\\nb.txt")]
    [InlineData($"a{Cr}b.txt", "a\\rb.txt")]
    [InlineData("a\tb.txt", "a\\tb.txt")]
    [InlineData("a\u001Bb.txt", "a\\u{001B}b.txt")]
    [InlineData("a\u007Fb.txt", "a\\u{007F}b.txt")]
    [InlineData("a‮b.txt", "a\\u{202E}b.txt")]
    [InlineData("a​b.txt", "a\\u{200B}b.txt")]
    [InlineData("a\U000E0001b.txt", "a\\u{E0001}b.txt")]
    [InlineData("a\\b.txt", "a\\\\b.txt")]
    public void Failing_PathWithControlFormatOrBackslash_PrintsEscapedOnOneLine(string path, string printed)
    {
        // Act
        CheckResult result = Fail(path, PlainPath);

        // Assert
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  {printed}{Lf}" +
            $"  {PlainPath}{Lf}" +
            $"Fix:{Lf}" +
            $"  git add -- {PlainPath}{Lf}" +
            $"{UnsafeNote}{Lf}" +
            $"  {printed}");
    }

    // Trigger: A check reports files and none of the names is shell-safe.
    // Behavior: No command line is printed, and neither is the label above it; the message is the entries and the note.
    // Assertion: The message reads exactly as below: two entries, then the note with the same two names.
    [Fact]
    public void Failing_NoPathIsShellSafe_PrintsNoCommandLinesOnlyTheNote()
    {
        // Act
        CheckResult result = CheckResult.Failing(
            Rule,
            Summary,
            [FailureEntry.Path("$(touch pwned).txt"), FailureEntry.Path("a;b.txt")],
            [FixStep.WithPaths("Fix:", "git add"), FixStep.WithPaths("Or stop tracking them:", "git rm --cached")],
            ["$(touch pwned).txt", "a;b.txt"]);

        // Assert
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  $(touch pwned).txt{Lf}" +
            $"  a;b.txt{Lf}" +
            $"{UnsafeNote}{Lf}" +
            $"  $(touch pwned).txt{Lf}" +
            "  a;b.txt");
    }

    // Trigger: A check whose fix is a command with no paths reports a file with a shell character in its name.
    // Behavior: The name is listed, and nothing was left out of any command, so there is no note: it would say a
    //           command above lacks a path, and no command above takes one.
    // Assertion: The message is the rule line, the entry, and the fix command, with no note.
    [Fact]
    public void Failing_UnsafePathAndNoCommandTakesPaths_HasNoNote()
    {
        // Act
        CheckResult result = CheckResult.Failing(
            Rule, Summary, [FailureEntry.Path("a;b.txt")], [FixStep.WithCommand("Fix:", "git commit")], ["a;b.txt"]);

        // Assert
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  a;b.txt{Lf}" +
            $"Fix:{Lf}" +
            "  git commit");
    }

    // Trigger: A check reports a folder, "$(touch pwned)/" with 3 files, next to the folder "bin/" with 12 files.
    // Behavior: The folder entry prints as it always does, and the shell character keeps its folder out of the
    //           command; the note names the folder's path, not its count.
    // Assertion: Both folders in the entries, "bin/" alone in the command, "$(touch pwned)/" alone in the note.
    [Fact]
    public void Failing_FolderWithShellCharacter_IsListedButLeftOutOfCommands()
    {
        // Act
        CheckResult result = CheckResult.Failing(
            Rule,
            Summary,
            [FailureEntry.Folder("$(touch pwned)/", 3), FailureEntry.Folder("bin/", 12)],
            [FixStep.WithPaths("Fix:", "git rm -r --cached")],
            ["$(touch pwned)/a.txt", "bin/b.txt"]);

        // Assert
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  $(touch pwned)/ (3 files){Lf}" +
            $"  bin/ (12 files){Lf}" +
            $"Fix:{Lf}" +
            $"  git rm -r --cached -- bin/{Lf}" +
            $"{UnsafeNote}{Lf}" +
            "  $(touch pwned)/");
    }

    // Trigger: A check reports a file with a newline in its name.
    // Behavior: The message prints the name escaped, but Files, for programmatic use, holds the name as it is.
    // Assertion: Files holds one path, "a", a real line break, "b.txt".
    [Fact]
    public void Failing_PathWithNewline_FilesHoldsTheRawPath()
    {
        // Act
        CheckResult result = Fail($"a{Lf}b.txt");

        // Assert
        result.Files.ShouldBe([$"a{Lf}b.txt"]);
    }

    // Trigger: A check reports 25 paths of 300 characters each, more paths than a message lists and longer than it
    //          prints.
    // Behavior: The message lists 20 of them, shortened, but Files, for programmatic use, holds every path, whole.
    // Assertion: Files holds the 25 paths in order, each as given, none shortened or left out.
    [Fact]
    public void Failing_ManyLongPaths_FilesHoldsEveryRawPath()
    {
        // Arrange
        string[] paths = [.. Enumerable.Range(0, 25).Select(static number => $"{number:00}{new string('m', 298)}")];

        // Act
        CheckResult result = Fail(paths);

        // Assert
        result.Files.ShouldBe(paths);
    }

    // Trigger: A check reports a path of 201 or of 300 characters, next to a plain file.
    // Behavior: The entry prints the first 100 characters, "...", and the last 60; a path that long is unsafe, so no
    //           command takes it and the note names it, shortened the same way.
    // Assertion: The entry and the note read as the shortened form in each row: 100 "a", "...", 60 "z".
    [Theory]
    [InlineData(LongestWholePath + 1)]
    [InlineData(300)]
    public void Failing_PathOverTheLengthLimit_IsShortenedAndLeftOutOfCommands(int length)
    {
        // Arrange
        string path = LongPath(length);

        // Act
        CheckResult result = Fail(path, PlainPath);

        // Assert
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  {Shortened}{Lf}" +
            $"  {PlainPath}{Lf}" +
            $"Fix:{Lf}" +
            $"  git add -- {PlainPath}{Lf}" +
            $"{UnsafeNote}{Lf}" +
            $"  {Shortened}");
    }

    // Trigger: A check reports a path of 202 characters whose 100th character is the first half of an emoji (U+1F600,
    //          two UTF-16 characters) and whose 60th from last is the second half of another, next to a plain file.
    // Behavior: A cut inside an emoji would print half of one, a broken character, so the left-over half is dropped:
    //           the entry keeps 99 characters at the start and 59 at the end, and the note shortens it the same way.
    // Assertion: The entry and the note read 99 "a", "...", 59 "z".
    [Fact]
    public void Failing_ShorteningCutsInsideAnEmoji_DropsTheLeftOverHalf()
    {
        // Arrange
        const string emoji = "\U0001F600";
        string path = new string('a', ShortenedHead - 1) + emoji + new string('m', 40) + emoji +
                      new string('z', ShortenedTail - 1);
        string printed = new string('a', ShortenedHead - 1) + "..." + new string('z', ShortenedTail - 1);

        // Act
        CheckResult result = Fail(path, PlainPath);

        // Assert
        path.Length.ShouldBe(202);
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  {printed}{Lf}" +
            $"  {PlainPath}{Lf}" +
            $"Fix:{Lf}" +
            $"  git add -- {PlainPath}{Lf}" +
            $"{UnsafeNote}{Lf}" +
            $"  {printed}");
    }

    // Trigger: A check reports a path of exactly 200 characters, the longest the message prints whole.
    // Behavior: The path is printed whole and goes into the command, bare, as any plain path does: no note.
    // Assertion: The entry and the command hold all 200 characters, and the message has no note.
    [Fact]
    public void Failing_PathOfExactlyTheLengthLimit_IsListedWholeAndKeptInCommands()
    {
        // Arrange
        string path = LongPath(LongestWholePath);

        // Act
        CheckResult result = Fail(path, PlainPath);

        // Assert
        result.Message.ShouldBe(
            $"{Rule}: {Summary}{Lf}" +
            $"  {path}{Lf}" +
            $"  {PlainPath}{Lf}" +
            $"Fix:{Lf}" +
            $"  git add -- {path} {PlainPath}");
    }

    // Trigger: A check reports eleven paths: ten unsafe ones whose entries print as 1,595 characters each, as long as
    //          an entry gets, and a plain one, and the message comes to exactly 16,000 characters.
    // Behavior: The message is within the limit, so nothing is cut and no marker is added.
    // Assertion: The message is 16,000 characters long, holds all eleven entries, and does not end in the marker.
    [Fact]
    public void Failing_MessageOfExactlyTheLimit_IsNotCut()
    {
        // Act
        CheckResult result = FailWithEntriesThatFillTheLimit(lastPathLength: 10);

        // Assert
        result.Message.Length.ShouldBe(MessageLimit);
        result.Message.Split(Lf).Length.ShouldBe(12);
        result.Message.ShouldNotEndWith(TruncationLine);
    }

    // Trigger: The same message comes to 16,001 characters: its last path is one character longer.
    // Behavior: The message is cut at the last full line that leaves room for the marker, which counts toward the
    //           limit: nine entries stay although a tenth would fit without the marker, and the marker ends it.
    // Assertion: The message is 14,415 characters long and has 11 lines: the rule line, nine whole entries, and
    //            "  ...message truncated." last.
    [Fact]
    public void Failing_MessageOneCharacterOverTheLimit_IsCutAtAFullLineAndMarked()
    {
        // Act
        CheckResult result = FailWithEntriesThatFillTheLimit(lastPathLength: 11);

        // Assert
        result.Message.Length.ShouldBe(14_415);
        result.Message.Split(Lf).Length.ShouldBe(11);
        result.Message.ShouldEndWith($"{Lf}{TruncationLine}");
    }

    private static CheckResult Fail(params string[] paths)
    {
        return CheckResult.Failing(
            Rule, Summary, [.. paths.Select(FailureEntry.Path)], [FixStep.WithPaths("Fix:", "git add")], paths);
    }

    /// <summary>
    /// A path of <paramref name="length"/> characters that starts with the first 100 of the shortened form, ends with
    /// the last 60, and holds plain letters between.
    /// </summary>
    private static string LongPath(int length)
    {
        return new string('a', ShortenedHead) + new string('m', length - ShortenedHead - ShortenedTail) +
               new string('z', ShortenedTail);
    }

    /// <summary>
    /// Ten paths of 199 bidirectional overrides and one letter, which print as 199 x 8 + 1 = 1,593 characters (each
    /// override is the eight characters <c>\u{202E}</c>), 1,595 with the indent, and are under the 200 characters
    /// after which a path is shortened; then one more path, of plain letters, that sorts after them. With no fix
    /// block, the message is the rule line (27 characters) and eleven lines joined by eleven line breaks: 27 + 10 x
    /// 1,595 + (2 + <paramref name="lastPathLength"/>) + 11 characters, which is 16,000 for a last path of 10.
    /// </summary>
    private static CheckResult FailWithEntriesThatFillTheLimit(int lastPathLength)
    {
        string[] paths =
        [
            .. "abcdefghij".Select(static letter => new string('‮', 199) + letter),
            new string('z', lastPathLength),
        ];
        return CheckResult.Failing(Rule, Summary, [.. paths.Select(FailureEntry.Path)], [], paths);
    }
}

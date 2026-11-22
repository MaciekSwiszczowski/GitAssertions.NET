using System.Text;

using Meziantou.Framework;

namespace GitAssertions.Tests.Infrastructure;

public sealed class GitScriptTests
{
    private const string ScriptName = "test.script.txt";

    // The line breaks of the scripts and of the files under test come from the repository's constants, never from a
    // literal, and never from Environment.NewLine where a test needs one particular break on every system.
    private const string Lf = LineBreak.Lf;

    private const string Cr = LineBreak.Cr;

    private const string CrLf = LineBreak.CrLf;

    /// <summary>A write time long before any test runs, so a rewritten file is told from an untouched one.</summary>
    private static readonly DateTime LongAgo = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Trigger: A test author writes a script line that is not an instruction, here a shell command, below a comment.
    // Behavior: Playing the script stops at that line, and the message names the script and the line and lists every
    //           instruction a line may be.
    // Assertion: An InvalidOperationException whose message is "test.script.txt line 2:", all the forms, and the line.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_UnknownInstruction_ThrowsNamingTheLine()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            PlayAsync(directory, $"# comment{Lf}rm readme.txt{Lf}"));

        // Assert
        exception.Message.ShouldBe(
            "test.script.txt line 2: expected \"git <arguments>\", \"echo <content> <path>\", "
            + "\"append <content> <path>\", \"unix2dos <path>\" or \"dos2unix <path>\", got \"rm readme.txt\".");
    }

    // Trigger: A test author writes a script line whose arguments do not fit its instruction: content of two words
    //          without quotes, a missing or empty path, one path too many, or a double quote that opens no word or
    //          closes none.
    // Behavior: Playing the script stops at that line instead of writing a file named after a stray word, and the
    //           message names the script and the line, and says what was expected.
    // Assertion: An InvalidOperationException whose message names "test.script.txt line 1:" and the row's complaint.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData(
        "git",
        "test.script.txt line 1: expected \"git <arguments>\", got \"git\".")]
    [InlineData(
        "echo two words readme.txt",
        "test.script.txt line 1: expected \"echo <content> <path>\", got \"echo two words readme.txt\". "
        + "A word with spaces goes in double quotes.")]
    [InlineData(
        "echo readme.txt",
        "test.script.txt line 1: expected \"echo <content> <path>\", got \"echo readme.txt\".")]
    [InlineData(
        "append two words readme.txt",
        "test.script.txt line 1: expected \"append <content> <path>\", got \"append two words readme.txt\". "
        + "A word with spaces goes in double quotes.")]
    [InlineData(
        "append readme.txt",
        "test.script.txt line 1: expected \"append <content> <path>\", got \"append readme.txt\".")]
    [InlineData(
        "unix2dos",
        "test.script.txt line 1: expected \"unix2dos <path>\", got \"unix2dos\".")]
    [InlineData(
        "dos2unix readme.txt notes.txt",
        "test.script.txt line 1: expected \"dos2unix <path>\", got \"dos2unix readme.txt notes.txt\". "
        + "A word with spaces goes in double quotes.")]
    [InlineData(
        "echo \"* text=auto readme.txt",
        "test.script.txt line 1: a double quote is never closed in \"echo \"* text=auto readme.txt\"; "
        + "close it, and leave double quotes out of quoted content.")]
    [InlineData(
        "echo \"say \"hi\" readme.txt",
        "test.script.txt line 1: a double quote must open or close a whole word in "
        + "\"echo \"say \"hi\" readme.txt\"; leave double quotes out of content.")]
    [InlineData(
        "echo say\"hi readme.txt",
        "test.script.txt line 1: a double quote must open or close a whole word in "
        + "\"echo say\"hi readme.txt\"; leave double quotes out of content.")]
    [InlineData(
        "echo text \"\"",
        "test.script.txt line 1: expected \"echo <content> <path>\", got \"echo text \"\"\". The path is empty.")]
    [InlineData(
        "append text \"\"",
        "test.script.txt line 1: expected \"append <content> <path>\", got \"append text \"\"\". The path is empty.")]
    [InlineData(
        "unix2dos \"\"",
        "test.script.txt line 1: expected \"unix2dos <path>\", got \"unix2dos \"\"\". The path is empty.")]
    public async Task PlayAsync_MalformedLine_ThrowsNamingTheLine(string line, string expectedMessage)
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            PlayAsync(directory, line + Lf));

        // Assert
        exception.Message.ShouldBe(expectedMessage);
    }

    // Trigger: A test author converts the line breaks of a file the script never wrote.
    // Behavior: Playing the script stops at that line, and the message names the script, the line and the file.
    // Assertion: An InvalidOperationException whose message says "readme.txt" does not exist.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("unix2dos")]
    [InlineData("dos2unix")]
    public async Task PlayAsync_ConvertingMissingFile_ThrowsNamingTheFile(string instruction)
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            PlayAsync(directory, $"{instruction} readme.txt{Lf}"));

        // Assert
        exception.Message.ShouldBe(
            $"test.script.txt line 1: \"{instruction} readme.txt\" cannot convert \"readme.txt\", "
            + "because the file does not exist.");
    }

    // Trigger: A test author echoes content into a file in a folder that does not exist yet.
    // Behavior: The folder is created, and the file holds the content and one LF line break, as shell echo writes it.
    // Assertion: The file's text is "notes" and one LF.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_EchoIntoNewFolder_WritesContentAndLineBreak()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        await PlayAsync(directory, $"echo notes tmp/notes.txt{Lf}");

        // Assert
        (await ReadTextAsync(directory, "tmp/notes.txt")).ShouldBe($"notes{Lf}");
    }

    // Trigger: A test author echoes a line with spaces, such as an attribute rule, in double quotes.
    // Behavior: The quotes group the words into one piece of content, and the file holds it, without the quotes, as
    //           one line.
    // Assertion: The file's text is "* text=auto" and one LF.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_EchoQuotedContent_WritesOneLineWithTheSpaces()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        await PlayAsync(directory, $"echo \"* text=auto\" .gitattributes{Lf}");

        // Assert
        (await ReadTextAsync(directory, ".gitattributes")).ShouldBe($"* text=auto{Lf}");
    }

    // Trigger: A test author echoes into a path with a space in its name, in double quotes.
    // Behavior: Any word may be quoted, so the quotes group the path too, and the file gets the name with the space.
    // Assertion: The file "my notes.txt" holds "notes" and one LF.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_EchoQuotedPath_WritesTheFileNamedWithTheSpace()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        await PlayAsync(directory, $"echo notes \"my notes.txt\"{Lf}");

        // Assert
        (await ReadTextAsync(directory, "my notes.txt")).ShouldBe($"notes{Lf}");
    }

    // Trigger: A test author echoes quotes with nothing between them, to write an empty line.
    // Behavior: The content is empty, and the file holds just the line break.
    // Assertion: The file's text is one LF.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_EchoEmptyQuotes_WritesOnlyTheLineBreak()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        await PlayAsync(directory, $"echo \"\" blank.txt{Lf}");

        // Assert
        (await ReadTextAsync(directory, "blank.txt")).ShouldBe(Lf);
    }

    // Trigger: A test author echoes a second time into a file the script already wrote.
    // Behavior: The file is overwritten: the old content is gone.
    // Assertion: The file's text is "second" and one LF.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_EchoIntoExistingFile_ReplacesItsContent()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        await PlayAsync(directory, $"echo first notes.txt{Lf}echo second notes.txt{Lf}");

        // Assert
        (await ReadTextAsync(directory, "notes.txt")).ShouldBe($"second{Lf}");
    }

    // Trigger: A test author adds a second ignore rule, with a "!" in it, below the first one in .gitignore.
    // Behavior: The new line goes to the end of the file after the old ones, and the shell's glob and "!" mean
    //           nothing to the script.
    // Assertion: The file's text is "bin/*" and an LF, then "!bin/keep.txt" and an LF.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_AppendToExistingFile_AddsALineAtTheEnd()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        await PlayAsync(directory, $"echo bin/* .gitignore{Lf}append !bin/keep.txt .gitignore{Lf}");

        // Assert
        (await ReadTextAsync(directory, ".gitignore")).ShouldBe($"bin/*{Lf}!bin/keep.txt{Lf}");
    }

    // Trigger: A test author appends quoted content with spaces to a file in a folder that does not exist yet.
    // Behavior: The folder and the file are created, and the file holds the content and one LF line break.
    // Assertion: The file's text is "first line" and one LF.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_AppendToMissingFile_CreatesItAndItsFolder()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();

        // Act
        await PlayAsync(directory, $"append \"first line\" docs/notes.txt{Lf}");

        // Assert
        (await ReadTextAsync(directory, "docs/notes.txt")).ShouldBe($"first line{Lf}");
    }

    // Trigger: A test author turns the line breaks of a file the script wrote into CRLF, or into LF.
    // Behavior: Every line break becomes the chosen one, whatever it was before. Nothing else changes: a lone CR is no
    //           line break, a missing final line break is not added, and the bytes of non-ASCII text stay.
    // Assertion: The file's bytes decode to the row's expected text.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("unix2dos", "LF and CRLF mixed", $"one{Lf}two{CrLf}three{Lf}", $"one{CrLf}two{CrLf}three{CrLf}")]
    [InlineData("dos2unix", "LF and CRLF mixed", $"one{CrLf}two{Lf}three{CrLf}", $"one{Lf}two{Lf}three{Lf}")]
    [InlineData("unix2dos", "no final line break", $"one{Lf}two", $"one{CrLf}two")]
    [InlineData("dos2unix", "no final line break", $"one{CrLf}two", $"one{Lf}two")]
    [InlineData("unix2dos", "lone CR", $"a{Cr}b{Lf}", $"a{Cr}b{CrLf}")]
    [InlineData("dos2unix", "lone CR", $"a{Cr}b{CrLf}", $"a{Cr}b{Lf}")]
    [InlineData("unix2dos", "CR before a CRLF", $"a{Cr}{CrLf}", $"a{Cr}{CrLf}")]
    [InlineData("dos2unix", "CR before a CRLF", $"a{Cr}{CrLf}", $"a{Cr}{Lf}")]
    [InlineData("unix2dos", "CR ending the file", $"a{Cr}", $"a{Cr}")]
    [InlineData("dos2unix", "CR ending the file", $"a{Cr}", $"a{Cr}")]
    [InlineData("unix2dos", "non-ASCII", $"zażółć{Lf}", $"zażółć{CrLf}")]
    [InlineData("dos2unix", "non-ASCII", $"zażółć{CrLf}", $"zażółć{Lf}")]
    public async Task PlayAsync_ConvertLineBreaks_ChangesOnlyTheLineBreaks(
        string instruction, string description, string content, string expected)
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();
        string path = directory.GetFullPath("readme.txt");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(content), TestContext.Current.CancellationToken);

        // Act
        await PlayAsync(directory, $"{instruction} readme.txt{Lf}");

        // Assert
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(bytes).ShouldBe(expected, description);
    }

    // Trigger: A test author converts a UTF-8 file that starts with a BOM, as an editor on Windows saves it.
    // Behavior: The BOM stays the first bytes of the file, and only the line break changes.
    // Assertion: The file's bytes are the BOM, "zażółć", and the other line break.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("unix2dos", $"zażółć{Lf}", $"zażółć{CrLf}")]
    [InlineData("dos2unix", $"zażółć{CrLf}", $"zażółć{Lf}")]
    public async Task PlayAsync_ConvertFileWithBom_KeepsTheBom(string instruction, string content, string expected)
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();
        byte[] bom = Encoding.UTF8.GetPreamble();
        string path = directory.GetFullPath("readme.txt");
        await File.WriteAllBytesAsync(
            path, [.. bom, .. Encoding.UTF8.GetBytes(content)], TestContext.Current.CancellationToken);

        // Act
        await PlayAsync(directory, $"{instruction} readme.txt{Lf}");

        // Assert
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        bytes.ShouldBe([.. bom, .. Encoding.UTF8.GetBytes(expected)]);
    }

    // Trigger: A test author converts a file that already has the line breaks asked for, as a person saves a file
    //          unchanged, so git has to read it again.
    // Behavior: The file is rewritten all the same, and gets a new write time.
    // Assertion: The write time is no longer the one the file had before the script.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("unix2dos", $"one{CrLf}two{CrLf}")]
    [InlineData("dos2unix", $"one{Lf}two{Lf}")]
    public async Task PlayAsync_ConvertFileAlreadyConverted_RewritesIt(string instruction, string content)
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();
        string path = directory.GetFullPath("readme.txt");
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(path, LongAgo);

        // Act
        await PlayAsync(directory, $"{instruction} readme.txt{Lf}");

        // Assert
        File.GetLastWriteTimeUtc(path).ShouldBeGreaterThan(LongAgo);
    }

    private static async Task PlayAsync(TemporaryDirectory directory, string script)
    {
        await directory.CreateTextFileAsync(ScriptName, script, TestContext.Current.CancellationToken);
        await GitScript.PlayAsync(directory.GetFullPath(ScriptName), directory.FullPath);
    }

    private static Task<string> ReadTextAsync(TemporaryDirectory directory, string relativePath)
    {
        return File.ReadAllTextAsync(directory.GetFullPath(relativePath), TestContext.Current.CancellationToken);
    }
}

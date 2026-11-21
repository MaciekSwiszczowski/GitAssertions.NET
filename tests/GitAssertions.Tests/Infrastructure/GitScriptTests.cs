using Meziantou.Framework;

namespace GitAssertions.Tests.Infrastructure;

public sealed class GitScriptTests
{
    // Trigger: A test author writes a script line that is neither a git command nor an echo, here a shell command.
    // Behavior: Playing the script stops at that line, and the message names the script and the line and says what a
    //           line may be.
    // Assertion: An InvalidOperationException whose message is "bad.script.txt line 2:", both forms, and the line.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_LineNeitherGitNorEcho_ThrowsNamingTheLine()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();
        await directory.CreateTextFileAsync(
            "bad.script.txt", "# comment\nrm readme.txt\n", TestContext.Current.CancellationToken);
        string script = directory.GetFullPath("bad.script.txt");

        // Act
        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(GitScript.PlayAsync(script, directory.FullPath));

        // Assert
        exception.Message.ShouldBe(
            "bad.script.txt line 2: expected \"git <arguments>\" or \"echo <content> <path>\", got \"rm readme.txt\".");
    }

    // Trigger: A test author writes an echo line with content of two words, which the script cannot tell from a path.
    // Behavior: Playing the script stops at that line instead of writing a file named after the second word.
    // Assertion: An InvalidOperationException whose message names the line and the expected form.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_EchoWithoutThreeTokens_ThrowsNamingTheLine()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();
        await directory.CreateTextFileAsync(
            "bad.script.txt", "echo two words readme.txt\n", TestContext.Current.CancellationToken);
        string script = directory.GetFullPath("bad.script.txt");

        // Act
        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(GitScript.PlayAsync(script, directory.FullPath));

        // Assert
        exception.Message.ShouldBe(
            "bad.script.txt line 1: expected \"echo <content> <path>\", got \"echo two words readme.txt\".");
    }

    // Trigger: A test author writes an echo line for a file in a folder that does not exist yet.
    // Behavior: The folder is created, and the file holds the content and one LF line break, as shell echo writes it.
    // Assertion: The file's text is exactly "notes\n".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task PlayAsync_EchoIntoNewFolder_WritesContentAndLineBreak()
    {
        // Arrange
        await using TemporaryDirectory directory = TemporaryDirectory.Create();
        await directory.CreateTextFileAsync(
            "echo.script.txt", "echo notes tmp/notes.txt\n", TestContext.Current.CancellationToken);
        string script = directory.GetFullPath("echo.script.txt");

        // Act
        await GitScript.PlayAsync(script, directory.FullPath);

        // Assert
        (await File.ReadAllTextAsync(directory.GetFullPath("tmp/notes.txt"), TestContext.Current.CancellationToken))
            .ShouldBe("notes\n");
    }
}

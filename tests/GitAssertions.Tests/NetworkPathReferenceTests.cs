namespace GitAssertions.Tests;

/// <summary>
/// Pins the one-line message of <see cref="NetworkPathReference"/>: the folder path the caller passed to
/// <see cref="GitAssert.Open"/> and the repository's own words are all printed escaped, so none of them can split a
/// log line or reach a terminal as a control sequence. How <see cref="GitAssert.Open"/> uses the message is pinned in
/// <see cref="OpenNetworkPathTests"/>.
/// </summary>
public sealed class NetworkPathReferenceTests
{
    // Trigger: A developer opens a repository whose folder name holds a line break and a terminal escape sequence, as a
    //          Unix folder name can, and its config names a share.
    // Behavior: The message prints the folder path with control characters as escapes, like the file, key and value.
    // Assertion: The message starts with the path with \n and \u{001B} written out, and holds no line feed or escape.
    [Fact]
    public void ToMessage_RepoPathHoldsLineBreakAndEscape_PrintsThemEscaped()
    {
        // Arrange
        NetworkPathReference reference = new(
            "/work/a/.git/config",
            "core.excludesfile",
            "//gitassertions.invalid/share/x");

        // Act
        string message = reference.ToMessage($"/work/a{Lf}b\u001b[31m");

        // Assert
        message.ShouldStartWith(
            "\"/work/a\\nb\\u{001B}[31m\" is refused: /work/a/.git/config names a network path in ");
        message.ShouldNotContain(Lf);
        message.ShouldNotContain("\u001b");
    }
}

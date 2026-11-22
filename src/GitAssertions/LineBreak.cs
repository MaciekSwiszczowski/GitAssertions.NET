namespace GitAssertions;

/// <summary>
/// The line break for text that breaks its lines the same way on every operating system, where
/// <see cref="Environment.NewLine"/> cannot be used: it is <c>\r\n</c> on Windows and <c>\n</c> elsewhere.
/// <list type="bullet">
/// <item>A check's message: its lines are joined by <see cref="Lf"/> on every system (docs/specification.md, Failure
/// messages, Line endings), so a message, and the snapshot that pins it, reads the same character for character
/// everywhere.</item>
/// <item>The git CLI's output, which the tests read: git ends every line it prints with <see cref="Lf"/>, also on
/// Windows. Split on <see cref="Environment.NewLine"/> there, the whole output stays one line, so a test that reads
/// lines finds none, or checks only the first.</item>
/// </list>
/// Text that belongs to the operating system, such as a file a test writes for git to read, keeps
/// <see cref="Environment.NewLine"/>.
/// </summary>
internal static class LineBreak
{
    public const string Lf = "\n";

    /// <summary>
    /// A carriage return on its own, for text that holds one on every system, such as a file a test writes to see
    /// that a tool leaves it alone.
    /// </summary>
    public const string Cr = "\r";

    /// <summary>
    /// A CRLF line break, for text that holds it on every system, such as a Windows-style file a test writes on Linux,
    /// where <see cref="Environment.NewLine"/> is <see cref="Lf"/>.
    /// </summary>
    public const string CrLf = Cr + Lf;
}

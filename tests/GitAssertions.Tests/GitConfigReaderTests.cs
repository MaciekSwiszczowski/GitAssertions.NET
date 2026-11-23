namespace GitAssertions.Tests;

/// <summary>
/// Pins how <see cref="GitConfigReader"/> reads the text of a git config file: the part of the format
/// (https://git-scm.com/docs/git-config#_syntax) that the network-path refusal of <see cref="GitAssert.Open"/> depends
/// on. A value the reader reads differently from libgit2 must only ever make <c>Open</c> refuse more, never less, so
/// each row is one way a hostile file can spell a value.
/// </summary>
public sealed class GitConfigReaderTests
{
    /// <summary>
    /// The rows of <see cref="Read_ConfigText_ReturnsEntriesInOrder"/>: what is written and what is read.
    /// </summary>
    public static TheoryData<string, string, string[]> Configs()
    {
        return new TheoryData<string, string, string[]>
        {
            { "a variable under its section", $"[core]{Lf}\tbare = false{Lf}", ["core.bare=false"] },
            {
                "section and variable names fold to lower case",
                $"[CORE]{Lf}ExcludesFile = x{Lf}",
                ["core.excludesfile=x"]
            },
            {
                "a quoted subsection keeps its case and joins the name",
                $"[includeIf \"gitdir:~/Work/\"]{Lf}path = x{Lf}",
                ["includeif.gitdir:~/Work/.path=x"]
            },
            {
                "the old section.subsection form folds to lower case",
                $"[Remote.Origin]{Lf}URL = u{Lf}",
                ["remote.origin.url=u"]
            },
            {
                "a backslash and a quote in a subsection stand for themselves",
                $@"[includeIf ""gitdir:C:\\Work\""x""]{Lf}path = y{Lf}",
                [@"includeif.gitdir:C:\Work""x.path=y"]
            },
            {
                "whitespace between the header's name and its subsection",
                $"[includeIf \t \"x\"]{Lf}path = y{Lf}",
                ["includeif.x.path=y"]
            },
            { "a quoted value", $"[core]{Lf}x = \"a b\"{Lf}", ["core.x=a b"] },
            { "quotes that start inside a value", $"[core]{Lf}x = a \"b c\" d{Lf}", ["core.x=a b c d"] },
            {
                "space around a value goes, and space inside it stays",
                $"[core]{Lf}x =   a   b  {Lf}",
                ["core.x=a   b"]
            },
            { "space inside quotes is kept at both ends", $"[core]{Lf}x = \" a \"{Lf}", ["core.x= a "] },
            { "an equals sign inside a value", $"[core]{Lf}x = a=b{Lf}", ["core.x=a=b"] },
            { "an empty value", $"[core]{Lf}x ={Lf}", ["core.x="] },
            { "a variable with no value", $"[core]{Lf}x{Lf}", ["core.x"] },
            {
                "an escaped backslash reads as one",
                $@"[core]{Lf}x = \\\\gitassertions.invalid\\share{Lf}",
                [@"core.x=\\gitassertions.invalid\share"]
            },
            { "an escaped quote", $@"[core]{Lf}x = a\""b{Lf}", ["core.x=a\"b"] },
            { "an escaped line feed", $@"[core]{Lf}x = a\nb{Lf}", [$"core.x=a{Lf}b"] },
            { "an escaped tab", $@"[core]{Lf}x = a\tb{Lf}", ["core.x=a\tb"] },
            { "an escaped backspace", $@"[core]{Lf}x = a\bb{Lf}", ["core.x=a\bb"] },
            { "a backslash before a line break continues the value", $"[core]{Lf}x = a\\{Lf}b{Lf}", ["core.x=ab"] },
            {
                "a backslash before a CRLF continues the value",
                $"[core]{CrLf}x = a\\{CrLf}b{CrLf}",
                ["core.x=ab"]
            },
            {
                "the continued line keeps its leading space",
                $"[core]{Lf}x = a\\{Lf}  b{Lf}",
                ["core.x=a  b"]
            },
            { "a continuation inside quotes", $"[core]{Lf}x = \"a\\{Lf}b\"{Lf}", ["core.x=ab"] },
            { "a continuation at the end of the file", $"[core]{Lf}x = a\\", ["core.x=a"] },
            { "a hash comment after a value", $"[core]{Lf}x = a # b{Lf}", ["core.x=a"] },
            { "a semicolon comment after a value", $"[core]{Lf}x = a ; b{Lf}", ["core.x=a"] },
            { "comment characters inside quotes", $"[core]{Lf}x = \"a # b ; c\"{Lf}", ["core.x=a # b ; c"] },
            {
                "comment lines hold no variable",
                $"[core]{Lf}# x = 1{Lf}; y = 2{Lf}  # z = 3{Lf}w = 4{Lf}",
                ["core.w=4"]
            },
            { "a variable on the line of its header", $"[core] x = 1{Lf}", ["core.x=1"] },
            { "a comment on the line of a header", $"[core] # a{Lf}x = 1{Lf}", ["core.x=1"] },
            { "a name with a digit and a dash", $"[core]{Lf}a-b1 = v{Lf}", ["core.a-b1=v"] },
            { "a file with CRLF line breaks", $"[core]{CrLf}x = 1{CrLf}y = 2{CrLf}", ["core.x=1", "core.y=2"] },
            { "a last line with no line break", $"[core]{Lf}x = 1", ["core.x=1"] },
            {
                "two sections, each variable under its own",
                $"[a]{Lf}x = 1{Lf}[b \"s\"]{Lf}y = 2{Lf}[a]{Lf}z = 3{Lf}",
                ["a.x=1", "b.s.y=2", "a.z=3"]
            },
            {
                "a line that is nothing is skipped",
                $"[core]{Lf}%^${Lf}x = 1{Lf}",
                ["core.x=1"]
            },
            { "a variable before any header is dropped", $"x = 1{Lf}[core]{Lf}y = 2{Lf}", ["core.y=2"] },
            { "an unterminated header holds no variable", $"[core{Lf}x = 1{Lf}", [] },
            { "an empty file", string.Empty, [] },
        };
    }

    // Trigger: A repository folder brings a config file written in any of the ways git accepts.
    // Behavior: The reader returns every variable with its full name, folded as git folds it, and its value as git
    //           reads it: after the quoting, the escapes, the continued lines, and the comments.
    // Assertion: The entries read are the expected ones, in the order they were written, each as name=value.
    [Theory]
    [MemberData(nameof(Configs))]
    public void Read_ConfigText_ReturnsEntriesInOrder(string description, string text, string[] expected)
    {
        // Act
        IReadOnlyList<GitConfigEntry> entries = GitConfigReader.Read(text);

        // Assert
        entries.Select(static entry => entry.Value is null ? entry.Name : $"{entry.Name}={entry.Value}")
            .ShouldBe(expected, customMessage: description);
    }
}

namespace GitAssertions;

/// <summary>
/// One block of the fix part of a failure message: a label line, such as <c>Fix:</c> or <c>Or delete them:</c>, and,
/// unless the block is only a sentence, one command line under it. <see cref="FailureMessage"/> prints the blocks in
/// the order the check gives them.
/// </summary>
internal sealed class FixStep
{
    private FixStep(string label, string? command, bool takesPaths)
    {
        Label = label;
        Command = command;
        TakesPaths = takesPaths;
    }

    /// <summary>The line before the command, or the whole block when there is no command.</summary>
    public string Label { get; }

    /// <summary>The command, without the paths; null when the block is only a sentence.</summary>
    public string? Command { get; }

    /// <summary>True when the paths of the listed entries are appended to <see cref="Command"/>.</summary>
    public bool TakesPaths { get; }

    /// <summary>
    /// A command that acts on the listed entries: <paramref name="command"/>, then <c>--</c>, then their paths, so no
    /// path can be read as an option. <c>WithPaths("Fix:", "git add")</c> prints <c>git add -- a.txt b.txt</c>.
    /// </summary>
    public static FixStep WithPaths(string label, string command)
    {
        return new FixStep(label, command, takesPaths: true);
    }

    /// <summary>A command printed as given, with no paths: <c>git commit</c>, <c>git merge --abort</c>.</summary>
    public static FixStep WithCommand(string label, string command)
    {
        return new FixStep(label, command, takesPaths: false);
    }

    /// <summary>
    /// A sentence with no command line, for a fix no single command does, such as
    /// <c>Or ignore them: add the paths to .gitignore.</c>
    /// </summary>
    public static FixStep TextOnly(string text)
    {
        return new FixStep(text, command: null, takesPaths: false);
    }
}

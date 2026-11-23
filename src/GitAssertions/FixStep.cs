namespace GitAssertions;

/// <summary>
/// One block of the fix part of a failure message: a label line, such as <c>Fix:</c> or <c>Or ignore them:</c>, and,
/// unless the block is only a sentence, one command line under it. <see cref="FailureMessage"/> prints the blocks in
/// the order the check gives them.
/// </summary>
internal sealed class FixStep
{
    private readonly Func<FailureEntry, bool>? _appliesTo;

    private FixStep(string label, string? command, bool takesPaths, Func<FailureEntry, bool>? appliesTo = null)
    {
        Label = label;
        Command = command;
        TakesPaths = takesPaths;
        _appliesTo = appliesTo;
    }

    /// <summary>The line before the command, or the whole block when there is no command.</summary>
    public string Label { get; }

    /// <summary>The command, without the paths; null when the block is only a sentence.</summary>
    public string? Command { get; }

    /// <summary>True when the paths of the listed entries are appended to <see cref="Command"/>.</summary>
    public bool TakesPaths { get; }

    /// <summary>
    /// True when the step is right for <paramref name="entry"/>: its paths go into <see cref="Command"/>. Every entry,
    /// unless the step was made with a filter: a command that is wrong for one kind of entry leaves those out.
    /// </summary>
    public bool AppliesTo(FailureEntry entry)
    {
        return _appliesTo is null || _appliesTo(entry);
    }

    /// <summary>
    /// True when the step is right for at least one of <paramref name="entries"/>, and always when the step was made
    /// with no filter. A step that takes no paths is left out of the message when this is false.
    /// </summary>
    public bool AppliesToAny(IEnumerable<FailureEntry> entries)
    {
        return _appliesTo is null || entries.Any(_appliesTo);
    }

    /// <summary>
    /// A command that acts on the listed entries: <paramref name="command"/>, then <c>--</c>, then their paths, so no
    /// path can be read as an option. <c>WithPaths("Fix:", "git add")</c> prints <c>git add -- a.txt b.txt</c>.
    /// </summary>
    /// <param name="label">The line before the command.</param>
    /// <param name="command">The command, without the paths.</param>
    /// <param name="appliesTo">
    /// Which entries' paths the command takes; null takes every entry's. When no listed entry is taken, the whole
    /// block, label included, is left out of the message.
    /// </param>
    public static FixStep WithPaths(string label, string command, Func<FailureEntry, bool>? appliesTo = null)
    {
        return new FixStep(label, command, takesPaths: true, appliesTo);
    }

    /// <summary>A command printed as given, with no paths: <c>git commit</c>, <c>git merge --abort</c>.</summary>
    public static FixStep WithCommand(string label, string command)
    {
        return new FixStep(label, command, takesPaths: false);
    }

    /// <summary>
    /// A sentence with no command line, for a fix no single command does, such as
    /// <c>Or ignore them: add the paths to .gitignore.</c>, and for a fix that cannot be undone, such as deleting
    /// untracked files, which the message says in words and never gives as a command (spec, Common format).
    /// </summary>
    /// <param name="text">The whole sentence.</param>
    /// <param name="appliesTo">
    /// Which entries the sentence is right for; null means every entry. When none of the entries is, the sentence is
    /// left out of the message.
    /// </param>
    public static FixStep TextOnly(string text, Func<FailureEntry, bool>? appliesTo = null)
    {
        return new FixStep(text, command: null, takesPaths: false, appliesTo);
    }
}

using System.Buffers;
using System.Globalization;

namespace GitAssertions;

/// <summary>
/// Prints a failing check's message in the spec's common format (docs/specification.md, Failure messages, Common
/// format), so every check reads the same way: the rule line; the entries, sorted ordinally by their whole line and
/// at most <see cref="ListedEntryLimit"/> of them; then the fix blocks, whose commands name the listed entries they
/// are right for (<see cref="FixStep.TakesEntry"/>), every one unless the step says otherwise. Lines are joined by
/// <see cref="LineBreak.Lf"/>, with no line break at the end.
/// </summary>
internal static class FailureMessage
{
    /// <summary>The most entries one message lists. The rest are counted in one line; Files keeps them all.</summary>
    private const int ListedEntryLimit = 20;

    private const string Indent = "  ";

    /// <summary>
    /// The characters a path may hold and still go into a command bare. A path with any other character, such as a
    /// space, a non-ASCII letter, or <c>+ , = @</c>, is wrapped in double quotes, where those are literal in bash,
    /// PowerShell and cmd alike.
    /// </summary>
    private static readonly SearchValues<char> BareCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789._-/");

    public static string Format(
        string rule,
        string summary,
        IReadOnlyList<FailureEntry> entries,
        IReadOnlyList<FixStep> fixes)
    {
        // Sorted by the whole line, kind first, so the entries of one kind stay together; the commands take the
        // paths in this same order.
        (string Line, FailureEntry Entry)[] listed =
        [
            .. entries
                .Select(static entry => (Line: EntryLine(entry), Entry: entry))
                .OrderBy(static entry => entry.Line, StringComparer.Ordinal)
                .Take(ListedEntryLimit),
        ];

        List<string> lines = [$"{rule}: {summary}"];
        lines.AddRange(listed.Select(static entry => Indent + entry.Line));

        int unlisted = entries.Count - listed.Length;
        if (unlisted > 0)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}...and {unlisted} more. Run the check again after fixing these."));
        }

        foreach (FixStep fix in fixes)
        {
            if (!fix.TakesPaths)
            {
                lines.Add(fix.Label);
                if (fix.Command is not null)
                {
                    lines.Add(Indent + fix.Command);
                }

                continue;
            }

            // A command takes the paths of the listed entries it is right for. With none left it is not printed at
            // all, label included: a label over no command would offer a fix that does nothing.
            string commandPaths = string.Join(
                ' ',
                listed.Where(entry => fix.TakesEntry(entry.Entry))
                    .SelectMany(static entry => entry.Entry.Paths)
                    .Select(QuoteForCommand));
            if (commandPaths.Length > 0)
            {
                lines.Add(fix.Label);
                lines.Add($"{Indent}{fix.Command} -- {commandPaths}");
            }
        }

        return string.Join(LineBreak.Lf, lines);
    }

    private static string EntryLine(FailureEntry entry)
    {
        string paths = entry.Label ?? string.Join(" -> ", entry.Paths);
        return entry.Kind is null ? paths : $"{entry.Kind}: {paths}";
    }

    private static string QuoteForCommand(string path)
    {
        return path.AsSpan().ContainsAnyExcept(BareCharacters) ? $"\"{path}\"" : path;
    }
}

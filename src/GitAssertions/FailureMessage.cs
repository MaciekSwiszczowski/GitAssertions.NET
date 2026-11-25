using System.Buffers;
using System.Globalization;
using System.Text;

namespace GitAssertions;

/// <summary>
/// Prints a failing check's message in the spec's common format (docs/specification.md, Failure messages, Common
/// format), so every check reads the same way: the rule line; the entries, sorted ordinally by their whole line as
/// printed and at most <see cref="ListedEntryLimit"/> of them; then the fix blocks, whose commands name the listed
/// entries they are right for (<see cref="FixStep.AppliesTo"/>), every one unless the step says otherwise, and a
/// sentence only when it is right for some entry; then, when a command left a path out, the note that names it. Lines
/// are joined by <see cref="LineBreak.Lf"/>, with no line break at the end.
/// <para>
/// Paths are the only untrusted input; the rest of a message is fixed text (spec, Security, Shell-safe paths). A person
/// or an agent may paste a fix command into a shell, and file names can come from outside the repository's commits,
/// such as a download, an unpacked archive, or generated output, so a path goes into a command only when it is
/// shell-safe, see <see cref="IsShellSafe"/>. Even ordinary names hold spaces and non-ASCII letters, so a command
/// quotes them, see <see cref="QuoteForCommand"/>. Wherever a path is printed outside a command, it is shortened when
/// it is long and its control and format characters are escaped, see <see cref="Display"/>. A message is cut at
/// <see cref="MessageLimit"/> characters.
/// </para>
/// </summary>
internal static class FailureMessage
{
    /// <summary>The most entries one message lists. The rest are counted in one line; Files keeps them all.</summary>
    private const int ListedEntryLimit = 20;

    /// <summary>The most characters one message holds, the marker line included (spec, Output limits).</summary>
    private const int MessageLimit = 16_000;

    /// <summary>
    /// The longest path an entry prints whole, and the longest that can be shell-safe. A longer path is shortened to
    /// <see cref="ShortenedHeadLength"/> characters, three dots, and <see cref="ShortenedTailLength"/> characters.
    /// </summary>
    private const int LongestWholePath = 200;

    private const int ShortenedHeadLength = 100;

    private const int ShortenedTailLength = 60;

    private const string Indent = "  ";

    private const string TruncationLine = Indent + "...message truncated.";

    private const string UnsafePathNote =
        "Not included in the commands above because the path contains characters that are unsafe in a shell; " +
        "handle it by hand:";

    private const char Backslash = '\\';

    /// <summary>
    /// The characters a path may hold and still go into a command bare. A path with any other character, such as a
    /// space, a non-ASCII letter, or <c>+ , = @</c>, is wrapped in double quotes, where those are literal in bash,
    /// PowerShell and cmd alike.
    /// </summary>
    private static readonly SearchValues<char> BareCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789._-/");

    /// <summary>
    /// The characters other than letters and digits that a shell-safe path may hold (spec, Security, Shell-safe
    /// paths). Every other character is unsafe, the colon of git's pathspec magic, such as <c>:(top)</c>, among them.
    /// </summary>
    private static readonly SearchValues<char> SafePunctuation = SearchValues.Create(" ._-/+,=@");

    public static string Format(
        string rule,
        string summary,
        IReadOnlyList<FailureEntry> entries,
        IReadOnlyList<FixStep> fixes)
    {
        // Sorted by the whole line as printed, kind first, so the entries of one kind stay together; the commands take
        // the paths in this same order. Entries that print alike are long paths, which no command takes, so their
        // order among themselves cannot be seen.
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
                // A sentence for some kinds of entry only is printed when one of them is among the entries, all of
                // them, not the 20 listed: what the sentence says holds for the check's whole finding.
                if (fix.AppliesToAny(entries))
                {
                    lines.Add(fix.Label);
                    if (fix.Command is not null)
                    {
                        lines.Add(Indent + fix.Command);
                    }
                }

                continue;
            }

            // A command takes the paths of the listed entries it is right for, and of those only the shell-safe ones.
            // With none left it is not printed at all, label included: a label over no command would offer a fix that
            // does nothing.
            string commandPaths = string.Join(
                ' ',
                listed.Where(entry => fix.AppliesTo(entry.Entry))
                    .SelectMany(static entry => entry.Entry.Paths)
                    .Where(IsShellSafe)
                    .Select(QuoteForCommand));
            if (commandPaths.Length > 0)
            {
                lines.Add(fix.Label);
                lines.Add($"{Indent}{fix.Command} -- {commandPaths}");
            }
        }

        AddUnsafePathNote(lines, listed.Select(static entry => entry.Entry), fixes);
        return Limit(lines);
    }

    /// <summary>
    /// The note after the commands, with each path that a command would have taken and was not shell-safe. A path no
    /// command takes, because the check's fixes have no paths in them, is not named: the note says what is missing
    /// from the commands above, and nothing is.
    /// </summary>
    private static void AddUnsafePathNote(
        List<string> lines,
        IEnumerable<FailureEntry> listed,
        IReadOnlyList<FixStep> fixes)
    {
        string[] unsafePaths =
        [
            .. listed
                .Where(entry => fixes.Any(fix => fix.TakesPaths && fix.AppliesTo(entry)))
                .SelectMany(static entry => entry.Paths)
                .Where(static path => !IsShellSafe(path))
                .Select(static path => Indent + Display(path)),
        ];

        if (unsafePaths.Length > 0)
        {
            lines.Add(UnsafePathNote);
            lines.AddRange(unsafePaths);
        }
    }

    /// <summary>
    /// The lines joined, or, when that is more than <see cref="MessageLimit"/> characters, the lines that fit followed
    /// by <see cref="TruncationLine"/>. The marker counts toward the limit, so a line that would fit without it is
    /// dropped when it would push the marker over.
    /// </summary>
    private static string Limit(List<string> lines)
    {
        string message = string.Join(LineBreak.Lf, lines);
        if (message.Length <= MessageLimit)
        {
            return message;
        }

        // Each kept line is followed by a line break, because the marker line comes after it, so a line costs its
        // length plus one break. The predicate keeps a running total, which is right because string.Join walks the
        // sequence exactly once.
        int room = MessageLimit - TruncationLine.Length;
        int used = 0;
        IEnumerable<string> kept = lines.TakeWhile(line => (used += line.Length + LineBreak.Lf.Length) <= room);
        return string.Join(LineBreak.Lf, kept.Append(TruncationLine));
    }

    /// <summary>
    /// The entry as printed: its kind, then its paths shortened and escaped, then its detail, such as the file count of
    /// a folder.
    /// </summary>
    private static string EntryLine(FailureEntry entry)
    {
        string paths = string.Join(" -> ", entry.Paths.Select(Display));
        string line = entry.Detail is null ? paths : $"{paths} {entry.Detail}";
        return entry.Kind is null ? line : $"{entry.Kind}: {line}";
    }

    /// <summary>
    /// True when <paramref name="path"/> is safe to paste into bash, PowerShell and cmd: it is no longer than
    /// <see cref="LongestWholePath"/>, and every character is a Unicode letter or digit or one of
    /// <see cref="SafePunctuation"/>. Control and format characters, quotes, and every shell operator fail it. No
    /// single escaping is right for all three shells, so a path that fails is never quoted or escaped into a command.
    /// </summary>
    private static bool IsShellSafe(string path)
    {
        if (path.Length > LongestWholePath)
        {
            return false;
        }

        // A letter outside the Basic Multilingual Plane is two chars, so the test is on the whole rune. A lone
        // surrogate, which is not text, decodes to the replacement character, which is no letter: the path is unsafe.
        ReadOnlySpan<char> rest = path;
        while (!rest.IsEmpty)
        {
            _ = Rune.DecodeFromUtf16(rest, out Rune rune, out int consumed);
            if (!Rune.IsLetterOrDigit(rune) && !(rune.IsBmp && SafePunctuation.Contains((char)rune.Value)))
            {
                return false;
            }

            rest = rest[consumed..];
        }

        return true;
    }

    /// <summary>
    /// The path as printed outside a command: shortened when longer than <see cref="LongestWholePath"/>, then every
    /// control and format character as an escape, <c>\n</c>, <c>\r</c>, <c>\t</c>, or <c>\u{XXXX}</c> with the hex code
    /// point, and a backslash as two. One path then stays on one line, and no escape sequence reaches a terminal. The
    /// shortening comes first, so it never cuts an escape in two.
    /// </summary>
    private static string Display(string path)
    {
        string shown = path.Length > LongestWholePath ? Shorten(path) : path;
        StringBuilder text = new(shown.Length);
        ReadOnlySpan<char> rest = shown;
        while (!rest.IsEmpty)
        {
            _ = Rune.DecodeFromUtf16(rest, out Rune rune, out int consumed);
            AppendEscaped(text, rune);
            rest = rest[consumed..];
        }

        return text.ToString();
    }

    private static string Shorten(string path)
    {
        ReadOnlySpan<char> head = path.AsSpan(0, ShortenedHeadLength);
        ReadOnlySpan<char> tail = path.AsSpan(path.Length - ShortenedTailLength);

        // The cut may fall inside a surrogate pair, a character outside the Basic Multilingual Plane such as an emoji;
        // the half that is left over would print as a broken character, so it goes.
        if (char.IsHighSurrogate(head[^1]))
        {
            head = head[..^1];
        }

        if (char.IsLowSurrogate(tail[0]))
        {
            tail = tail[1..];
        }

        return string.Concat(head, "...", tail);
    }

    private static void AppendEscaped(StringBuilder text, Rune rune)
    {
        // Written by code point, never as a line-break literal: these are the characters being escaped.
        const int lineFeed = 0x0A;
        const int carriageReturn = 0x0D;
        const int tab = 0x09;

        switch (rune.Value)
        {
            case Backslash:
                text.Append(Backslash).Append(Backslash);
                break;
            case lineFeed:
                text.Append(Backslash).Append('n');
                break;
            case carriageReturn:
                text.Append(Backslash).Append('r');
                break;
            case tab:
                text.Append(Backslash).Append('t');
                break;
            default:
                UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                if (category is UnicodeCategory.Control or UnicodeCategory.Format)
                {
                    text.Append(Backslash).Append("u{")
                        .Append(rune.Value.ToString("X4", CultureInfo.InvariantCulture)).Append('}');
                }
                else
                {
                    text.Append(rune);
                }

                break;
        }
    }

    private static string QuoteForCommand(string path)
    {
        return path.AsSpan().ContainsAnyExcept(BareCharacters) ? $"\"{path}\"" : path;
    }
}

using System.Text;

using static GitAssertions.LineBreak;

namespace GitAssertions;

/// <summary>
/// Reads the variables out of the text of a git config file, for <see cref="NetworkPathGuard"/>: the syntax of
/// https://git-scm.com/docs/git-config#_syntax, and not a whole parser. It knows sections (<c>[core]</c>,
/// <c>[includeIf "gitdir:~/work/"]</c>, and the old <c>[section.subsection]</c> form), names that git compares ignoring
/// case, values with or without double quotes, the escapes <c>\\</c>, <c>\"</c>, <c>\n</c>, <c>\t</c>, and
/// <c>\b</c>, a backslash at the end of a line that continues the value, and comments after <c>#</c> or <c>;</c>
/// outside quotes. It ports the way git itself reads a value (<c>parse_value</c> in git's <c>config.c</c>), so a value
/// reads here as it reads in git.
/// <para>
/// The reader is lenient where git would stop with an error: a line it cannot make sense of is skipped, and an escape
/// git does not know stands for its own character. A file git rejects is one libgit2 rejects too, so what the reader
/// does with it can only change which error <see cref="GitAssert.Open"/> gives. It never throws.
/// </para>
/// </summary>
internal static class GitConfigReader
{
    private const char LineFeed = (char)0x0A;

    /// <summary>
    /// The variables of the file, in the order they are written; a variable written twice appears twice.
    /// </summary>
    public static IReadOnlyList<GitConfigEntry> Read(string text)
    {
        // A line break is CRLF or LF, as in git's reader, which turns CRLF into LF before it looks at anything else.
        Scanner scanner = new(text.Replace(CrLf, Lf, StringComparison.Ordinal));
        List<GitConfigEntry> entries = [];
        string? section = null;

        while (scanner.SkipWhitespace())
        {
            char first = scanner.Peek();
            if (first is '#' or ';')
            {
                scanner.SkipLine();
            }
            else if (first == '[')
            {
                scanner.Skip();
                section = scanner.ReadHeader();
                if (section is null)
                {
                    scanner.SkipLine();
                }
            }
            else if (char.IsAsciiLetter(first))
            {
                string name = scanner.ReadVariableName();
                (bool isVariable, string? value) = scanner.ReadAfterName();
                if (isVariable && section is not null)
                {
                    entries.Add(new GitConfigEntry($"{section}.{name}", value));
                }
            }
            else
            {
                scanner.SkipLine();
            }
        }

        return entries;
    }

    /// <summary>
    /// A cursor over the text with the reading rules, one per <see cref="Read"/> call. Every method that consumes
    /// text moves the cursor on, and none reads past the end.
    /// </summary>
    private sealed class Scanner(string text)
    {
        private int _position;

        /// <summary>
        /// Moves past white space, line breaks included, and says whether anything is left. Git skips them between
        /// lines, and between a header and what follows it on its line.
        /// </summary>
        public bool SkipWhitespace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }

            return _position < text.Length;
        }

        public char Peek()
        {
            return text[_position];
        }

        public void Skip()
        {
            _position++;
        }

        /// <summary>Moves past the rest of the line and its line break.</summary>
        public void SkipLine()
        {
            int lineEnd = text.IndexOf(LineFeed, _position);
            _position = lineEnd < 0 ? text.Length : lineEnd + 1;
        }

        /// <summary>
        /// Reads a section header after its <c>[</c>, and returns the prefix of its variables: <c>core</c> for
        /// <c>[core]</c>, <c>includeif.gitdir:x</c> for <c>[includeIf "gitdir:x"]</c>, and <c>remote.origin</c> for the
        /// old form <c>[Remote.Origin]</c>. Null when the header is malformed. The cursor stands after the <c>]</c>.
        /// </summary>
        public string? ReadHeader()
        {
            StringBuilder name = new();
            while (_position < text.Length)
            {
                char c = text[_position++];
                if (c == ']')
                {
                    return name.Length > 0 ? name.ToString() : null;
                }

                if (c is ' ' or '\t')
                {
                    return ReadSubsection(name);
                }

                if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.'))
                {
                    return null;
                }

                name.Append(char.ToLowerInvariant(c));
            }

            return null;
        }

        /// <summary>The name of a variable: letters, digits and dashes, in lower case.</summary>
        public string ReadVariableName()
        {
            StringBuilder name = new();
            while (_position < text.Length && IsNameCharacter(text[_position]))
            {
                name.Append(char.ToLowerInvariant(text[_position++]));
            }

            return name.ToString();
        }

        /// <summary>
        /// Reads what follows a variable's name: <c>= value</c>, or nothing before the end of the line or a comment,
        /// which is a variable with no value. Anything else is no variable, and the rest of the line is skipped.
        /// </summary>
        public (bool IsVariable, string? Value) ReadAfterName()
        {
            while (_position < text.Length && text[_position] != LineFeed && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }

            if (_position >= text.Length || text[_position] is LineFeed or '#' or ';')
            {
                SkipLine();
                return (true, null);
            }

            if (text[_position] != '=')
            {
                SkipLine();
                return (false, null);
            }

            _position++;
            return (true, ReadValue());
        }

        /// <summary>
        /// The subsection of a header like <c>[includeIf "gitdir:x"]</c>, after the space that follows the section's
        /// name: in double quotes, where a backslash stands for the character after it. Null when it is not closed
        /// on its own line.
        /// </summary>
        private string? ReadSubsection(StringBuilder section)
        {
            while (_position < text.Length && text[_position] is ' ' or '\t')
            {
                _position++;
            }

            if (_position >= text.Length || text[_position++] != '"')
            {
                return null;
            }

            section.Append('.');
            while (_position < text.Length)
            {
                char c = text[_position++];
                if (c == LineFeed)
                {
                    return null;
                }

                if (c == '"')
                {
                    return _position < text.Length && text[_position++] == ']' ? section.ToString() : null;
                }

                if (c == '\\')
                {
                    if (_position >= text.Length || text[_position] == LineFeed)
                    {
                        return null;
                    }

                    c = text[_position++];
                }

                section.Append(c);
            }

            return null;
        }

        /// <summary>
        /// A value, up to the end of its line, as git's <c>parse_value</c> reads it. White space around the value is
        /// dropped and white space inside it stays, one space for each, unless it is in quotes, where every character
        /// stays as written. A backslash and a line break continue the value on the next line. A <c>#</c> or <c>;</c>
        /// outside quotes starts a comment.
        /// </summary>
        private string ReadValue()
        {
            StringBuilder value = new();
            bool quoted = false;
            bool comment = false;
            int pendingSpaces = 0;

            while (_position < text.Length)
            {
                char c = text[_position++];
                if (c == LineFeed)
                {
                    break;
                }

                if (comment)
                {
                    continue;
                }

                if (char.IsWhiteSpace(c) && !quoted)
                {
                    // Leading space is dropped, and space inside is kept only when something follows it.
                    if (value.Length > 0)
                    {
                        pendingSpaces++;
                    }

                    continue;
                }

                if (!quoted && c is '#' or ';')
                {
                    comment = true;
                    continue;
                }

                value.Append(' ', pendingSpaces);
                pendingSpaces = 0;

                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c != '\\')
                {
                    value.Append(c);
                }
                else if (!AppendEscape(value))
                {
                    break;
                }
            }

            return value.ToString();
        }

        /// <summary>
        /// Reads the character after a backslash and appends what it stands for: a tab, a backspace, or a line feed for
        /// <c>t</c>, <c>b</c>, and <c>n</c>, and the character itself otherwise, which is right for <c>\\</c> and
        /// <c>\"</c>, and, here, also for an escape git rejects, which libgit2 rejects as well. A line break after the
        /// backslash continues the value and appends nothing. False when the text ends after the backslash.
        /// </summary>
        private bool AppendEscape(StringBuilder value)
        {
            if (_position >= text.Length)
            {
                return false;
            }

            char c = text[_position++];
            if (c != LineFeed)
            {
                value.Append(c switch
                {
                    't' => '\t',
                    'b' => '\b',
                    'n' => LineFeed,
                    _ => c,
                });
            }

            return true;
        }

        private static bool IsNameCharacter(char c)
        {
            return char.IsAsciiLetterOrDigit(c) || c == '-';
        }
    }
}

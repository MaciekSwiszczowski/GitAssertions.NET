namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Plays a script, a text file that builds a repository line by line with the real git CLI, so an end-to-end scenario
/// reads as the commands a person would type. Every instruction but <c>git</c> runs in this process, so a script needs
/// no shell and nothing installed on the build machine; real shell syntax (<c>&gt;</c>, <c>&gt;&gt;</c>, globbing) is
/// not parsed, and <c>bin/*</c> is just a word. A line is one of:
/// <list type="bullet">
/// <item><c>git &lt;arguments&gt;</c>: runs git in the folder (<see cref="GitCli.RunLineAsync"/>), with the arguments
/// exactly as written. A failing command does not stop the script; the snapshot of the result says what is
/// expected.</item>
/// <item><c>echo &lt;content&gt; &lt;path&gt;</c>: writes the content and an LF to the file, creating it and its
/// folders, or overwriting it.</item>
/// <item><c>append &lt;content&gt; &lt;path&gt;</c>: adds the content and an LF right after the last byte of the
/// file, creating it and its folders if needed. A file that does not end in a line break gets the content joined to its
/// last line.</item>
/// <item><c>unix2dos &lt;path&gt;</c> and <c>dos2unix &lt;path&gt;</c>: rewrite the file with every line break (LF or
/// CRLF) as CRLF, or as LF, and nothing else changed: the other bytes stay, so do a BOM, a lone CR and a missing final
/// line break. The conversion works on bytes, so it suits UTF-8 and ASCII, which is what <c>echo</c> and
/// <c>append</c> write; a UTF-16 file would be corrupted. The file is always rewritten, even when it already has those
/// line breaks, so it gets a new write time, as a file saved unchanged does. A missing file is an error.</item>
/// <item>An empty line, or a comment starting with <c>#</c>.</item>
/// </list>
/// The words of <c>echo</c>, <c>append</c>, <c>unix2dos</c> and <c>dos2unix</c> are separated by spaces. A word that
/// starts with a double quote runs to the next double quote and may hold spaces, such as
/// <c>echo "* text=auto" .gitattributes</c>; it cannot hold a double quote, and a double quote anywhere else in a word
/// is an error. Any word may be quoted, a path too.
/// Each git command and its output go to the test's output, so a typo such as <c>git comit</c> can be found when the
/// snapshot does not match.
/// </summary>
internal static class GitScript
{
    /// <summary>The ending of a script's file name.</summary>
    public const string Extension = ".script.txt";

    private const char Quote = '"';

    private static readonly byte Cr = (byte)LineBreak.Cr[0];

    private static readonly byte Lf = (byte)LineBreak.Lf[0];

    /// <summary>
    /// The names of the scripts in <paramref name="directory"/> whose file name starts with <paramref name="prefix"/>,
    /// such as <c>ScriptTests.</c>, with the prefix and <see cref="Extension"/> taken off, sorted.
    /// </summary>
    public static IEnumerable<string> NamesIn(string directory, string prefix)
    {
        return Directory.EnumerateFiles(directory, prefix + "*" + Extension)
            .Select(path => Path.GetFileName(path)[prefix.Length..^Extension.Length])
            .Order(StringComparer.Ordinal);
    }

    /// <summary>Plays <paramref name="scriptFile"/> line by line in <paramref name="workingDirectory"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// A line is not an instruction listed on <see cref="GitScript"/>, has the wrong number of words for its
    /// instruction, has a double quote that is never closed or sits inside a word, or asks to convert a file that does
    /// not exist. The message names the script and the line.
    /// </exception>
    public static async Task PlayAsync(string scriptFile, string workingDirectory)
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string[] lines = await File.ReadAllLinesAsync(scriptFile, cancellation);

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            string where = $"{Path.GetFileName(scriptFile)} line {index + 1}";

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int space = line.IndexOf(' ');
            string instruction = space < 0 ? line : line[..space];
            string arguments = space < 0 ? string.Empty : line[(space + 1)..];

            switch (instruction)
            {
                case "git":
                    await RunGitAsync(where, line, arguments, workingDirectory);
                    break;

                case "echo":
                {
                    string[] words = ReadWords(where, line, arguments, "echo <content> <path>", 2);
                    await WriteTextAsync(workingDirectory, words[1], words[0], append: false, cancellation);
                    break;
                }

                case "append":
                {
                    string[] words = ReadWords(where, line, arguments, "append <content> <path>", 2);
                    await WriteTextAsync(workingDirectory, words[1], words[0], append: true, cancellation);
                    break;
                }

                case "unix2dos":
                {
                    string[] words = ReadWords(where, line, arguments, "unix2dos <path>", 1);
                    await ConvertLineBreaksAsync(where, line, workingDirectory, words[0], toCrlf: true, cancellation);
                    break;
                }

                case "dos2unix":
                {
                    string[] words = ReadWords(where, line, arguments, "dos2unix <path>", 1);
                    await ConvertLineBreaksAsync(where, line, workingDirectory, words[0], toCrlf: false, cancellation);
                    break;
                }

                default:
                    throw new InvalidOperationException(
                        $"{where}: expected \"git <arguments>\", \"echo <content> <path>\", "
                        + "\"append <content> <path>\", \"unix2dos <path>\" or \"dos2unix <path>\", "
                        + $"got \"{line}\".");
            }
        }
    }

    private static async Task RunGitAsync(string where, string line, string arguments, string workingDirectory)
    {
        if (arguments.Length == 0)
        {
            throw ExpectedForm(where, "git <arguments>", line);
        }

        GitRun run = await GitCli.RunLineAsync(workingDirectory, arguments);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{where}: {line}{Environment.NewLine}exit code {run.ExitCode}{Environment.NewLine}"
            + $"{run.StandardOutput}{run.StandardError}");
    }

    /// <summary>
    /// Splits <paramref name="arguments"/> into words, and checks there are <paramref name="expectedCount"/> of them.
    /// </summary>
    private static string[] ReadWords(string where, string line, string arguments, string form, int expectedCount)
    {
        List<string> words = [];
        int position = 0;

        while (true)
        {
            while (position < arguments.Length && arguments[position] == ' ')
            {
                position++;
            }

            if (position == arguments.Length)
            {
                break;
            }

            if (arguments[position] == Quote)
            {
                int closing = arguments.IndexOf(Quote, position + 1);
                if (closing < 0)
                {
                    throw new InvalidOperationException(
                        $"{where}: a double quote is never closed in \"{line}\"; "
                        + "close it, and leave double quotes out of quoted content.");
                }

                if (closing + 1 < arguments.Length && arguments[closing + 1] != ' ')
                {
                    throw MisplacedQuote(where, line);
                }

                words.Add(arguments[(position + 1)..closing]);
                position = closing + 1;
            }
            else
            {
                int end = arguments.IndexOf(' ', position);
                if (end < 0)
                {
                    end = arguments.Length;
                }

                string word = arguments[position..end];
                if (word.Contains(Quote))
                {
                    throw MisplacedQuote(where, line);
                }

                words.Add(word);
                position = end;
            }
        }

        if (words.Count != expectedCount)
        {
            // Too many words is most often content or a path with spaces and no quotes.
            string hint = words.Count > expectedCount ? " A word with spaces goes in double quotes." : string.Empty;
            throw ExpectedForm(where, form, line, hint);
        }

        // The path is the last word of every form. Quotes make an empty one possible, and it would resolve to the
        // working directory and fail in the file API without naming the line.
        if (words[^1].Length == 0)
        {
            throw ExpectedForm(where, form, line, " The path is empty.");
        }

        return [.. words];
    }

    private static async Task WriteTextAsync(
        string workingDirectory, string relativePath, string content, bool append, CancellationToken cancellation)
    {
        string path = Path.Combine(workingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string text = content + LineBreak.Lf;

        if (append)
        {
            await File.AppendAllTextAsync(path, text, cancellation);
        }
        else
        {
            await File.WriteAllTextAsync(path, text, cancellation);
        }
    }

    /// <summary>
    /// Rewrites the file on the bytes, not as text, so a BOM and the encoding stay as they are. The write is
    /// unconditional: the file gets a new write time even when no byte changes.
    /// </summary>
    private static async Task ConvertLineBreaksAsync(
        string where,
        string line,
        string workingDirectory,
        string relativePath,
        bool toCrlf,
        CancellationToken cancellation)
    {
        string path = Path.Combine(workingDirectory, relativePath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"{where}: \"{line}\" cannot convert \"{relativePath}\", because the file does not exist.");
        }

        byte[] content = await File.ReadAllBytesAsync(path, cancellation);
        await File.WriteAllBytesAsync(path, ConvertLineBreaks(content, toCrlf), cancellation);
    }

    /// <summary>A line break is an LF or a CRLF; a CR on its own is not one, as it is not for git.</summary>
    private static byte[] ConvertLineBreaks(byte[] content, bool toCrlf)
    {
        List<byte> converted = new(content.Length);

        for (int index = 0; index < content.Length; index++)
        {
            byte current = content[index];
            bool isCrlf = current == Cr && index + 1 < content.Length && content[index + 1] == Lf;
            if (isCrlf)
            {
                index++;
            }

            if (isCrlf || current == Lf)
            {
                if (toCrlf)
                {
                    converted.Add(Cr);
                }

                converted.Add(Lf);
            }
            else
            {
                converted.Add(current);
            }
        }

        return [.. converted];
    }

    private static InvalidOperationException ExpectedForm(
        string where, string form, string line, string hint = "")
    {
        return new InvalidOperationException($"{where}: expected \"{form}\", got \"{line}\".{hint}");
    }

    private static InvalidOperationException MisplacedQuote(string where, string line)
    {
        return new InvalidOperationException(
            $"{where}: a double quote must open or close a whole word in \"{line}\"; "
            + "leave double quotes out of content.");
    }
}

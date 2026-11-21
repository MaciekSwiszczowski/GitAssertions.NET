namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// Plays a script, a text file that builds a repository line by line with the real git CLI, so an end-to-end scenario
/// reads as the commands a person would type. A line is one of:
/// <list type="bullet">
/// <item><c>git &lt;arguments&gt;</c>: runs git in the folder (<see cref="GitCli.RunLineAsync"/>), with the arguments
/// exactly as written. A failing command does not stop the script; the snapshot of the result says what is
/// expected.</item>
/// <item><c>echo &lt;content&gt; &lt;path&gt;</c>: writes the one-word content and a line break to the file,
/// creating it and its folders, or overwriting it.</item>
/// <item>An empty line, or a comment starting with <c>#</c>.</item>
/// </list>
/// Each git command and its output go to the test's output, so a typo such as <c>git comit</c> can be found when the
/// snapshot does not match.
/// </summary>
internal static class GitScript
{
    /// <summary>The ending of a script's file name.</summary>
    public const string Extension = ".script.txt";

    private const string GitPrefix = "git ";

    private const string EchoPrefix = "echo ";

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
    /// A line is neither a git command, an echo with content and a path, a comment, nor empty. The message names the
    /// script and the line.
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

            if (line.StartsWith(GitPrefix, StringComparison.Ordinal))
            {
                GitRun run = await GitCli.RunLineAsync(workingDirectory, line[GitPrefix.Length..]);
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"{where}: {line}\nexit code {run.ExitCode}\n{run.StandardOutput}{run.StandardError}");
            }
            else if (line.StartsWith(EchoPrefix, StringComparison.Ordinal))
            {
                string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 3)
                {
                    throw new InvalidOperationException(
                        $"{where}: expected \"echo <content> <path>\", got \"{line}\".");
                }

                string path = Path.Combine(workingDirectory, tokens[2]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, tokens[1] + LineBreak.Lf, cancellation);
            }
            else
            {
                throw new InvalidOperationException(
                    $"{where}: expected \"git <arguments>\" or \"echo <content> <path>\", got \"{line}\".");
            }
        }
    }
}

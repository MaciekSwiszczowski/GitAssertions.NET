namespace GitAssertions;

/// <summary>
/// What one check found: whether the repository keeps the check's rule, a message for a person, and the offending
/// files. In a test, assert on <see cref="IsHealthy"/> and pass <see cref="Message"/> as the failure text, for
/// example <c>Assert.True(result.IsHealthy, result.Message)</c>.
/// </summary>
public sealed class CheckResult
{
    private CheckResult(string rule, bool isHealthy, string message, IReadOnlyList<string> files)
    {
        Rule = rule;
        IsHealthy = isHealthy;
        Message = message;
        Files = files;
    }

    /// <summary>The check's name, such as <c>NoUntrackedFiles</c>.</summary>
    public string Rule { get; }

    /// <summary>True when the repository keeps the check's rule.</summary>
    public bool IsHealthy { get; }

    /// <summary>
    /// <c>&lt;Rule&gt;: OK.</c> when healthy. Otherwise the rule line, the offending entries (at most 20), and a
    /// <c>Fix:</c> command to run from the repository root. Fix commands are suggestions: read one before running it.
    /// A path is printed escaped and, when long, shortened, and goes into a command only when it is shell-safe; a note
    /// names the others. The message is at most 16,000 characters.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Every offending path, relative to the repository root and sorted ordinally. It is never shortened: it holds
    /// every path even when <see cref="Message"/> lists only the first 20. Empty when healthy, and for a failure that
    /// is about no particular file, such as a merge in progress.
    /// </summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>The result of a check that found nothing wrong: <c>&lt;Rule&gt;: OK.</c> and no files.</summary>
    internal static CheckResult Healthy(string rule)
    {
        return new CheckResult(rule, isHealthy: true, $"{rule}: OK.", []);
    }

    /// <summary>
    /// The result of a check that found something wrong. <paramref name="summary"/> is the rest of the first line,
    /// with the count, such as <c>2 untracked files found.</c>. <see cref="FailureMessage"/> prints the entries and
    /// the fix blocks; <paramref name="files"/> becomes <see cref="Files"/>.
    /// </summary>
    internal static CheckResult Failing(
        string rule,
        string summary,
        IReadOnlyList<FailureEntry> entries,
        IReadOnlyList<FixStep> fixes,
        IReadOnlyList<string> files)
    {
        string message = FailureMessage.Format(rule, summary, entries, fixes);

        // A sorted copy: the caller's list may change later, and a read-only list cannot be cast back and edited.
        return new CheckResult(rule, isHealthy: false, message, [.. files.Order(StringComparer.Ordinal)]);
    }
}

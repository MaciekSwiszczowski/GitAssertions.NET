using System.Globalization;

namespace GitAssertions;

/// <summary>
/// What <see cref="GitAssert.RunAll"/> found: the result of every check, the ones that failed, and one message for a
/// person. In a test, assert on <see cref="IsHealthy"/> and pass <see cref="Message"/> as the failure text, for example
/// <c>Assert.True(result.IsHealthy, result.Message)</c>.
/// </summary>
public sealed class RunAllResult
{
    private const string HealthyMessage = "All Git checks passed.";

    private RunAllResult(IReadOnlyList<CheckResult> results, IReadOnlyList<CheckResult> failures, string message)
    {
        Results = results;
        Failures = failures;
        Message = message;
    }

    /// <summary>
    /// One result per check, healthy ones included, in a fixed order: NoUntrackedFiles, NoUnstagedChanges,
    /// NoConflicts, NoOperationInProgress, NoTrackedIgnoredFiles.
    /// </summary>
    public IReadOnlyList<CheckResult> Results { get; }

    /// <summary>
    /// The entries of <see cref="Results"/> that failed, in the same order. Empty when everything passed.
    /// </summary>
    public IReadOnlyList<CheckResult> Failures { get; }

    /// <summary>True only when every check is healthy.</summary>
    public bool IsHealthy => Failures.Count == 0;

    /// <summary>
    /// <c>All Git checks passed.</c> when healthy. Otherwise a header, <c>&lt;f&gt; of &lt;n&gt; Git checks failed.</c>,
    /// then the <see cref="CheckResult.Message"/> of each failing check, unchanged and in the order of
    /// <see cref="Results"/>, with a blank line before each. Fix commands are suggestions: read one before running it.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Builds the result from every check's result, in the order they ran. Both lists are copied, so the caller's
    /// list may change afterwards and a read-only list cannot be cast back and edited.
    /// </summary>
    internal static RunAllResult Create(IReadOnlyList<CheckResult> results)
    {
        // Typed as the read-only interface, so the collection expression builds a list that cannot be cast back to an
        // array or a List<T> and edited, as an array assigned here would be.
        IReadOnlyList<CheckResult> failures = [.. results.Where(static result => !result.IsHealthy)];

        string message = failures.Count == 0
            ? HealthyMessage
            : string.Create(CultureInfo.InvariantCulture, $"{failures.Count} of {results.Count} Git checks failed.")
              + string.Concat(failures.Select(static failure => LineBreak.Lf + LineBreak.Lf + failure.Message));

        return new RunAllResult([.. results], failures, message);
    }
}

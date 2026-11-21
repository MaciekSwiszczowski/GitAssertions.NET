using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using Xunit.v3;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// The snapshot every check test ends in: a <see cref="CheckResult"/>, or the <see cref="RunAllResult"/> of RunAll,
/// rendered as text and compared with a reviewed <c>.verified.txt</c> file beside the test class. One file pins the
/// whole result, so a change to a message is one reviewed diff, not a hunt through string literals. Read the
/// snapshot-tests-csharp skill before adding a snapshot test.
/// </summary>
internal static class CheckResultSnapshot
{
    private static readonly char[] NotInFileNames = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Verifies <paramref name="result"/> against the snapshot of the calling test,
    /// <c>&lt;TestClass&gt;.&lt;TestMethod&gt;.verified.txt</c>. <paramref name="sourceFile"/> is the caller's file,
    /// which tells Verify where the test class lives; leave it to the compiler.
    /// </summary>
    public static Task Verify(CheckResult result, [CallerFilePath] string sourceFile = "")
    {
        return Compare(Render(result), settings: null, sourceFile);
    }

    /// <summary>
    /// Verifies <paramref name="result"/>, what <see cref="GitAssert.RunAll"/> returned, against the snapshot of the
    /// calling test, <c>&lt;TestClass&gt;.&lt;TestMethod&gt;.verified.txt</c>.
    /// </summary>
    public static Task Verify(RunAllResult result, [CallerFilePath] string sourceFile = "")
    {
        return Compare(Render(result), settings: null, sourceFile);
    }

    /// <summary>
    /// Verifies <paramref name="result"/> against the one snapshot that every healthy scenario of
    /// <paramref name="rule"/> shares, <c>&lt;rule&gt;.IsHealthy.verified.txt</c>, because a healthy result is the
    /// same whatever the scenario. The test names the rule it expects, so a result from another check, or a failing
    /// result, can never match some other file.
    /// </summary>
    public static Task VerifyHealthy(string rule, CheckResult result, [CallerFilePath] string sourceFile = "")
    {
        return Compare(Render(result), SharedHealthySettings(rule), sourceFile);
    }

    /// <summary>
    /// Verifies <paramref name="result"/>, what <see cref="GitAssert.RunAll"/> returned, against the one snapshot every
    /// healthy RunAll shares, <c>&lt;rule&gt;.IsHealthy.verified.txt</c>, with <paramref name="rule"/> the name of
    /// <see cref="GitAssert.RunAll"/>. As for a single check, the test names the file it expects, never the result.
    /// </summary>
    public static Task VerifyHealthy(string rule, RunAllResult result, [CallerFilePath] string sourceFile = "")
    {
        return Compare(Render(result), SharedHealthySettings(rule), sourceFile);
    }

    /// <summary>
    /// Verifies <paramref name="result"/>, what <see cref="GitAssert.RunAll"/> returned after a script was played,
    /// against <c>&lt;fileName&gt;.verified.txt</c>, with <paramref name="fileName"/> the script's file name without
    /// its <c>.script.txt</c>, so the script and its snapshot sit side by side under the test class. A healthy result
    /// is pinned per script too, never chosen from the result.
    /// </summary>
    public static Task VerifyScript(string fileName, RunAllResult result, [CallerFilePath] string sourceFile = "")
    {
        VerifySettings settings = new();
        settings.UseFileName(fileName);
        return Compare(Render(result), settings, sourceFile);
    }

    private static VerifySettings SharedHealthySettings(string rule)
    {
        VerifySettings settings = new();
        settings.UseTypeName(rule);
        settings.UseMethodName("IsHealthy");

        // The verified file is shared, but each test still writes its own received file, named after the test. With
        // one shared name, tests in classes that run in parallel fail with an IOException writing the same file, and
        // a passing test deletes the received file a failing one has just written.
        settings.UseTextForParameters(ReceivedFileName());
        settings.IgnoreParametersForVerified();
        return settings;
    }

    private static Task Compare(string text, VerifySettings? settings, string sourceFile)
    {
        // Verify takes no cancellation token, so the test's deadline bounds the wait here: when the Timeout fires,
        // the await ends with the run instead of leaving the comparison running on a background thread.
        // ReSharper disable once ExplicitCallerInfoArgument - this is a needed workaround
        return Verifier.Verify(text, settings, sourceFile).ToTask()
            .WaitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The running test as <c>Class.Method</c>, then each theory argument after an underscore, with every character a
    /// file name cannot hold replaced by <c>-</c>, as Verify does for its own parameter names.
    /// </summary>
    private static string ReceivedFileName()
    {
        IXunitTest test = (IXunitTest)TestContext.Current.Test!;
        string name = $"{test.TestCase.TestClassSimpleName}.{test.TestCase.TestMethodName}{string.Concat(
            test.TestMethodArguments.Select(static argument =>
                $"_{Convert.ToString(argument, CultureInfo.InvariantCulture)}"))}";
        return string.Concat(name.Select(static character => NotInFileNames.Contains(character) ? '-' : character));
    }

    /// <summary>
    /// Every public property of the result, with the message last and exactly as it prints. The file count is part
    /// of the heading, so an empty list reads "Files (0):" and cannot be mistaken for a missing one.
    /// </summary>
    private static string Render(CheckResult result)
    {
        StringBuilder text = new();
        text.Append(CultureInfo.InvariantCulture, $"Rule: {result.Rule}\n");
        text.Append(CultureInfo.InvariantCulture, $"IsHealthy: {result.IsHealthy}\n");
        text.Append(CultureInfo.InvariantCulture, $"Files ({result.Files.Count}):\n");
        foreach (string file in result.Files)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {file}\n");
        }

        text.Append("Message:\n").Append(result.Message);
        return text.ToString();
    }

    /// <summary>
    /// Every public property of the result. Each check result is one line, its files below it; its own message is left
    /// out, because the check's own tests pin it and the RunAll message repeats every failing one. The failures list
    /// only the rules, because each is one of the results above. The message comes last, exactly as it prints. Every
    /// count is in its heading, so an empty list reads "Failures (0):" and cannot be mistaken for a missing one.
    /// </summary>
    private static string Render(RunAllResult result)
    {
        StringBuilder text = new();
        text.Append(CultureInfo.InvariantCulture, $"IsHealthy: {result.IsHealthy}\n");
        text.Append(CultureInfo.InvariantCulture, $"Results ({result.Results.Count}):\n");
        foreach (CheckResult check in result.Results)
        {
            text.Append(
                CultureInfo.InvariantCulture,
                $"  {check.Rule}: IsHealthy {check.IsHealthy}, Files ({check.Files.Count}):\n");
            foreach (string file in check.Files)
            {
                text.Append(CultureInfo.InvariantCulture, $"    {file}\n");
            }
        }

        text.Append(CultureInfo.InvariantCulture, $"Failures ({result.Failures.Count}):\n");
        foreach (CheckResult failure in result.Failures)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {failure.Rule}\n");
        }

        text.Append("Message:\n").Append(result.Message);
        return text.ToString();
    }
}

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using Xunit.v3;

namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// The snapshot every check test ends in: a <see cref="CheckResult"/> rendered as text and compared with a reviewed
/// <c>.verified.txt</c> file beside the test class. One file pins the whole result, so a change to a message is one
/// reviewed diff, not a hunt through string literals. Read the snapshot-tests-csharp skill before adding a snapshot
/// test.
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
        return Compare(result, settings: null, sourceFile);
    }

    /// <summary>
    /// Verifies <paramref name="result"/> against the one snapshot that every healthy scenario of
    /// <paramref name="rule"/> shares, <c>&lt;rule&gt;.IsHealthy.verified.txt</c>, because a healthy result is the
    /// same whatever the scenario. The test names the rule it expects, so a result from another check, or a failing
    /// result, can never match some other file.
    /// </summary>
    public static Task VerifyHealthy(string rule, CheckResult result, [CallerFilePath] string sourceFile = "")
    {
        VerifySettings settings = new();
        settings.UseTypeName(rule);
        settings.UseMethodName("IsHealthy");

        // The verified file is shared, but each test still writes its own received file, named after the test. With
        // one shared name, tests in classes that run in parallel fail with an IOException writing the same file, and
        // a passing test deletes the received file a failing one has just written.
        settings.UseTextForParameters(ReceivedFileName());
        settings.IgnoreParametersForVerified();
        return Compare(result, settings, sourceFile);
    }

    private static Task Compare(CheckResult result, VerifySettings? settings, string sourceFile)
    {
        // Verify takes no cancellation token, so the test's deadline bounds the wait here: when the Timeout fires,
        // the await ends with the run instead of leaving the comparison running on a background thread.
        // ReSharper disable once ExplicitCallerInfoArgument - this is a needed workaround
        return Verifier.Verify(Render(result), settings, sourceFile).ToTask()
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
}

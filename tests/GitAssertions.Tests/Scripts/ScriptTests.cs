using System.Runtime.CompilerServices;

using GitAssertions.Tests.Infrastructure;

using Meziantou.Framework;

namespace GitAssertions.Tests.Scripts;

/// <summary>
/// End-to-end scenarios against real Git. Each <c>ScriptTests.&lt;Name&gt;.script.txt</c> in this folder builds a
/// repository with the git CLI (<see cref="GitScript"/>), and <c>ScriptTests.&lt;Name&gt;.verified.txt</c> beside it
/// holds what RunAll must return for it. Both are named after this class, so the IDE nests them under it, as it nests
/// the verified files of any snapshot test. A new scenario is those two files and no code: the theory finds every
/// script in the folder.
/// </summary>
public sealed class ScriptTests
{
    private const string FilePrefix = nameof(ScriptTests) + ".";

    public static TheoryData<string> Scripts => new(GitScript.NamesIn(ScriptDirectory(), FilePrefix));

    // Trigger: A person, or an agent, builds a repository with real git commands, as the script spells them out.
    // Behavior: RunAll reads the repository git left behind as git itself sees it, and reports what the scenario
    //           expects: healthy, or each broken rule with its message.
    // Assertion: The whole RunAll result matches the script's verified file.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [MemberData(nameof(Scripts))]
    [Trait("Category", TestCategories.Integration)]
    public async Task RunAll_AfterScript_MatchesItsVerifiedFile(string script)
    {
        // Arrange
        string fileName = FilePrefix + script;
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        await GitScript.PlayAsync(Path.Combine(ScriptDirectory(), fileName + GitScript.Extension), directory.FullPath);

        // Act
        RunAllResult result = GitAssert.Open(directory.FullPath).RunAll();

        // Assert
        await CheckResultSnapshot.VerifyScript(fileName, result);
    }

    /// <summary>
    /// This folder in the source tree, where the scripts and their verified files sit beside this class.
    /// </summary>
    private static string ScriptDirectory([CallerFilePath] string sourceFile = "")
    {
        return Path.GetDirectoryName(sourceFile)!;
    }
}

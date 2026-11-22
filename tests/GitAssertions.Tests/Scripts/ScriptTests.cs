using System.Runtime.CompilerServices;

using GitAssertions.Tests.Infrastructure;

using Meziantou.Framework;

namespace GitAssertions.Tests.Scripts;

/// <summary>
/// End-to-end scenarios against real Git. Each <c>ScriptTests.&lt;Name&gt;.script.txt</c> in this folder builds a
/// repository with the git CLI (<see cref="GitScript"/>), and <c>ScriptTests.&lt;Name&gt;.verified.txt</c> beside it
/// holds what real Git answers about the finished repository, <see cref="GitQuestions"/>, then a <c>---</c> line, then
/// what RunAll must return for it, so the library is checked against git in one file. Both are named after this class, so the IDE nests
/// them under it, as it nests the verified files of any snapshot test. A new scenario is those two files and no code:
/// the theory finds every script in the folder.
/// </summary>
public sealed class ScriptTests
{
    private const string FilePrefix = nameof(ScriptTests) + ".";

    /// <summary>
    /// What the tests ask git after RunAll, about the same folder. <c>git status --porcelain --untracked-files=all</c>
    /// shows untracked files (<c>??</c>, one by one, as NoUntrackedFiles lists them), unstaged changes (<c>M</c> or
    /// <c>D</c> in the second column) and conflicts (<c>UU</c>, <c>AA</c>, <c>DU</c>, <c>UD</c>). Porcelain v1, because
    /// git guarantees its format does not change between versions or with user config, and never translates it:
    /// https://git-scm.com/docs/git-status#_porcelain_format_version_1
    /// <c>git ls-files -i -c --exclude-standard</c> shows tracked files that match an ignore rule, which
    /// <c>git status</c> does not show (NoTrackedIgnoredFiles):
    /// https://git-scm.com/docs/git-ls-files
    /// NoOperationInProgress has no machine-readable answer from git, so it is not asked. Asking changes nothing
    /// RunAll read, because <see cref="GitCli"/> sets <c>GIT_OPTIONAL_LOCKS=0</c> and <c>git status</c> does not
    /// rewrite the index.
    /// </summary>
    private static readonly string[][] GitQuestions =
    [
        ["status", "--porcelain", "--untracked-files=all"],
        ["ls-files", "-i", "-c", "--exclude-standard"],
    ];

    public static TheoryData<string> Scripts => new(GitScript.NamesIn(ScriptDirectory(), FilePrefix));

    // Trigger: A person, or an agent, builds a repository with real git commands, as the script spells them out.
    // Behavior: RunAll reads the repository git left behind as git itself sees it, and reports what the scenario
    //           expects: healthy, or each broken rule with its message. Git's own answers about the same folder
    //           tell the same story.
    // Assertion: Git's answers and the whole RunAll result match the script's verified file.
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
        List<GitAnswer> gitAnswers = [];
        foreach (string[] question in GitQuestions)
        {
            gitAnswers.Add(new GitAnswer(
                $"git {string.Join(' ', question)}",
                await GitCli.RunAsync(directory.FullPath, question)));
        }

        // Assert
        await CheckResultSnapshot.VerifyScript(fileName, gitAnswers, result);
    }

    /// <summary>
    /// This folder in the source tree, where the scripts and their verified files sit beside this class.
    /// </summary>
    private static string ScriptDirectory([CallerFilePath] string sourceFile = "")
    {
        return Path.GetDirectoryName(sourceFile)!;
    }
}

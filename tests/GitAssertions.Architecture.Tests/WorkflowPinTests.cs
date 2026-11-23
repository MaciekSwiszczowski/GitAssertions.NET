using System.Reflection;
using System.Text.RegularExpressions;

using GitAssertions.Tests;

using YamlDotNet.RepresentationModel;

namespace GitAssertions.Architecture.Tests;

/// <summary>
/// Fails when a workflow or an action under <c>.github</c> runs another action by a name its owner can move, such as
/// <c>uses: actions/upload-artifact@v4</c>, so the mistake is found before it is committed (docs/specification.md,
/// Security, Supply chain). Dependabot moves a pinned SHA to the next release, but leaves a tag a tag.
/// <para>
/// Written to be copied into another repository. Besides YamlDotNet and the global usings of Xunit and Shouldly, it
/// needs the repository root as <c>RepositoryRoot</c> assembly metadata, written by the project file
/// (<c>&lt;AssemblyMetadata Include="RepositoryRoot" Value="..." /&gt;</c>), a <c>TestTimeouts.DefaultMilliseconds</c>
/// constant, and <c>Lf</c>, the string of one line feed.
/// </para>
/// </summary>
public sealed partial class WorkflowPinTests
{
    private const string Sha = "3d3c42e5aac5ba805825da76410c181273ba90b1";

    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const int RegexTimeoutMilliseconds = 1_000;

    private static readonly string RepositoryRoot = typeof(WorkflowPinTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(static attribute => string.Equals(attribute.Key, "RepositoryRoot", StringComparison.Ordinal))
        .Value!;

    // The three pinned forms: a local action or workflow, which a commit of this repository already pins; an action
    // or reusable workflow at a full commit SHA; and a Docker image at its digest.
    [GeneratedRegex(
        @"^(\./\S*|[\w.-]+/[^@\s]+@[0-9a-fA-F]{40}|docker://[^@\s]+@sha256:[0-9a-fA-F]{64})$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        RegexTimeoutMilliseconds)]
    private static partial Regex PinnedForm();

    // Trigger: Someone writes a pinned `uses:`: an action at a commit SHA with its release in a comment, quoted or
    //          not, a reusable workflow the same way, a local action, or a Docker image at its digest.
    // Behavior: The rule reads the one `uses:` of the snippet as pinned.
    // Assertion: The snippet holds one `uses:`, and it is pinned, in every row.
    [Theory]
    [InlineData($"- uses: actions/checkout@{Sha} # v7.0.1")]
    [InlineData($"- uses: \"actions/checkout@{Sha}\" # v7.0.1")]
    [InlineData($"- uses: github/codeql-action/analyze@{Sha} # v3.28.0")]
    [InlineData($"uses: octo-org/example-repo/.github/workflows/build.yml@{Sha} # v1.2.0")]
    [InlineData("- uses: ./.github/actions/setup")]
    [InlineData($"- uses: docker://alpine@sha256:{Digest}")]
    public void Uses_PinnedForm_IsPinned(string yaml)
    {
        // Assert
        Uses(yaml).ShouldHaveSingleItem().Pinned.ShouldBeTrue();
    }

    // Trigger: Someone, or an agent copying an example, writes a `uses:` by a tag, a branch, or a short SHA, without a
    //          ref, at a commit SHA without its release comment, or a Docker image by its tag.
    // Behavior: The rule reads the one `uses:` of the snippet as unpinned, written in a flow mapping too.
    // Assertion: The snippet holds one `uses:`, and it is not pinned, in every row.
    [Theory]
    [InlineData("- uses: actions/upload-artifact@v4")]
    [InlineData("- uses: actions/checkout@main")]
    [InlineData("- uses: actions/checkout@3d3c42e # v7.0.1")]
    [InlineData($"- uses: actions/checkout@{Sha}")]
    [InlineData("- uses: actions/checkout")]
    [InlineData("- uses:")]
    [InlineData("- { uses: actions/checkout@v4 }")]
    [InlineData("uses: octo-org/example-repo/.github/workflows/build.yml@v1")]
    [InlineData("- uses: docker://alpine:3.20")]
    public void Uses_MovableName_IsNotPinned(string yaml)
    {
        // Assert
        Uses(yaml).ShouldHaveSingleItem().Pinned.ShouldBeFalse();
    }

    // Trigger: A workflow mentions `uses:` where it is not a key: in a comment, or in the text of a `run:` script.
    // Behavior: The rule reads no `uses:` there, so neither raises a false alarm.
    // Assertion: The snippet holds no `uses:`, in every row.
    [Theory]
    [InlineData("# uses: actions/checkout@v4")]
    [InlineData($"- run: |{Lf}    echo uses: actions/checkout@v4")]
    public void Uses_NotAKey_IsNotRead(string yaml)
    {
        // Assert
        Uses(yaml).ShouldBeEmpty();
    }

    // Trigger: Someone adds `uses: actions/upload-artifact@v4`, or any action by a tag, to a workflow or an action
    //          under .github in this repository.
    // Behavior: The suite fails and lists each such `uses:` as file:line, with how to pin it. It also fails when it
    //           reads no `uses:` at all, because a rule that reads nothing passes for ever.
    // Assertion: At least one `uses:` was read, and none of them is unpinned.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Workflows_ThisRepository_RunOnlyPinnedActions()
    {
        // Arrange
        string github = Path.Combine(RepositoryRoot, ".github");

        // Act
        List<(string Place, string Value, bool Pinned)> uses =
        [
            .. Directory.EnumerateFiles(github, "*", SearchOption.AllDirectories)
                .Where(static file => Path.GetExtension(file) is ".yml" or ".yaml")
                .SelectMany(static file => Uses(File.ReadAllText(file)).Select(use =>
                    ($"{Path.GetRelativePath(RepositoryRoot, file)}:{use.Line}", use.Value, use.Pinned))),
        ];

        // Assert
        uses.ShouldNotBeEmpty(
            $"No .yml or .yaml file under {github} holds a `uses:`, so the rule would pass whatever they hold.");
        uses.Where(static use => !use.Pinned).Select(static use => $"{use.Place}: uses: {use.Value}").ShouldBeEmpty(
            "These run an action by a name its owner can move. Pin each to the full commit SHA of a release, with " +
            "the release in a comment (`uses: <owner>/<repo>@<40 hex digits> # v1.2.3`), taken from the action's " +
            "own repository, never from a fork: `gh api repos/<owner>/<repo>/commits/<release tag> --jq .sha`.");
    }

    /// <summary>
    /// Every <c>uses:</c> key in the YAML text, with its line, its value, and whether the value is pinned. The parser
    /// reads only real keys, so quotes, comments, and a <c>run:</c> script that mentions one do not matter. It drops
    /// comments, so the release comment that a commit SHA needs, which Dependabot reads to move the SHA, is looked for
    /// on the line itself.
    /// </summary>
    private static IEnumerable<(long Line, string Value, bool Pinned)> Uses(string yaml)
    {
        YamlStream stream = [];
        stream.Load(new StringReader(yaml));
        string[] lines = yaml.Split(Lf);
        return stream.Documents
            .SelectMany(static document => document.AllNodes.OfType<YamlMappingNode>())
            .SelectMany(static mapping => mapping.Children)
            .Where(static entry => entry.Key is YamlScalarNode { Value: "uses" })
            .Select(entry =>
            {
                long line = entry.Key.Start.Line;
                string value = (entry.Value as YamlScalarNode)?.Value ?? string.Empty;
                bool atCommit = !value.StartsWith("./", StringComparison.Ordinal) &&
                    !value.StartsWith("docker://", StringComparison.Ordinal);
                bool commented = lines[line - 1].Contains(" #", StringComparison.Ordinal);
                return (line, value, PinnedForm().IsMatch(value) && (!atCommit || commented));
            });
    }
}

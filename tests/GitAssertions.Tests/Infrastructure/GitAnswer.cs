namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// What git answered to a question about a repository: the command as a person would type it, such as
/// <c>git status --porcelain --untracked-files=all</c>, and what it printed. A script scenario pins it in its verified
/// file above the RunAll result, with a <c>---</c> line between them (<see cref="CheckResultSnapshot.VerifyScript"/>),
/// so a disagreement between the library and real Git shows in one file.
/// </summary>
internal sealed record GitAnswer(string Command, string Output);

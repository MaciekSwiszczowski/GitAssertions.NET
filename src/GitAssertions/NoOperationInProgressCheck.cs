using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// The NoOperationInProgress check (docs/specification.md, Checks): fails while a merge, rebase, cherry-pick, or revert
/// is stopped halfway, which <c>git status</c> reports as "You have unmerged paths" or "rebase in progress". It reads
/// what libgit2 reports as the current operation, which Git keeps in state files such as <c>MERGE_HEAD</c> and
/// <c>rebase-merge/</c> until the operation is finished or aborted. The failure lists no file: the operation belongs to
/// the repository, not to a path. It reads an open repository, so <see cref="GitAssert"/> can run it on a repository it
/// opens for this check alone, and a later RunAll on one it opens once for every check.
/// </summary>
internal static class NoOperationInProgressCheck
{
    private const string Rule = "NoOperationInProgress";

    public static CheckResult Run(Repository repository)
    {
        CurrentOperation operation = repository.Info.CurrentOperation;

        if (operation == CurrentOperation.None)
        {
            return CheckResult.Healthy(Rule);
        }

        string? command = CommandOf(operation);
        if (command is null)
        {
            // An operation with no command of its own yet (a bisect, a sequence of several commits, git am): libgit2's
            // name for it, and a pointer to git status, which says how to finish or abort that one.
            return CheckResult.Failing(
                Rule,
                $"a Git operation ({operation}) is in progress.",
                entries: [],
                [FixStep.TextOnly("Fix: run \"git status\" to see how to finish or abort it.")],
                files: []);
        }

        return CheckResult.Failing(
            Rule,
            $"a {command} is in progress.",
            entries: [],
            [
                FixStep.WithCommand(
                    "Fix: once all conflicts are resolved and staged, run:",
                    $"git {command} --continue"),
                FixStep.WithCommand("Or abort it:", $"git {command} --abort"),
            ],
            files: []);
    }

    /// <summary>
    /// The name of the Git command that started the operation, which is also the command that finishes it with
    /// <c>--continue</c> or gives it up with <c>--abort</c>; null for an operation with no mapping yet. libgit2 tells a
    /// rebase apart by how it was started, which makes no difference to the person who has to finish it.
    /// </summary>
    private static string? CommandOf(CurrentOperation operation)
    {
        return operation switch
        {
            CurrentOperation.Merge => "merge",
            CurrentOperation.Rebase or CurrentOperation.RebaseInteractive or CurrentOperation.RebaseMerge => "rebase",
            CurrentOperation.CherryPick => "cherry-pick",
            CurrentOperation.Revert => "revert",
            _ => null,
        };
    }
}

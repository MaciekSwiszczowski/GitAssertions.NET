using LibGit2Sharp;

namespace GitAssertions;

/// <summary>
/// The NoOperationInProgress check (docs/specification.md, Checks): fails while a merge, rebase, cherry-pick, or revert
/// is stopped halfway, which <c>git status</c> reports as "You have unmerged paths" or "rebase in progress", or while a
/// bisect is left open ("You are currently bisecting"). It reads what libgit2 reports as the current operation, which
/// Git keeps in state files such as <c>MERGE_HEAD</c> and <c>rebase-merge/</c> until the operation is finished or
/// aborted, and a bisect's until <c>git bisect reset</c>, even after it has found the first bad commit. The failure
/// lists no file: the operation belongs to the repository, not to a path. It reads an open repository:
/// <see cref="GitAssert"/> opens one for this check alone, also when <see cref="GitAssert.RunAll"/> calls it, and
/// closes it when the check returns.
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

        if (operation == CurrentOperation.Bisect)
        {
            // A bisect has no --continue and no --abort, and it is not stopped on a conflict: git bisect reset ends it.
            return CheckResult.Failing(
                Rule,
                "a bisect is in progress.",
                entries: [],
                [FixStep.WithCommand("Fix: to end it and return to where you started, run:", "git bisect reset")],
                files: []);
        }

        string? command = CommandOf(operation);
        if (command is null)
        {
            // An operation with no command of its own yet (git am): libgit2's name for it, and a pointer to git
            // status, which says how to finish or abort that one.
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
    /// rebase apart by how it was started, and a cherry-pick or revert of several commits (it finds Git's
    /// <c>sequencer/</c> folder beside <c>CHERRY_PICK_HEAD</c> or <c>REVERT_HEAD</c>) from one of a single commit, which
    /// makes no difference to the person who has to finish it: the same <c>--continue</c> and <c>--abort</c> serve both.
    /// </summary>
    private static string? CommandOf(CurrentOperation operation)
    {
        return operation switch
        {
            CurrentOperation.Merge => "merge",
            CurrentOperation.Rebase or CurrentOperation.RebaseInteractive or CurrentOperation.RebaseMerge => "rebase",
            CurrentOperation.CherryPick or CurrentOperation.CherryPickSequence => "cherry-pick",
            CurrentOperation.Revert or CurrentOperation.RevertSequence => "revert",
            _ => null,
        };
    }
}

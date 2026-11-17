# GitAssertions.NET — Library Spec

## Purpose

GitAssertions.NET (namespace GitAssertions) is a small .NET library for writing Git state tests (architecture-style unit tests) that fail when an AI agent finishes a task and leaves the repository in a broken or messy state. A failing test tells the agent exactly what's wrong, so it can clean up the Git state itself before it finishes.

The library is **read-only**. It inspects the repository and reports what it finds; it never stages, commits, deletes, resets, or changes any file, ref, or setting. Each failure message ends with a ready-to-run `Fix:` command built from the actual offending paths, so a person can paste it into a terminal and an agent can run it. Running that command is always the caller's decision.

## Scope

Version 1 is a two-day build done with agents. Everything else waits for phase two and gets added when it's needed.

- **Version 1:** NoUntrackedFiles, NoUnstagedChanges, NoStagedUncommitted, NoConflicts, NoOperationInProgress, NoTrackedIgnoredFiles, and a plain RunAll with no options (every v1 check always runs). Unit tests use LibGit2Sharp only, so every v1 scenario is set up in-process.
- **Phase two:** NoMixedStaging (already covered by the staged and unstaged checks), NotDetachedHead, OnExpectedBranch, NoStaleIndexLock, configuration (enabling and disabling checks, allow-lists, expected branch), the CliWrap real-Git integration tests, and NuGet packaging. Use a project reference until then.

Test scenarios that depend on a phase-two feature (allow-listed paths, disabled checks, verification against the real git CLI) are marked *(phase two)* below and are not part of v1.

## Goals

- Readable, one-line assertions per rule.
- No external git.exe processes in the library at runtime. Everything the library does runs in-process through LibGit2Sharp (libgit2). This rule applies to the shipped library only; the phase-two integration tests deliberately call the git CLI to set up scenarios.
- Safe on build machines: no background processes and no leaked file handles.
- Clear failure messages that list the offending files and suggest a fix.

## Non-goals

- Hooks, sparse checkout, worktrees and other advanced Git features.
- Full parity with every edge case of the git CLI.

## Technology

- .NET with the LibGit2Sharp NuGet package (includes the native libgit2 binaries).
- No C++ layer. A thin C# facade over LibGit2Sharp is enough.

## Public API (sketch)

- `GitAssert.Open(string repoPath)` returns a disposable `GitAssert` instance that wraps `Repository`.
- Each check returns a `CheckResult` with `Rule` (the check's name, e.g. `NoUntrackedFiles`), `IsHealthy`, a short `Message`, and the list of `Files`. A healthy result has an empty `Files` list.
- Works with any test framework, e.g.

  ```csharp
  using var git = GitAssert.Open(repoPath);
  var result = git.NoUntrackedFiles();
  Assert.True(result.IsHealthy, result.Message);
  ```

- `git.RunAll()` runs every v1 check and returns a `RunAllResult`:
  - `Results`: one `CheckResult` per check, in a fixed order (the order of the Checks list below), healthy ones included.
  - `Failures`: the entries of `Results` where `IsHealthy` is false.
  - `IsHealthy`: true only when every check is healthy.
  - `Message`: as defined under [RunAll message](#runall-message); two broken rules produce two message blocks.
- Phase two: `git.RunAll(options)` adds options that disable checks, set allow-lists and the expected branch.

## Checks

Every check is tested against a throwaway repository: create a temp folder, `Repository.Init`, make an initial commit, set up the scenario, run the check, then dispose the repo and delete the folder. Each check has a healthy test, at least one failing test, and edge cases.

Each check's "Fix" below is the suggestion printed in its failure message, not something the library does. The exact v1 message for each check is under [Failure messages](#failure-messages).

1. **NoUntrackedFiles**: no untracked files that aren't ignored. Detect with `FileStatus.NewInWorkdir`.
   - Fix: `git add` the files if they belong, or delete them, or add them to .gitignore.
   - Tests: clean repo passes; new file in root fails and lists its path; new file in a nested untracked folder fails and lists the file (not just the folder); file matching .gitignore passes; allow-listed path passes *(phase two)*.
2. **NoUnstagedChanges**: no modified or deleted files left unstaged. Detect with `ModifiedInWorkdir`, `DeletedFromWorkdir`, `RenamedInWorkdir`.
   - Fix: `git add <file>` to stage the change, or `git restore <file>` to discard it.
   - Tests: clean repo passes; edited tracked file fails; deleted tracked file fails; line-ending-only change with autocrlf set behaves as documented (set up with LibGit2Sharp in v1; checked against the real git CLI *(phase two)*); file mode change on Windows does not fail.
3. **NoStagedUncommitted**: no staged changes relative to `HEAD` (the index matches the `HEAD` commit; the index itself is not expected to be empty). Detect with `NewInIndex`, `ModifiedInIndex`, `DeletedFromIndex`, `RenamedInIndex`.
   - Fix: `git commit`, or `git restore --staged <file>` to unstage.
   - Tests: clean repo passes; staged new file fails; staged edit fails; staged delete (`git rm`) fails; staged rename fails and reports both paths.
4. **NoMixedStaging**: no file both staged and changed again.
   - Fix: `git add <file>` to stage the latest version, then commit.
   - Tests: staged-only file passes this check; unstaged-only file passes this check; staged then edited again fails; staged then deleted from disk fails.
5. **NoConflicts**: no conflicted entries. Detect with `FileStatus.Conflicted` / `repo.Index.Conflicts`.
   - Fix: resolve the conflict markers in the file, then `git add <file>`; then finish the operation (see NoOperationInProgress).
   - Tests: clean repo passes; merge of two branches editing the same line fails and lists the file; conflict resolved and staged passes; delete/modify conflict fails.
6. **NoOperationInProgress**: no unfinished merge, rebase, cherry-pick or revert. Detect with `repo.Info.CurrentOperation`.
   - Fix: `git <operation> --continue` once conflicts are resolved, or `git <operation> --abort`, where `<operation>` is merge, rebase, cherry-pick, or revert.
   - Tests: clean repo passes; conflicting merge left open fails and names "merge"; conflicting rebase left open fails and names "rebase"; conflicting cherry-pick left open fails and names "cherry-pick"; conflicting revert left open fails and names "revert"; aborted operation passes.
7. **NotDetachedHead**: HEAD points to a branch. Detect with `repo.Info.IsHeadDetached`.
   - Fix: `git switch <branch>` (create one with `git switch -c` if the commits must be kept).
   - Tests: on a branch passes; checkout of a commit SHA fails and the message shows the SHA; fresh repo with no commits (unborn HEAD) passes.
8. **NoStaleIndexLock**: no `.git/index.lock` left behind.
   - Fix: make sure no Git process is running, then delete `.git/index.lock`.
   - Tests: no lock passes; empty `index.lock` file fails; lock in a worktree or submodule .git path is out of scope and documented.
9. **OnExpectedBranch**: HEAD is on the configured branch (optional).
   - Fix: `git switch <expected-branch>`.
   - Tests: matching branch passes; other branch fails and shows both names; not configured means the check is skipped; detached HEAD fails with a clear message.
10. **NoTrackedIgnoredFiles**: no tracked files that match ignore rules. Detect with `repo.Ignore.IsPathIgnored` on tracked paths.
    - Fix: `git rm --cached <file>` (the file stays on disk), then commit.
    - Tests: tracked file not ignored passes; file committed before the ignore rule was added fails; file force-added with `git add -f` fails; bin/ and obj/ folder content fails and is grouped by folder; negated rule (`!keep.txt`) passes.

Cross-cutting tests: `RunAll` reports every failing check at once (two broken rules give two `Failures` and two message blocks in `Message`); a clean repo makes `RunAll` healthy; disabled checks are skipped *(phase two)*; the repo handle is released after dispose (the temp folder can be deleted on Windows); a path that isn't a repository throws a clear error.

## Test tooling

- Unit tests (v1) use LibGit2Sharp to set up scenarios: in-process, fast, and no git processes to clean up.
- Phase two: a small set of integration tests creates the tricky scenarios with the real git CLI, so libgit2 is checked against real Git: autocrlf / line endings, `git add -f`, files committed before an ignore rule, a merge or rebase left in progress, and a stale index.lock.
- The CLI tests run git through CliWrap, with a timeout and cancellation so no process is left running. They're tagged as integration tests and skipped when git isn't on PATH.

## Configuration (phase two)

v1 has no configuration: `RunAll()` runs every v1 check and individual checks take no options.

- Enable or disable individual checks.
- Allow-list of paths or globs to skip per check.
- Expected branch name for OnExpectedBranch.

## Failure messages

Tests assert against these templates, so every v1 message follows them exactly. In the templates, `<...>` is a placeholder; everything else is literal.

### Common format

```text
<Rule>: <summary>
  <entry>
  <entry>
Fix:
  <command>
Or <alternative description>:
  <command>
```

- **First line:** the rule name, a colon, and a one-sentence summary with the count, e.g. `NoUnstagedChanges: 2 files have unstaged changes.` Use the singular form for one item (`1 file has`).
- **Entries:** one per line, indented two spaces. Paths are relative to the repository root, use `/` separators, and are sorted ordinally. Where a check reports a kind of change, the entry is `<kind>: <path>`.
- **Limit:** at most 20 entries. When there are more, the 21st line is `  ...and <n> more. Run the check again after fixing these.` `CheckResult.Files` still holds every offending path.
- **`Fix:`** one command line, indented two spaces, that fixes exactly the listed entries when run from the repository root. Paths are separated from options with `--`.
- **`Or ...:`** zero or more alternatives, each with its own command line, or a plain sentence when there is no single command (e.g. editing .gitignore).
- **Quoting:** a path is wrapped in double quotes when it contains anything other than letters, digits, `.`, `_`, `-`, and `/`, e.g. `"docs/my notes.md"`. This works the same in bash, PowerShell, and cmd. Paths that contain `"`, `$`, or a backtick are out of scope for v1.
- **Line endings:** `\n` between lines, no trailing newline.
- **Healthy result:** `Message` is `<Rule>: OK.` and `Files` is empty.

### NoUntrackedFiles

```text
NoUntrackedFiles: <n> untracked file(s) found.
  <path>
Fix:
  git add -- <paths>
Or delete them:
  git clean -f -- <paths>
Or ignore them: add the paths to .gitignore.
```

Example:

```text
NoUntrackedFiles: 2 untracked files found.
  docs/my notes.md
  tmp/notes.txt
Fix:
  git add -- "docs/my notes.md" tmp/notes.txt
Or delete them:
  git clean -f -- "docs/my notes.md" tmp/notes.txt
Or ignore them: add the paths to .gitignore.
```

### NoUnstagedChanges

Entry kinds: `modified`, `deleted`, `renamed` (`renamed: <old> -> <new>`; both paths go into the commands).

```text
NoUnstagedChanges: <n> file(s) have unstaged changes.
  <kind>: <path>
Fix:
  git add -- <paths>
Or discard the changes:
  git restore -- <paths>
```

Example:

```text
NoUnstagedChanges: 2 files have unstaged changes.
  deleted: old.txt
  modified: readme.txt
Fix:
  git add -- old.txt readme.txt
Or discard the changes:
  git restore -- old.txt readme.txt
```

### NoStagedUncommitted

Entry kinds: `new`, `modified`, `deleted`, `renamed` (`renamed: <old> -> <new>`; both paths go into the unstage command).

```text
NoStagedUncommitted: <n> staged change(s) not committed.
  <kind>: <path>
Fix:
  git commit
Or unstage them:
  git restore --staged -- <paths>
```

Example:

```text
NoStagedUncommitted: 2 staged changes not committed.
  new: src/Feature.cs
  renamed: a.txt -> b.txt
Fix:
  git commit
Or unstage them:
  git restore --staged -- src/Feature.cs a.txt b.txt
```

### NoConflicts

```text
NoConflicts: <n> file(s) have unresolved conflicts.
  conflicted: <path>
Fix: resolve the conflicts in each file, then run:
  git add -- <paths>
```

Example:

```text
NoConflicts: 1 file has unresolved conflicts.
  conflicted: src/Program.cs
Fix: resolve the conflicts in each file, then run:
  git add -- src/Program.cs
```

### NoOperationInProgress

No entries; `Files` is empty even on failure. `<operation>` is `merge`, `rebase`, `cherry-pick`, or `revert` (all libgit2 rebase variants map to `rebase`).

```text
NoOperationInProgress: a <operation> is in progress.
Fix: once all conflicts are resolved and staged, run:
  git <operation> --continue
Or abort it:
  git <operation> --abort
```

Example:

```text
NoOperationInProgress: a merge is in progress.
Fix: once all conflicts are resolved and staged, run:
  git merge --continue
Or abort it:
  git merge --abort
```

Any other operation libgit2 reports (e.g. `am`, `bisect`) uses `a Git operation (<name>) is in progress.` with the fix line `Fix: run "git status" to see how to finish or abort it.` and no command. It is not tested in v1.

### NoTrackedIgnoredFiles

When an ignored folder contains tracked files, the folder is one entry, `<folder>/ (<k> files)`, and the command removes it recursively. Other files are listed one by one. `Files` holds every individual file.

```text
NoTrackedIgnoredFiles: <n> tracked path(s) match ignore rules.
  <path>
  <folder>/ (<k> files)
Fix (the files stay on disk), then commit:
  git rm -r --cached -- <paths and folders>
```

Example:

```text
NoTrackedIgnoredFiles: 2 tracked paths match ignore rules.
  bin/ (12 files)
  secrets.json
Fix (the files stay on disk), then commit:
  git rm -r --cached -- bin/ secrets.json
```

### RunAll message

When every check is healthy, `Message` is `All Git checks passed.`

Otherwise it is a header line followed by the `Message` of each failing check, in `Results` order, separated by a blank line:

```text
<f> of 6 Git checks failed.

<failing check message>

<failing check message>
```

Example:

```text
2 of 6 Git checks failed.

NoUntrackedFiles: 1 untracked file found.
  tmp/notes.txt
Fix:
  git add -- tmp/notes.txt
Or delete them:
  git clean -f -- tmp/notes.txt
Or ignore them: add the paths to .gitignore.

NoUnstagedChanges: 1 file has unstaged changes.
  modified: readme.txt
Fix:
  git add -- readme.txt
Or discard the changes:
  git restore -- readme.txt
```

Phase-two checks will follow the same common format when they are added.

## Implementation notes and catches

- Always dispose `Repository` (wrap it in `using`). Leaked handles can lock files in .git on Windows.
- Make sure the right native libgit2 binary is copied to the output on every build agent and platform.
- Status can differ a little from `git status` because of autocrlf / line endings, file mode, and some gitignore edge cases. Turn on `RecurseUntrackedDirs` and keep `IncludeIgnored` off when checking untracked files.
- LibGit2Sharp is maintained but releases slowly.

## Open items

- Review the real repositories for other scenarios to add.

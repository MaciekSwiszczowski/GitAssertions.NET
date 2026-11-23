# GitAssertions.NET — Library Spec

## Purpose

GitAssertions.NET (namespace GitAssertions) is a small .NET library for writing Git state tests (architecture-style unit tests) that fail when an AI agent finishes a task and leaves the repository in a broken or messy state. A failing test tells the agent exactly what's wrong, so it can clean up the Git state itself before it finishes.

The library is **read-only**. It inspects the repository and reports what it finds; it never stages, commits, deletes, resets, or changes any file, ref, or setting. A failure message gives a `Fix:` command built from the actual offending paths, ready to paste into a terminal. A path that isn't shell-safe is left out of the command, and when no listed path is shell-safe the command is left out and the message says to handle the paths by hand (see [Shell-safe paths](#shell-safe-paths)). Fix commands are suggestions: read them before running them. Running one is always the caller's decision (see [Security](#security)).

## Scope

Version 1 (v1) was a two-day build done with agents, released as 0.1; it is not 1.0, the first public release. The phases after it in [tasks.md](tasks.md) add integration tests with real Git, accurate messages, security, release preparation, and nice-to-have features, and tasks.md tracks which are done.

- **Version 1:** NoUntrackedFiles, NoUnstagedChanges, NoConflicts, NoOperationInProgress, NoTrackedIgnoredFiles, and a plain RunAll with no options (no check can be turned off).
- **Later:** a name of its own in NoOperationInProgress for the rarest operation left (`git am`), a `typechange` entry kind for NoUnstagedChanges (a tracked file replaced by another type, such as a symlink), NoMixedStaging (a narrower check than NoUnstagedChanges, which also reports a file changed again after it was staged; its own method, left out of the default `RunAll` so no file prints twice), NotDetachedHead, OnExpectedBranch, NoStaleIndexLock, configuration (enabling and disabling checks, allow-lists, expected branch), and NuGet packaging. Use a project reference until then.

Test scenarios that depend on a later feature (allow-listed paths, disabled checks) are marked *(later)* below.

## Goals

- Readable, one-line assertions per rule.
- No external git.exe processes in the library at runtime. Everything the library does runs in-process through LibGit2Sharp (libgit2). This rule applies to the shipped library only; the integration tests deliberately call the git CLI to set up scenarios.
- Safe on build machines: no background processes and no leaked file handles.
- Clear failure messages that list the offending files and suggest a fix.

## Non-goals

- Hooks, sparse checkout, worktrees and other advanced Git features.
- Full parity with every edge case of the git CLI.
- A check for staged changes that are not committed. An agent is expected to stage its work and leave the commit to a person, so a staged change is the healthy end of a task, not a broken state.

## Technology

- .NET with the LibGit2Sharp NuGet package (includes the native libgit2 binaries).
- No C++ layer. A thin C# facade over LibGit2Sharp is enough.

## Public API

- `GitAssert.Open(string repoPath)` checks that the path is a repository root and returns a `GitAssert` that keeps the path. It holds no `Repository` handle: each check opens the repository, reads it, and disposes it before returning, also when `RunAll` calls it. So `GitAssert` is not disposable, an instance may be stored and shared between tests running in parallel, and the repository folder can be deleted as soon as a check returns.
- `Open` throws `ArgumentException` for a path that is not a repository root, and for a bare repository, which has no working tree for the checks to read.
- No LibGit2Sharp type reaches the caller, exceptions included: a LibGit2Sharp exception from `Open` or a check is re-thrown as `InvalidOperationException` with the same message and no inner exception.
- Each check returns a `CheckResult` with `Rule` (the check's name, e.g. `NoUntrackedFiles`), `IsHealthy`, a short `Message`, and the list of `Files`. `Files` holds every offending path, sorted ordinally and never truncated; a healthy result has an empty `Files` list.
- Works with any test framework, e.g.

  ```csharp
  var git = GitAssert.Open(repoPath);
  var result = git.NoUntrackedFiles();
  Assert.True(result.IsHealthy, result.Message);
  ```

- `git.RunAll()` runs every v1 check, one after another, each exactly as if it were called on its own, and returns a `RunAllResult`:
  - `Results`: one `CheckResult` per check, in a fixed order (the order of the Checks list below), healthy ones included.
  - `Failures`: the entries of `Results` where `IsHealthy` is false.
  - `IsHealthy`: true only when every check is healthy.
  - `Message`: as defined under [RunAll message](#runall-message); two broken rules produce two message blocks.
- A failing check does not stop `RunAll`: the checks after it still run. A check that throws does: `RunAll` throws the same exception, and the checks after it do not run. Stopping is deliberate: a repository that one check cannot read usually fails the others too, so going on would only repeat the error, and each check could first spend its own time failing, which on a large or broken working tree is slow.
- Later: `git.RunAll(options)` adds options that disable checks, set allow-lists and the expected branch.

## Checks

Every check is tested against a throwaway repository in a temp folder that is deleted afterwards. A unit test sets it up in-process: `Repository.Init`, an initial commit unless the scenario needs a repository with no commits, then the scenario; the repository is disposed before the folder is deleted. A script scenario sets it up with the real git CLI (see [Test tooling](#test-tooling)). Each check has a healthy test, at least one failing test, and edge cases.

Each check's "Fix" below is the suggestion printed in its failure message, not something the library does. The exact v1 message for each check is under [Failure messages](#failure-messages).

1. **NoUntrackedFiles**: no untracked files that aren't ignored. Detect with `FileStatus.NewInWorkdir`.
   - Fix: `git add` the files if they belong, or delete them, or add them to .gitignore.
   - Tests: clean repo passes; a repository with no commits passes; new file in root fails and lists its path; new file in a nested untracked folder fails and lists the file (not just the folder); file matching .gitignore passes; empty folders pass; edits, deletes, and staged new files pass (NoUnstagedChanges reports the edits and deletes); a file moved without `git mv` lists its new path, as `git status` does; allow-listed path passes *(later)*.
2. **NoUnstagedChanges**: no modified or deleted files left unstaged. Detect with `ModifiedInWorkdir`, `DeletedFromWorkdir`. The check reports what `git status` reports, so a file moved without `git mv` is its old path, deleted (its new path is untracked, for NoUntrackedFiles), and a change of line endings only is reported when `git status` reports it. A tracked file replaced by another type, such as a symlink, which `git status` prints as `typechange`, is not reported in v1 (later). A file recorded with `git add -N` (intent to add), which `git status` prints as ` A` and lists as `new file`, is a kind of its own, `new (intent to add)`. LibGit2Sharp does not expose the intent-to-add flag, so the file is recognized by its state: not in HEAD, the empty blob in the index, and changed in the working tree. An empty file staged with a plain `git add` and written afterwards (`AM`) has the same state, and gets the same kind. A known gap, accepted: an empty file recorded with `git add -N` matches the empty blob in the index, so nothing marks it as changed, and the check passes it where `git status` prints ` A`; no content can be lost.
   - Fix: `git add <file>` to stage the change, or `git restore <file>` to discard it. `git restore` is not offered for an intent-to-add file: the index holds it with no content, so `git restore` would empty it and leave it ` A`.
   - Tests: clean repo passes; a repository with no commits passes; edited tracked file fails; deleted tracked file fails; a file moved without `git mv` lists the old path as deleted; a file staged and then edited again fails as modified (`MM`, `AM`), and one staged and then deleted fails as deleted (`AD`); line-ending-only change with autocrlf set behaves as documented (set up with LibGit2Sharp, and checked against the real git CLI as scripts, with `.gitattributes` and `core.autocrlf=input` too); file mode change on Windows does not fail; an unresolved conflict passes (NoConflicts reports it); a file recorded with `git add -N` fails as `new (intent to add)` and is offered `git add` and no `git restore` (real Git, as a script); the same file next to an edited tracked file is listed with it, and `git restore` names only the edited file (real Git, as a script); an empty file staged and then written fails as `new (intent to add)` too, also in a repository with no commits; a committed empty file that was written fails as `modified` and is still offered `git restore`, because HEAD holds it; an empty file recorded with `git add -N` passes, the known gap above (real Git, as a script, with the disagreement between git's block and the `RunAll` result recorded in its comment).
3. **NoMixedStaging** *(later)*: no file both staged and changed again. Narrower than NoUnstagedChanges, which reports these files too, and the only check for someone who keeps other unstaged edits on purpose. Called on its own, not part of the default `RunAll`.
   - Fix: `git add <file>` to stage the latest version, then commit.
   - Tests: staged-only file passes this check; unstaged-only file passes this check; staged then edited again fails; staged then deleted from disk fails.
4. **NoConflicts**: no conflicted entries. Detect with `repo.Index.Conflicts`: the paths that still hold conflict stages in the index, as `git ls-files -u` lists them. The check reads the index only, so a file staged with conflict markers left in it passes.
   - Fix: resolve the conflicts in the file, then `git add <file>`, which also stages a deletion when the resolution was to delete it; then finish the operation (see NoOperationInProgress).
   - Tests: clean repo passes; repository with no commits passes; merge of two branches editing the same line fails and lists the file; conflict resolved and staged passes; delete/modify conflict fails; add/add conflict (both branches created the same file) fails; binary-file conflict fails.
5. **NoOperationInProgress**: no unfinished merge, rebase, cherry-pick, revert, or bisect. Detect with `repo.Info.CurrentOperation`.
   - Fix: `git <operation> --continue` once any conflicts are resolved and staged (an operation can stop with none), or `git <operation> --abort`, where `<operation>` is merge, rebase, cherry-pick, or revert; `git bisect reset` for a bisect.
   - Tests: clean repo passes; a repository with no commits passes; conflicting merge left open fails and names "merge"; conflicting rebase left open fails and names "rebase"; conflicting cherry-pick left open fails and names "cherry-pick"; conflicting revert left open fails and names "revert"; a cherry-pick or revert of several commits fails and names "cherry-pick" or "revert"; a bisect fails and names "bisect"; a merge stopped before its commit (`git merge --no-commit`) fails and names "merge"; a rebase stopped with no conflict, after a failing `--exec` command, fails and names "rebase" (an `edit` step leaves the same state, and needs an edited list of rebase steps, which the script runner cannot pass); a conflicting rebase aborted with LibGit2Sharp's `Rebase.Abort()` passes; a conflicting merge aborted with `git merge --abort` passes (real Git, because LibGit2Sharp has no merge abort).
6. **NotDetachedHead**: HEAD points to a branch. Detect with `repo.Info.IsHeadDetached`.
   - Fix: `git switch <branch>` (create one with `git switch -c` if the commits must be kept).
   - Tests: on a branch passes; checkout of a commit SHA fails and the message shows the SHA; fresh repo with no commits (unborn HEAD) passes.
7. **NoStaleIndexLock**: no `.git/index.lock` left behind.
   - Fix: make sure no Git process is running, then delete `.git/index.lock`.
   - Tests: no lock passes; empty `index.lock` file fails; lock in a worktree or submodule .git path is out of scope and documented.
8. **OnExpectedBranch**: HEAD is on the configured branch (optional).
   - Fix: `git switch <expected-branch>`.
   - Tests: matching branch passes; other branch fails and shows both names; not configured means the check is skipped; detached HEAD fails with a clear message.
9. **NoTrackedIgnoredFiles**: no tracked files that match ignore rules. Detect with `repo.Ignore.IsPathIgnored` on tracked paths. The check reads every ignore source that `git ls-files -i -c --exclude-standard` reads: the repository's `.gitignore` files, `.git/info/exclude`, and the file named by `core.excludesFile`.
   - Fix: `git rm --cached <file>` (the file stays on disk), then commit. No line offers to keep a file tracked by changing the ignore rules: the right edit depends on which rule matched, which libgit2 does not report.
   - Tests: a repository with no commits passes; tracked file not ignored passes; file committed before the ignore rule was added fails; file force-added with `git add -f` fails; bin/ and obj/ folder content fails and is grouped by folder; negated rule (`!keep.txt`) passes; with `bin/*` and `!bin/keep.txt`, a tracked `bin/keep.txt` passes and a tracked `bin/app.dll` fails as an entry of its own, not grouped, because the folder itself is not ignored.

Cross-cutting tests: `RunAll` reports every failing check at once (two broken rules give two `Failures` and two message blocks in `Message`, and the checks after them still run); a clean repo makes `RunAll` healthy, and so does a repository with no commits; `RunAll` does not repeat each check's own scenarios, apart from that one; disabled checks are skipped *(later)*; no repo handle outlives a check (the temp folder can be deleted on Windows right after a check, with nothing disposed); a path that isn't a repository throws a clear error.

## Test tooling

- Unit tests use LibGit2Sharp to set up scenarios: in-process, fast, and no git processes to clean up. They cut libgit2 off from the machine's and the user's git config, so a scenario gives the same result on every machine.
- Both test projects run every test in parallel, the tests of one class included (xUnit's parallel mode `all`), so the suite itself exercises a `GitAssert` shared between parallel tests. A test that changes process-wide state, such as the current directory, runs alone.
- `ThisRepositoryTests` runs every check, and `RunAll`, on this repository itself. It lives in a test project of its own, `GitAssertions.ThisRepository.Tests`, which does not cut libgit2 off from git config: libgit2 settings are process-wide, and a real repository has to be read as `git status` reads it, as a user's own test project reads theirs.
- Integration tests (Phase 5 in tasks.md) create the tricky scenarios with the real git CLI, as scripts, so libgit2 is checked against real Git: line endings (`core.autocrlf` and `.gitattributes`), `git add -f`, files committed before an ignore rule, negated ignore rules, and operations left in progress. They also set up the states libgit2 cannot create at all, such as bisect, a cherry-pick or revert of several commits, and a rebase stopped by a failing `--exec`; those scenarios are tested with real Git only, never by writing Git's state files by hand. A stale index.lock will be tested the same way, with the NoStaleIndexLock check *(later)*.
- The CLI tests run git through CliWrap, with a timeout that kills git's whole process tree, so no process is left running. Git is cut off from the machine's and the user's config, as libgit2 is in the unit tests. The tests are tagged as integration tests and run on CI with the others, on every platform CI runs. A missing git, or one older than 2.32, fails them instead of skipping them, so they cannot drop out unnoticed.
- A script scenario's verified file holds git's own answer about the repository (`git status --porcelain --untracked-files=all` and `git ls-files -i -c --exclude-standard`) above the `RunAll` result, so a disagreement between the library and git shows in one file, and a git version that answers differently fails the test.

### Platforms

- CI builds and runs the tests on Windows, Linux, and macOS (`windows-latest`, `ubuntu-latest`, and `macos-latest`), one job per platform (a matrix, so one platform failing does not cancel the others). Linux and macOS behave differently in ways the checks depend on: the executable bit is a real file mode, symbolic links need no privileges, and no machine-wide `core.autocrlf` is set. Linux paths are case-sensitive, macOS paths are case-insensitive by default, and a macOS temporary folder is reached through a symbolic link (`/var` is `/private/var`), so a path read back from Git can differ from the one the test created. A test that depends on one platform's behavior, such as the file mode test, is written for each platform or skipped on the one where it does not apply, and says so in its header.
- The missing `core.autocrlf` is why a file that is CRLF in the working tree and LF in the index reads as modified on Linux and macOS, while Windows hides it. `ThisRepositoryTests` shows it: it reads this repository as `git status` does. Files here are LF (`.editorconfig`: `end_of_line = lf`).

## Configuration (later)

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
- **Entries:** one per line, indented two spaces. Paths are relative to the repository root and use `/` separators. Where a check reports a kind of change, the entry is `<kind>: <path>`. Entries are sorted ordinally by their whole line, so the entries of one kind stay together (`deleted: old.txt` comes before `modified: readme.txt`), and commands list the paths in entry order.
- **Limit:** at most 20 entries. When there are more, the 21st line is `  ...and <n> more. Run the check again after fixing these.` `CheckResult.Files` still holds every offending path. Path length and total message size are capped too (see [Output limits](#output-limits)).
- **`Fix:`** one command line, indented two spaces, that fixes exactly the listed entries when run from the repository root. Paths are separated from options with `--`.
- **`Or ...:`** zero or more alternatives, each with its own command line, or a plain sentence when there is no single command (e.g. editing .gitignore).
- **Paths per command:** a command takes the paths of the listed entries it is right for, in entry order. Most take every entry; one that is wrong for a kind of entry leaves those out (`git restore` in NoUnstagedChanges leaves out the intent-to-add entries), and a command with no path left is left out with its label line.
- **Quoting:** a path goes into a command only if it is *shell-safe* (see [Shell-safe paths](#shell-safe-paths)). A shell-safe path is wrapped in double quotes when it contains anything other than ASCII letters and digits, `.`, `_`, `-`, and `/`, e.g. `"docs/my notes.md"` or `"docs/café.md"`. Inside double quotes, the allowed characters are literal in bash, PowerShell, and cmd.
- **Unsafe paths:** a path that isn't shell-safe is still listed as an entry, in escaped form, but is left out of every command. After the commands, the message adds `Not included in the commands above because the path contains characters that are unsafe in a shell; handle it by hand:` followed by each such path, escaped and indented two spaces. If no listed path is shell-safe, the command lines are omitted and only this note is printed (a sentence-only fix line, which holds no command, stays). The note names only a path that a command with paths would have taken, so a check whose fixes take no paths prints none.
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

Entry kinds: `modified`, `deleted`, `new (intent to add)`. There is no `renamed` kind: a file moved without `git mv` is listed as its old path, `deleted`. `new (intent to add)` is a file recorded with `git add -N`, which Git holds with no content: it gets `git add`, and no `git restore`, which would empty the file and leave it unstaged still.

```text
NoUnstagedChanges: <n> file(s) have unstaged changes.
  <kind>: <path>
Fix:
  git add -- <paths>
Or discard the changes:
  git restore -- <paths>
```

`git add` takes every listed path. `git restore` takes every listed path except the `new (intent to add)` ones, and the `Or discard the changes:` block is left out when no path is left for it.

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

Example with a file recorded with `git add -N`:

```text
NoUnstagedChanges: 2 files have unstaged changes.
  modified: readme.txt
  new (intent to add): notes.txt
Fix:
  git add -- readme.txt notes.txt
Or discard the changes:
  git restore -- readme.txt
```

Example with only that file:

```text
NoUnstagedChanges: 1 file has unstaged changes.
  new (intent to add): notes.txt
Fix:
  git add -- notes.txt
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

No entries; `Files` is empty even on failure. `<operation>` is `merge`, `rebase`, `cherry-pick`, or `revert` (all libgit2 rebase variants map to `rebase`, and a cherry-pick or revert of several commits, `CherryPickSequence` and `RevertSequence`, maps to `cherry-pick` and `revert` like one of a single commit). A bisect (`Bisect`) has no `--continue` and no `--abort`: it ends with `git bisect reset`, so it has a template of its own, below.

```text
NoOperationInProgress: a <operation> is in progress.
Fix: once any conflicts are resolved and staged, run:
  git <operation> --continue
Or abort it:
  git <operation> --abort
```

Example:

```text
NoOperationInProgress: a merge is in progress.
Fix: once any conflicts are resolved and staged, run:
  git merge --continue
Or abort it:
  git merge --abort
```

An operation can also stop with no conflict: `git merge --no-commit` stops before the merge commit, and an interactive rebase stops at an `edit` step or after an `--exec` command fails. The check cannot tell the two cases apart, because the state does not: a merge is `Merge` with a conflict or without one, and a rebase stopped on a conflict is `RebaseInteractive`, the same as one stopped without. So every operation has one wording, "once any conflicts are resolved and staged", which is true in both cases, and the commands after it fit both. When there are conflicts, NoConflicts' own block in the `RunAll` message already says to resolve them and to run `git add`.

A bisect, `git bisect start` and every step after it until `git bisect reset`, is named as a bisect. It checks out a commit of its own and keeps its state in the repository until `git bisect reset` ends it and returns to the branch or commit where it started, so that command is the only fix; the message names no file. It is not an operation that stopped on a conflict, so the Fix line does not mention conflicts.

```text
NoOperationInProgress: a bisect is in progress.
Fix: to end it and return to where you started, run:
  git bisect reset
```

Any other operation libgit2 reports uses `a Git operation (<name>) is in progress.` with the fix line `Fix: run "git status" to see how to finish or abort it.` and no command. `<name>` is libgit2's name for it, the `CurrentOperation` value. Only `git am` (`ApplyMailbox`, `ApplyMailboxOrRebase`) is left to it; a later task gives `git am` its own name and Fix lines. It is not tested.

```text
NoOperationInProgress: a Git operation (<name>) is in progress.
Fix: run "git status" to see how to finish or abort it.
```

Example:

```text
NoOperationInProgress: a Git operation (ApplyMailbox) is in progress.
Fix: run "git status" to see how to finish or abort it.
```

### NoTrackedIgnoredFiles

When a folder is itself ignored and every tracked file under it is ignored, the folder is one entry, `<folder>/ (<k> files)` (`(1 file)` for one), and the command removes it recursively. The entry is the topmost such folder, so `src/App/bin/` stands for everything under it. A folder that holds a tracked file the check does not report is never grouped, so the command never untracks a file the message did not list. Other files are listed one by one. `<n>` counts entries, a folder as one; `Files` holds every individual file.

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
<f> of 5 Git checks failed.

<failing check message>

<failing check message>
```

Example:

```text
2 of 5 Git checks failed.

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

Later checks will follow the same common format when they are added.

## Security

The library is read-only and runs in-process, so its attack surface is small. These rules cover what remains.

### Shell-safe paths

File names come from the repository, which may be hostile (for example, a repo an agent cloned). A person or an agent may paste a `Fix:` command straight into a shell, so a crafted name such as `$(rm -rf ~).txt`, or one that contains a newline, must never be able to inject a command.

- A path is **shell-safe** when every character is a Unicode letter or digit, a space, or one of `. _ - / + , = @`, and it does not start with `:` (git pathspec magic). Anything else makes the path unsafe, including `$`, backtick, `"`, `'`, `%`, `!`, `^`, `&`, `|`, `;`, `<`, `>`, `(`, `)`, `*`, `?`, `[`, `]`, `{`, `}`, `#`, `~`, and every control or format character (Unicode categories Cc and Cf, which include newline, tab, ESC, and bidirectional overrides).
- Only shell-safe paths appear in commands (see [Common format](#common-format)). Unsafe paths are never quoted or escaped into a command, because no single escaping is correct for bash, PowerShell, and cmd at once.
- **Escaped display:** wherever a path is printed outside a command (entries and the unsafe-path note), control and format characters are replaced with escapes: `\n`, `\r`, `\t`, and `\u{XXXX}` for the rest (hex code point). A literal backslash is printed as `\\`. This keeps one path on one line and stops terminal escape sequences from reaching logs.
- The rest of each message is fixed text from the templates, so paths are the only untrusted input.

Example with one safe and one unsafe untracked file (the second name contains `$(...)` and a newline):

```text
NoUntrackedFiles: 2 untracked files found.
  $(rm -rf ~)\n.txt
  tmp/notes.txt
Fix:
  git add -- tmp/notes.txt
Or delete them:
  git clean -f -- tmp/notes.txt
Or ignore them: add the paths to .gitignore.
Not included in the commands above because the path contains characters that are unsafe in a shell; handle it by hand:
  $(rm -rf ~)\n.txt
```

### Output limits

- At most 20 entries per check (see [Common format](#common-format)).
- A path longer than 200 characters is shortened, in entries and in the unsafe-path note, to its first 100 and last 60 characters joined by `...`, and it is treated as unsafe, so it never appears in a command. The shortening comes first and the escaping after it, so a cut never splits an escape; a cut that would split a surrogate pair (a character such as an emoji, two UTF-16 units) drops the left-over half.
- A single check's `Message` is at most 16,000 characters. If a message would exceed that (which the other limits should prevent), it is cut at the last full line that fits and ends with `  ...message truncated.` A `RunAll` message is therefore at most five check messages plus its header.
- `CheckResult.Files` is not truncated; it holds the raw paths for programmatic use.

### Untrusted repositories and libgit2

- The library never executes anything a repository asks for: no hooks, no filter drivers (clean/smudge/process commands from `.gitattributes` and config), no fsmonitor, and no other external commands. LibGit2Sharp does not run hooks or external filter drivers, and the library registers no filters of its own.
- The library does not change libgit2's global settings. In particular, it leaves repository owner validation (`safe.directory`) switched on.
- libgit2's settings belong to the whole process, so the two rules above hold only while the calling process leaves them alone: a filter its own code registered through LibGit2Sharp (`GlobalSettings.RegisterFilter`) runs during the checks, as during any libgit2 read, and owner validation it switched off stays off.
- Who can bring what: a clone brings only the files of its commits, so a hostile file name is all it can carry, and the message rules handle names. Its `.git` folder (config, hooks, alternates) is written on this machine. A repository folder copied in whole, such as an unpacked archive, a shared folder, or a Docker volume, brings its own `.git` as well, and its config can name files elsewhere, even on a network share (`include.path`, `core.excludesFile`, `.git/objects/info/alternates`), which libgit2 reads; on Windows, opening a file on a share can log in to that server automatically and hand it the user's credentials to crack or relay. The checks are meant for repositories cloned or created on this machine, and any other repository folder is untrusted input.
- Opening a hostile repository still runs native libgit2 parsing code. libgit2 fixes arrive with LibGit2Sharp releases, through Dependabot (see [Supply chain](#supply-chain)).

### Information in messages

Messages usually end up in test output and CI logs, so they reveal file names, which may include sensitive ones such as `.env` or `secrets.json`. They never include file contents. This is accepted by design; callers who publish CI logs should keep that in mind.

### Test cleanup

`TempRepo` keeps its repository in a `TemporaryDirectory` (Meziantou.Framework.TemporaryDirectory), a uniquely named folder under the system temp directory that is deleted on dispose. Its delete clears read-only flags (libgit2 writes read-only files under `.git/objects`), retries a locked file, and removes a junction or symlink as a link without following it, so nothing outside the folder is touched. `TempRepo` releases the repository handle before the folder is deleted.

### Supply chain

- Every GitHub Action is pinned to a full commit SHA, with its exact release in a comment: `uses: actions/checkout@<40 hex digits> # v7.0.1`. A tag can be moved by whoever controls the action's repository, and every run then executes the commit it points to; a commit SHA cannot be moved. A SHA is taken from the action's own repository, never from a fork.
- Dependabot keeps NuGet packages and GitHub Actions up to date (`.github/dependabot.yml`), with Dependabot alerts and security updates switched on. It is the whole policy for libgit2 as well: libgit2's own advisories are filed against the C library, not against the NuGet package, so neither Dependabot nor NuGetAudit flags them, and a libgit2 fix arrives when LibGit2Sharp ships a release with it, as an ordinary update. There is no separate watch of libgit2 advisories: this is a small tool, and it gets no more upkeep than it critically needs. The file has one entry for each ecosystem, both at the repository root and weekly, with the analyzer packages in one group, and it ignores `Verify.XunitV3`, which `Directory.Packages.props` pins to its last free version.
- Package versions are central (`Directory.Packages.props`), exact, and never floating. There are no NuGet lock files. A library's lock file does not reach the projects that use it. NuGet resolves the lowest applicable version, so exact versions already make a restore repeatable, and a version missing from the feed makes NuGet take a higher one with warning NU1603, which `TreatWarningsAsErrors` turns into an error. Lock files would also differ by configuration, because the analyzer packages are referenced under `Debug_Analyzers` only, and a locked restore breaks when the SDK rolls forward (`global.json`, `latestFeature`).
- CI workflows run with read-only token permissions (`permissions: contents: read`). In place already: keep it in every new workflow.
- Publishing to NuGet *(later)*: the package has SourceLink and no author signature. A code-signing certificate costs money every year, and most packages on nuget.org have none; nuget.org signs every package with its own repository signature, so what vouches for a version is who can push it. The publish job uses nuget.org Trusted Publishing: it exchanges GitHub's OIDC token (`permissions: id-token: write`) for a short-lived API key through the `NuGet/login` action, so no long-lived key is stored in the repository's secrets. Only that job gets `id-token: write`, and every action in its workflow is SHA-pinned.

## Implementation notes and catches

- Always dispose `Repository` (wrap it in `using`). Leaked handles can lock files in .git on Windows. `GitAssert` opens one per check and disposes it before returning; it never keeps one in a field, because a shared handle disposed on one thread while another reads it is a native access violation that ends the test run.
- Make sure the right native libgit2 binary is copied to the output on every build agent and platform.
- Status can differ a little from `git status` because of autocrlf / line endings, file mode, and some gitignore edge cases; the script scenarios check the known ones against real Git (see [Test tooling](#test-tooling)). Turn on `RecurseUntrackedDirs` and keep `IncludeIgnored` off when checking untracked files.
- LibGit2Sharp is maintained but releases slowly, so a libgit2 fix can take weeks to reach this library. It arrives through Dependabot (see [Supply chain](#supply-chain)).

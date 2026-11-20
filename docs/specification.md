# GitAssertions.NET — Library Spec

## Purpose

GitAssertions.NET (namespace GitAssertions) is a small .NET library for writing Git state tests (architecture-style unit tests) that fail when an AI agent finishes a task and leaves the repository in a broken or messy state. A failing test tells the agent exactly what's wrong, so it can clean up the Git state itself before it finishes.

The library is **read-only**. It inspects the repository and reports what it finds; it never stages, commits, deletes, resets, or changes any file, ref, or setting. A failure message gives a `Fix:` command built from the actual offending paths, ready to paste into a terminal. A path that isn't shell-safe is left out of the command, and when no listed path is shell-safe the command is left out and the message says to handle the paths by hand (see [Shell-safe paths](#shell-safe-paths)). Fix commands are suggestions: read them before running them. Running one is always the caller's decision (see [Security](#security)).

## Scope

Version 1 is a two-day build done with agents. Everything else waits for phase two and gets added when it's needed.

- **Version 1:** NoUntrackedFiles, NoUnstagedChanges, NoConflicts, NoOperationInProgress, NoTrackedIgnoredFiles, and a plain RunAll with no options (every v1 check always runs). Unit tests use LibGit2Sharp only, so every v1 scenario is set up in-process.
- **Phase two:** a `typechange` entry kind for NoUnstagedChanges (a tracked file replaced by another type, such as a symlink), an intent-to-add kind for NoUnstagedChanges (a file recorded with `git add -N`, which gets `git add` and no discard line), NoMixedStaging (a narrower check than NoUnstagedChanges, which also reports a file changed again after it was staged; its own method, left out of the default `RunAll` so no file prints twice), NotDetachedHead, OnExpectedBranch, NoStaleIndexLock, configuration (enabling and disabling checks, allow-lists, expected branch), the CliWrap real-Git integration tests, and NuGet packaging. Use a project reference until then.

Test scenarios that depend on a phase-two feature (allow-listed paths, disabled checks, verification against the real git CLI) are marked *(phase two)* below and are not part of v1.

## Goals

- Readable, one-line assertions per rule.
- No external git.exe processes in the library at runtime. Everything the library does runs in-process through LibGit2Sharp (libgit2). This rule applies to the shipped library only; the phase-two integration tests deliberately call the git CLI to set up scenarios.
- Safe on build machines: no background processes and no leaked file handles.
- Clear failure messages that list the offending files and suggest a fix.

## Non-goals

- Hooks, sparse checkout, worktrees and other advanced Git features.
- Full parity with every edge case of the git CLI.
- A check for staged changes that are not committed. An agent is expected to stage its work and leave the commit to a person, so a staged change is the healthy end of a task, not a broken state.

## Technology

- .NET with the LibGit2Sharp NuGet package (includes the native libgit2 binaries).
- No C++ layer. A thin C# facade over LibGit2Sharp is enough.

## Public API (sketch)

- `GitAssert.Open(string repoPath)` checks that the path is a repository root and returns a `GitAssert` that keeps the path. It holds no `Repository` handle: each check opens the repository, reads it, and disposes it before returning (`RunAll` opens it once for all checks). So `GitAssert` is not disposable, an instance may be stored and shared between tests running in parallel, and the repository folder can be deleted as soon as a check returns.
- `Open` throws `ArgumentException` for a path that is not a repository root, and for a bare repository, which has no working tree for the checks to read.
- No LibGit2Sharp type reaches the caller, exceptions included: a LibGit2Sharp exception from `Open` or a check is re-thrown as `InvalidOperationException` with the same message and no inner exception.
- Each check returns a `CheckResult` with `Rule` (the check's name, e.g. `NoUntrackedFiles`), `IsHealthy`, a short `Message`, and the list of `Files`. `Files` holds every offending path, sorted ordinally and never truncated; a healthy result has an empty `Files` list.
- Works with any test framework, e.g.

  ```csharp
  var git = GitAssert.Open(repoPath);
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
   - Tests: clean repo passes; new file in root fails and lists its path; new file in a nested untracked folder fails and lists the file (not just the folder); file matching .gitignore passes; empty folders pass; edits, deletes, and staged new files pass (NoUnstagedChanges reports the edits and deletes); a file moved without `git mv` lists its new path, as `git status` does; allow-listed path passes *(phase two)*.
2. **NoUnstagedChanges**: no modified or deleted files left unstaged. Detect with `ModifiedInWorkdir`, `DeletedFromWorkdir`. The check reports what `git status` reports, so a file moved without `git mv` is its old path, deleted (its new path is untracked, for NoUntrackedFiles), and a change of line endings only is reported when `git status` reports it. A tracked file replaced by another type, such as a symlink, which `git status` prints as `typechange`, is not reported in v1 (phase two). A file recorded with `git add -N` (intent to add) is not supported in v1 (phase two): it is reported as `modified`, and the `git restore` line offered for it would empty the file, because the index holds it with no content.
   - Fix: `git add <file>` to stage the change, or `git restore <file>` to discard it.
   - Tests: clean repo passes; edited tracked file fails; deleted tracked file fails; a file moved without `git mv` lists the old path as deleted; a file staged and then edited again fails as modified (`MM`, `AM`), and one staged and then deleted fails as deleted (`AD`); line-ending-only change with autocrlf set behaves as documented (set up with LibGit2Sharp in v1; checked against the real git CLI *(phase two)*); file mode change on Windows does not fail.
3. **NoMixedStaging** *(phase two)*: no file both staged and changed again. Narrower than NoUnstagedChanges, which reports these files too, and the only check for someone who keeps other unstaged edits on purpose. Called on its own, not part of the default `RunAll`.
   - Fix: `git add <file>` to stage the latest version, then commit.
   - Tests: staged-only file passes this check; unstaged-only file passes this check; staged then edited again fails; staged then deleted from disk fails.
4. **NoConflicts**: no conflicted entries. Detect with `FileStatus.Conflicted` / `repo.Index.Conflicts`.
   - Fix: resolve the conflict markers in the file, then `git add <file>`; then finish the operation (see NoOperationInProgress).
   - Tests: clean repo passes; merge of two branches editing the same line fails and lists the file; conflict resolved and staged passes; delete/modify conflict fails.
5. **NoOperationInProgress**: no unfinished merge, rebase, cherry-pick or revert. Detect with `repo.Info.CurrentOperation`.
   - Fix: `git <operation> --continue` once conflicts are resolved, or `git <operation> --abort`, where `<operation>` is merge, rebase, cherry-pick, or revert.
   - Tests: clean repo passes; conflicting merge left open fails and names "merge"; conflicting rebase left open fails and names "rebase"; conflicting cherry-pick left open fails and names "cherry-pick"; conflicting revert left open fails and names "revert"; aborted operation passes.
6. **NotDetachedHead**: HEAD points to a branch. Detect with `repo.Info.IsHeadDetached`.
   - Fix: `git switch <branch>` (create one with `git switch -c` if the commits must be kept).
   - Tests: on a branch passes; checkout of a commit SHA fails and the message shows the SHA; fresh repo with no commits (unborn HEAD) passes.
7. **NoStaleIndexLock**: no `.git/index.lock` left behind.
   - Fix: make sure no Git process is running, then delete `.git/index.lock`.
   - Tests: no lock passes; empty `index.lock` file fails; lock in a worktree or submodule .git path is out of scope and documented.
8. **OnExpectedBranch**: HEAD is on the configured branch (optional).
   - Fix: `git switch <expected-branch>`.
   - Tests: matching branch passes; other branch fails and shows both names; not configured means the check is skipped; detached HEAD fails with a clear message.
9. **NoTrackedIgnoredFiles**: no tracked files that match ignore rules. Detect with `repo.Ignore.IsPathIgnored` on tracked paths.
   - Fix: `git rm --cached <file>` (the file stays on disk), then commit.
   - Tests: tracked file not ignored passes; file committed before the ignore rule was added fails; file force-added with `git add -f` fails; bin/ and obj/ folder content fails and is grouped by folder; negated rule (`!keep.txt`) passes.

Cross-cutting tests: `RunAll` reports every failing check at once (two broken rules give two `Failures` and two message blocks in `Message`); a clean repo makes `RunAll` healthy; disabled checks are skipped *(phase two)*; no repo handle outlives a check (the temp folder can be deleted on Windows right after a check, with nothing disposed); a path that isn't a repository throws a clear error.

## Test tooling

- Unit tests (v1) use LibGit2Sharp to set up scenarios: in-process, fast, and no git processes to clean up. They cut libgit2 off from the machine's and the user's git config, so a scenario gives the same result on every machine.
- `ThisRepositoryTests` runs every check on this repository itself. It lives in a test project of its own, `GitAssertions.ThisRepository.Tests`, which does not cut libgit2 off from git config: libgit2 settings are process-wide, and a real repository has to be read as `git status` reads it, as a user's own test project reads theirs.
- Phase two: a small set of integration tests creates the tricky scenarios with the real git CLI, so libgit2 is checked against real Git: autocrlf / line endings, `git add -f`, files committed before an ignore rule, a merge or rebase left in progress, and a stale index.lock.
- The CLI tests run git through CliWrap, with a timeout and cancellation so no process is left running. They're tagged as integration tests and skipped when git isn't on PATH.

### Platforms

- v1 builds and runs its tests on Windows only (`windows-latest`).
- **Before the first public release (1.0), the tests also run on Linux** (`ubuntu-latest`), in CI next to the Windows run. Linux behaves differently in ways the checks depend on: the executable bit is a real file mode, symbolic links need no privileges, paths are case-sensitive, and no machine-wide `core.autocrlf` is set. A test that depends on one platform's behavior, such as the file mode test, is written for each platform or skipped on the one where it does not apply, and says so in its header.

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
- **Entries:** one per line, indented two spaces. Paths are relative to the repository root and use `/` separators. Where a check reports a kind of change, the entry is `<kind>: <path>`. Entries are sorted ordinally by their whole line, so the entries of one kind stay together (`deleted: old.txt` comes before `modified: readme.txt`), and commands list the paths in entry order.
- **Limit:** at most 20 entries. When there are more, the 21st line is `  ...and <n> more. Run the check again after fixing these.` `CheckResult.Files` still holds every offending path. Path length and total message size are capped too (see [Output limits](#output-limits)).
- **`Fix:`** one command line, indented two spaces, that fixes exactly the listed entries when run from the repository root. Paths are separated from options with `--`.
- **`Or ...:`** zero or more alternatives, each with its own command line, or a plain sentence when there is no single command (e.g. editing .gitignore).
- **Quoting:** a path goes into a command only if it is *shell-safe* (see [Shell-safe paths](#shell-safe-paths)). A shell-safe path is wrapped in double quotes when it contains anything other than ASCII letters and digits, `.`, `_`, `-`, and `/`, e.g. `"docs/my notes.md"` or `"docs/café.md"`. Inside double quotes, the allowed characters are literal in bash, PowerShell, and cmd.
- **Unsafe paths:** a path that isn't shell-safe is still listed as an entry, in escaped form, but is left out of every command. After the commands, the message adds `Not included in the commands above because the path contains characters that are unsafe in a shell; handle it by hand:` followed by each such path, escaped and indented two spaces. If no listed path is shell-safe, the command lines are omitted and only this note is printed.
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

Entry kinds: `modified`, `deleted`. There is no `renamed` kind: a file moved without `git mv` is listed as its old path, `deleted`.

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

Phase-two checks will follow the same common format when they are added.

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
- A path longer than 200 characters is shortened in entries to its first 100 and last 60 characters joined by `...`, and it is treated as unsafe, so it never appears in a command.
- A single check's `Message` is at most 16,000 characters. If a message would exceed that (which the other limits should prevent), it is cut at the last full line that fits and ends with `  ...message truncated.` A `RunAll` message is therefore at most five check messages plus its header.
- `CheckResult.Files` is not truncated; it holds the raw paths for programmatic use.

### Untrusted repositories and libgit2

- The library never executes anything a repository asks for: no hooks, no filter drivers (clean/smudge/process commands from `.gitattributes` and config), no fsmonitor, and no other external commands. LibGit2Sharp does not run hooks or external filter drivers, and the library registers no filters of its own.
- The library does not change libgit2's global settings. In particular, it leaves repository owner validation (`safe.directory`) switched on.
- Opening a hostile repository still runs native libgit2 parsing code. Keep LibGit2Sharp on its latest release and watch libgit2 security advisories.

### Information in messages

Messages usually end up in test output and CI logs, so they reveal file names, which may include sensitive ones such as `.env` or `secrets.json`. They never include file contents. This is accepted by design; callers who publish CI logs should keep that in mind.

### Test cleanup

`TempRepo` keeps its repository in a `TemporaryDirectory` (Meziantou.Framework.TemporaryDirectory), a uniquely named folder under the system temp directory that is deleted on dispose. Its delete clears read-only flags (libgit2 writes read-only files under `.git/objects`), retries a locked file, and removes a junction or symlink as a link without following it, so nothing outside the folder is touched. `TempRepo` releases the repository handle before the folder is deleted.

### Supply chain (phase two)

- Pin package versions (central package management with lock files) and keep GitHub Actions pinned to commit SHAs.
- Enable Dependabot for NuGet packages and GitHub Actions.
- CI workflows run with read-only token permissions (`permissions: contents: read`).
- When publishing to NuGet: sign the package and enable SourceLink.

## Implementation notes and catches

- Always dispose `Repository` (wrap it in `using`). Leaked handles can lock files in .git on Windows. `GitAssert` opens one per check and disposes it before returning; it never keeps one in a field, because a shared handle disposed on one thread while another reads it is a native access violation that ends the test run.
- Make sure the right native libgit2 binary is copied to the output on every build agent and platform.
- Status can differ a little from `git status` because of autocrlf / line endings, file mode, and some gitignore edge cases. Turn on `RecurseUntrackedDirs` and keep `IncludeIgnored` off when checking untracked files.
- LibGit2Sharp is maintained but releases slowly. Check its libgit2 version against security advisories when upgrading.

## Open items

- Review the real repositories for other scenarios to add.

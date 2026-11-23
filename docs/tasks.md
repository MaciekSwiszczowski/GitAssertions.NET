# GitAssertions.NET — TDD Task List

Version 1 is a two-day build done with agents: an empty GitHub repo with CI, then five checks and RunAll (Phases 0 to 4). Tasks are INVEST (Independent, Negotiable, Valuable, Estimable, Small, Testable), and each feature goes red, green, refactor: failing tests from the spec first, then the simplest code that passes, then cleanup. The later phases follow in order: integration tests with real Git, accurate messages, security, release preparation, and nice-to-have features.

A task builds the generic behavior first. Each corner case that makes it more exact, such as one more operation named in a message, is a small task of its own in a later phase, with a commit of its own.

Releases: each release closes a phase, and the phases run in order.

| Release | Closes | Contents |
|---|---|---|
| 0.1 | Phase 4 | The five checks and RunAll, with every test run in parallel. |
| 0.2 | Phase 6 | Integration tests with real Git, and accurate messages. |
| 0.3 | Phase 7 | Security. |
| 1.0 | Phase 8 | Release preparation: the first public release. |

Phase 9 holds nice-to-have features, picked up after 1.0 when they're needed. A task moved to a later phase keeps its ID.

## Definition of done

**Behavior tasks** (Phases 1 to 6, and any later task that adds or changes library or test-helper behavior):

- Tests written first and seen failing.
- All tests green locally, on Windows and on Ubuntu (WSL), and on GitHub Actions.
- No leaked Repository handles (temp folders delete cleanly).
- Failure message matches the spec's template for that check, including the copy-pasteable `Fix:` command (checks and RunAll only).

**Infrastructure tasks** (Phase 0, and tasks such as the supply chain, packaging, and branch protection) have no unit tests. Each one is done when its stated proof is shown, for example the command output, the pushed commit, or the green CI run, and nothing that was green before is now red.

## Phase 0: Repository and build

- [x] **T0.1 Create and seed the GitHub repo.** Repo `GitAssertions.NET` (this repository) with README, .gitignore for .NET, .editorconfig, a license, and docs/specification.md and docs/tasks.md. Proof: the repo exists on GitHub with these files, and the local folder C:\_github\GitAssertions has it as `origin`.
- [x] **T0.2 Solution skeleton.** Solution with `GitAssertions` (class library) and `GitAssertions.Tests` (xUnit or NUnit) projects, plus Directory.Build.props (nullable on, warnings as errors). Proof: `dotnet build` and `dotnet test` pass with one placeholder test.
- [x] **T0.3 GitHub Actions CI.** Workflow that runs restore, build, and test on push and PRs, on windows-latest. Proof: a PR shows a green check.

## Phase 1: Test infrastructure

- [x] **T1.1 TempRepo fixture.** Test helper that creates a temp folder, runs `Repository.Init`, makes an initial commit, and deletes everything on dispose. Tests first: the folder exists during the test and is gone afterward.
- [x] **T1.2 Scenario helpers.** Fluent helpers on TempRepo: AddFile, Stage, Commit, Delete, CreateBranch, Checkout, AddGitIgnore. Tests first: each helper produces the expected `RetrieveStatus` result. (Helpers for merge, rebase, cherry-pick, and revert are added with the check that needs them, T3.4 and T3.5.)

## Phase 2: Core API

- [x] **T2.1 GitAssert.Open.** Checks that a path is a repository root and keeps the path. `GitAssert` holds no `Repository` handle: every read opens the repository and disposes it before returning, so an instance has nothing to dispose and may be stored and shared. Throws a clear error for a path that isn't a repo or is a bare repo, and re-throws LibGit2Sharp exceptions as `InvalidOperationException` with the message only. Tests first: open reads the right repository, a bad path throws, and one instance shared by parallel readers works.
- [x] **T2.2 CheckResult.** `Rule`, `IsHealthy`, `Message`, `Files`, and a factory for healthy and failing results. Tests first, for the rules every check's message shares: the rule line, entries sorted ordinally, the 20-entry limit, `--` before the paths, `\n` between lines; a path with a space, a non-ASCII letter, or `+ , = @` is double-quoted in commands and a plain path is not; `Files` is sorted, read-only, and keeps every path; a healthy result is `<Rule>: OK.` with no files. What only some checks print (kinds of change, renames, commands without paths, sentence-only fix lines, a failure with no entries) is tested in Phase 3 by the checks that print it, against the spec's example messages.

## Phase 3: Version 1 checks (one task each, any order after Phase 2)

Each task: write the spec's v1 test scenarios as failing snapshot tests (skip the ones marked *(later)* in the spec). A failing scenario ends in `CheckResultSnapshot.Verify` and a healthy one in `CheckResultSnapshot.VerifyHealthy`, which shares one `<Check>.IsHealthy.verified.txt` between all of them. Each `.verified.txt` is written by hand before the check exists, a failing scenario's from the spec's template and example message (see AGENTS.md and the `snapshot-tests-csharp` skill). Then implement the check, refactor, and update the README. All scenarios are set up with LibGit2Sharp through TempRepo. Also add the check to `ThisRepositoryTests` (project `GitAssertions.ThisRepository.Tests`), which runs every check on this repository itself.

- [x] **T3.1 NoUntrackedFiles**, including nested folders and ignored files.
- [x] **T3.2 NoUnstagedChanges**, including deletes, files changed again after staging, and the autocrlf and file-mode edge cases.
- [x] **T3.4 NoConflicts**, including merge, delete/modify, add/add, and binary conflicts. Adds a merge helper to TempRepo.
- [x] **T3.5 NoOperationInProgress**, covering a conflicting merge, rebase, cherry-pick, and revert. Any other operation gets the spec's generic message, which names libgit2's `CurrentOperation` value, and is not tested; Phase 6 names the rest one task at a time (`git am` moved to T9.7). Adds rebase, cherry-pick, and revert helpers to TempRepo.
- [x] **T3.6 NoTrackedIgnoredFiles**, covering files committed before the ignore rule, force-added files, bin/obj grouping (only a folder that is itself ignored and holds no tracked file the check does not report), and negated rules.

## Phase 4: RunAll

- [x] **T4.1 RunAll.** `git.RunAll()` (no options in v1) runs the five checks and returns a `RunAllResult` as defined in the spec: `Results`, `Failures`, `IsHealthy`, and `Message`.
  - It calls the five checks one after another, in the spec's order, each exactly as if it were called on its own, so each opens and closes the repository itself. There is no shared handle: it would add a second way to run a check and a handle that is not thread-safe, to save four opens.
  - `Message` is the spec's RunAll template: a header, then each failing check's own message, unchanged.
  - If a check throws, `RunAll` throws the same exception and the checks after it do not run. Stopping is deliberate: a repository one check cannot read usually fails the others too, so going on would only repeat the error, and each check could first spend its own time failing, which on a large or broken working tree is slow.
  - Each check's scenarios are already its own tests and are not repeated through RunAll. Tests first, two snapshot tests:
    - a clean repo is healthy, with five healthy `Results` and `All Git checks passed.`; it ends in a `VerifyHealthy` for RunAll and shares `RunAll.IsHealthy.verified.txt`;
    - the spec's RunAll example, an untracked `tmp/notes.txt` and an edited `readme.txt`, gives two `Failures`, three healthy `Results` after them (a failing check does not stop the rest), and the spec's example `Message` character for character.
  - `CheckResultSnapshot` gets a renderer for `RunAllResult`, and AGENTS.md's snapshot rule gets a line saying it covers RunAll too. Each result is one line with its files below it; the result's own message is left out, because the check's tests pin it and `Message` repeats every failing one:

    ```text
    IsHealthy: False
    Results (5):
      NoUntrackedFiles: IsHealthy False, Files (1):
        tmp/notes.txt
      NoUnstagedChanges: IsHealthy False, Files (1):
        readme.txt
      NoConflicts: IsHealthy True, Files (0):
      NoOperationInProgress: IsHealthy True, Files (0):
      NoTrackedIgnoredFiles: IsHealthy True, Files (0):
    Failures (2):
      NoUntrackedFiles
      NoUnstagedChanges
    Message:
    2 of 5 Git checks failed.
    ...
    ```

  - Add a RunAll test to `ThisRepositoryTests`, beside the five check tests, sharing that project's `RunAll.IsHealthy.verified.txt`.
  - Reword the comments that promise a shared handle: the class summary of each of the five check classes ("a later RunAll on one it opens once for every check"), and the remark beside the per-read `StatusOptions` in `NoUntrackedFilesCheck` and `NoUnstagedChangesCheck`, which says "checks run in parallel" and means that a `GitAssert` may be shared by tests that run in parallel.
- [x] **T4.2 Run every test in parallel.** Both test projects switch to xUnit's parallel mode `all` (xUnit 4.0 and later), so tests in one class run at the same time too, not only classes against each other: `[assembly: Parallelization(Mode = ParallelMode.All)]` in a file of its own in each project, with the reason beside it (https://xunit.net/docs/running-tests-in-parallel). That makes the suite itself exercise a `GitAssert` shared between parallel tests, and exposes any state the tests share by accident.
  - The current-directory tests keep their `DisableParallelization` collection. In this mode such a test is "guaranteed not to run in parallel against any other test", but the docs no longer say it runs after the parallel ones, so reword the comment in `NonParallelCollectionForCurrentDirectoryTests` that says so.
  - Proof: the whole suite is green on several runs in a row, locally and on CI. A test that fails only in parallel is a finding: fix the test or the library, never opt the test out without a stated reason.

## Phase 5: Integration tests with real Git

The git CLI sets up the scenarios libgit2 cannot create, and checks what libgit2 reads against what real Git does. Only the tests run git; the library never does (spec, Goals). The tests are tagged as integration tests and run on CI with all the others. A missing or too old git fails them; it never skips them.

- [x] **T5.1 CliWrap git runner.** A test helper that runs git in a TempRepo through CliWrap, with a timeout so no process is left running, and with the CLI's version of `LibGit2Isolation`. Tests first: a git command run in a TempRepo changes what `RetrieveStatus` reports. The design follows the test suites of MinVer, gitoxide, and jj, and the git runner in WindowsFileManager (`tests/WinUiFileManager.Architecture.Tests/Git.cs`):
  - Process handling:
    - CliWrap alone, with no job object, so the runner also works on Linux (spec, Platforms);
    - CliWrap's forceful token is the test's cancellation token linked with the runner's own timeout. Cancelling it kills git's whole process tree and waits until it has exited. Never use the graceful token: on Windows it starts a helper program to send Ctrl+C;
    - stdout and stderr are read at the same time, as CliWrap does, because line-ending warnings fill the stderr pipe and would deadlock a runner that reads one stream after the other;
    - standard input stays CliWrap's default, empty and closed, so git never waits for input;
    - a tree kill misses a child whose parent has already exited. The children git is known to leave running are background maintenance, the fsmonitor daemon, and a credential-cache helper: the config values below turn off the first two, and the isolation removes the third.
  - Git on the machine:
    - a missing git, or one older than 2.32, fails the test with a message that says to install or update git. It never skips, because a skipped layer goes unnoticed. Git 2.32 is the first version that reads `GIT_CONFIG_GLOBAL`; an older one ignores it, and the tests would read the user's config;
    - CI prints `git --version` in a step before the tests, so a failure after a runner image update can be traced to a git version. The integration tests run in the existing CI job, with every other test.
  - Environment variables for every git call:
    - every inherited variable whose name starts with `GIT_` is removed first (CliWrap removes a variable set to `null`). Inside a git hook, git can set `GIT_DIR` and `GIT_INDEX_FILE`, and they would send the command to this repository's index instead of the TempRepo's;
    - `GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL` set to the null device (`NUL` on Windows, `/dev/null` elsewhere), and `GIT_ATTR_NOSYSTEM=1`: the CLI's version of `LibGit2Isolation`;
    - `XDG_CONFIG_HOME` set to an empty folder, because `GIT_CONFIG_GLOBAL` does not cover the user's global ignore and attributes files (`git/ignore` and `git/attributes` under `$XDG_CONFIG_HOME`, or under `~/.config`), and a global ignore rule changes what `git status` reports;
    - `GIT_TERMINAL_PROMPT=0`, so git never asks for credentials;
    - `GIT_EDITOR=true` and `GIT_SEQUENCE_EDITOR=true`, so `git commit` without `-m` or `git rebase -i` never waits for an editor, which on Windows can open a window on the user's desktop. No call can set its own `GIT_SEQUENCE_EDITOR`, so no test can edit a rebase todo list (T6.3 stops a rebase with a failing `--exec` instead);
    - `GIT_OPTIONAL_LOCKS=0`, because `git status` otherwise rewrites the index;
    - a fixed author and committer, with names, emails, and dates (`GIT_AUTHOR_*` and `GIT_COMMITTER_*`), so a commit is the same on every machine.
  - Config values for every git call:
    - `gc.auto=0` and `maintenance.auto=false`, so no background maintenance starts;
    - `core.fsmonitor=false`, because the fsmonitor process outlives git and keeps the temp folder locked;
    - `init.defaultBranch=main` and `commit.gpgsign=false`, so `git init` and `git commit` behave the same on every machine and git version.
- [x] **T5.5 Git's own answer in script snapshots (do it before T5.2 to T5.4).** Before this task, a script's verified file held only the RunAll result, so whether git agrees is checked by hand, once, by whoever writes the file, with the git on their machine. Phase 5 exists to check libgit2 against real Git, so after RunAll, `ScriptTests` asks git the same questions about the same folder, and the verified file holds git's answer above the RunAll result.
  - Two commands, both run through `GitCli.RunAsync` after RunAll, so a failing command fails the test:
    - `git status --porcelain --untracked-files=all` shows untracked files (`??`), unstaged changes (`M` or `D` in the second column), and conflicts (unmerged lines such as `UU`, `AA`, `DU`, `UD`). `--untracked-files=all` lists untracked files one by one, as NoUntrackedFiles does. Porcelain v1, because git guarantees that its format does not change between git versions or with user config, and it is never translated, unlike plain `git status`.
    - `git ls-files -i -c --exclude-standard` shows tracked files that match an ignore rule, which `git status` does not show (NoTrackedIgnoredFiles).
    - NoOperationInProgress has no machine-readable answer from git: the plain `git status` text changes between git versions and is translated, so it stays out.
  - Asking git changes nothing RunAll read: `GitCli` sets `GIT_OPTIONAL_LOCKS=0`, so `git status` does not rewrite the index.
  - The format: each command on a line of its own, ending in `:`, then each line git printed, exactly as printed, indented by two spaces; a command that printed nothing is its line alone. A line of `---` ends git's block, because without it a command that printed nothing reads as the first line of the result (`git ls-files -i -c --exclude-standard: IsHealthy: False`). The RunAll result follows, as now. For `ScriptTests.Merge_conflict`:

    ```text
    git status --porcelain --untracked-files=all:
      UU readme.txt
    git ls-files -i -c --exclude-standard:
    ---
    IsHealthy: False
    Results (5):
    ...
    ```
  - Tests first: write git's block into the three existing verified files by hand. `Committed_work` has both commands empty, `Merge_conflict` has `UU readme.txt`, and `Untracked_and_edited` has ` M readme.txt` and `?? tmp/notes.txt`. Then make `CheckResultSnapshot.VerifyScript` render them.
  - Reading a snapshot: git's lines and the RunAll result must tell the same story. A person compares them, not the code: the mapping is not one-to-one (a staged change, `M ` or `A ` in the first column only, fails no check), and a check test asserts nothing but its snapshot. A script whose git block disagrees with its RunAll result is a finding: report it and decide with Maciek before the library changes.
  - Git versions differ: on the development machine, Windows has git 2.56 and WSL has 2.34, and each CI runner has its own (CI prints `git --version`). Git's answer for the common cases has not changed in years; the known exceptions are line endings and ignore-rule corner cases, which T5.2 and T5.3 test. If two git versions print different blocks for one script, one verified file cannot match both. That is a finding too, decided with Maciek, for example a minimum git version for that script, or the newest git as the reference. Never scrub git's block, and never skip the script.
  - Update AGENTS.md's script-scenario rule (the verified file holds git's block and the RunAll result, both written by hand first), and the summaries of `ScriptTests` and `VerifyScript`.
  - Proof: green on Windows and on Ubuntu (WSL), two git versions apart, and on the three CI legs.
- [x] **T5.6 Script instructions for spaces, more lines, and line endings (do it before T5.2).** `echo <content> <path>` writes one word and an LF, which cannot express a CRLF file (T5.2), an attribute line such as `* text=auto`, or a second ignore rule. Extend the script's own instructions, run in-process as `echo` is: no shell and nothing to install on a build machine. Real shell syntax (`>`, `>>`, globbing) was considered and rejected: it would change the existing scripts and invite shell expectations the parser does not meet.
  - `echo "<text>" <path>`: content in double quotes may hold spaces, such as `echo "* text=auto" .gitattributes`. Unquoted content stays one word, as now. Quoted content cannot hold a double quote.
  - `append <content> <path>`: adds the content and an LF to the end of the file, creating the file and its folders if needed. Content may be quoted, as for `echo`. Example: `echo bin/* .gitignore`, then `append !bin/keep.txt .gitignore`.
  - `unix2dos <path>` and `dos2unix <path>`: rewrite the file with every line break as CRLF, or as LF, and nothing else changed; the names are the well-known tools', but the conversion runs in-process. The file is always rewritten, even when it already has those line breaks, so it gets a new write time, as a file saved unchanged does. A missing file is an error.
  - A malformed line (an unclosed quote, a missing path, an unknown instruction) throws `InvalidOperationException` naming the script and the line, as now.
  - Tests first, value tests in `GitScriptTests`: quoted content with spaces is written as one line; `append` adds a line to an existing file and creates a missing one; `unix2dos` and `dos2unix` convert every line break; each malformed line throws naming its line. The existing scripts and their verified files do not change.
  - Update the summary of `GitScript` and the instruction list in AGENTS.md's script-scenario rule.
- [x] **T5.2 Line endings against real Git (after T5.6).** Script scenarios, as in T5.3, that mirror the two NoUnstagedChanges autocrlf unit tests with real git. Git's own answer in the verified file (T5.5) shows whether `git status` agrees; a disagreement is a finding: report it and decide with Maciek before the library changes.
  - A file committed with `core.autocrlf=true` from CRLF content, then rewritten with LF, fails NoUnstagedChanges as modified, as `NoUnstagedChanges_LineEndingsChangedWithAutoCrlf_FailsAsModified` does. Measured with Git for Windows 2.56.0: `git status` lists the file as modified, because its size no longer matches the size git cached, although `git diff` prints no change.

    ```text
    git init
    git config core.autocrlf true
    echo line windows.txt
    unix2dos windows.txt
    git add windows.txt
    git commit -m "Add windows.txt"
    dos2unix windows.txt
    ```
  - The same file saved unchanged, ending in `unix2dos windows.txt` instead of `dos2unix`, is healthy, as `NoUnstagedChanges_CrlfFileSavedUnchangedWithAutoCrlf_IsHealthy` is: git reads the file again and compares it with its CRLF turned back into LF.
- [x] **T5.7 Line endings set by `.gitattributes` and `core.autocrlf=input` against real Git (after T5.2).** Many .NET repositories set line endings with `* text=auto` in `.gitattributes`, as the Visual Studio template does, instead of `core.autocrlf`; Linux and macOS users usually set `core.autocrlf=input`. Script scenarios, as in T5.2, with the T5.6 instructions. Each commits its `.gitattributes` too, so only NoUnstagedChanges can fail. Git's answers below were measured with Git for Windows 2.56.0 and with git 2.34.1 on Ubuntu (WSL), which agree; a RunAll result that disagrees with git's block is a finding: report it and decide with Maciek before the library changes.
  - `* text=auto`, a file committed from CRLF content (`echo`, `unix2dos`, `git add`, `git commit`), then rewritten with LF (`dos2unix`): `git status` lists it as modified, so NoUnstagedChanges fails. Saved unchanged instead (`unix2dos` again): healthy.
  - `*.txt eol=crlf`, the same file rewritten with LF: modified.
  - `git config core.autocrlf input`, the same file rewritten with LF: modified. Saved unchanged: healthy.
  - A file committed with CRLF before any rule, so the committed version holds CRLF; then `echo "* text=auto" .gitattributes`, committed; then the file saved unchanged: healthy. Git does not normalize a file whose committed version already holds CRLF until `git add --renormalize` is run, and libgit2 may not follow that rule, which makes this the likeliest disagreement.

    ```text
    git init
    echo line windows.txt
    unix2dos windows.txt
    git add windows.txt
    git commit -m "Add windows.txt with CRLF"
    echo "* text=auto" .gitattributes
    git add .gitattributes
    git commit -m "Normalize line endings"
    unix2dos windows.txt
    ```
- [x] **T5.3 Ignore rules against real Git.** Script scenarios (`tests/GitAssertions.Tests/Scripts`, one `ScriptTests.<Name>.script.txt` and its `.verified.txt` each), whose RunAll result agrees with git's own answer in the same verified file (T5.5). Write each verified file by hand from the spec first. A result that disagrees with git is a finding: report it and decide with Maciek before the library changes.
  - A file committed before its ignore rule was added fails NoTrackedIgnoredFiles.
  - A file force-added with `git add -f` fails NoTrackedIgnoredFiles.
  - A tracked `bin/` folder, then `bin/` ignored, fails with the folder grouped (`bin/ (<k> files)`), as the spec's template shows.
  - The fix: `git rm -r --cached` on the ignored file and folder, then a commit, ends healthy.
  - The ignore rule in `.git/info/exclude` (`echo *.log .git/info/exclude`) instead of `.gitignore` fails the same way.
  - The ignore rule in the file named by `git config core.excludesFile <file>` fails the same way. Check how a relative path in `core.excludesFile` resolves in git and in libgit2 before choosing the path the script gives.
  - Negated rules, where libgit2 is likeliest to differ from git. Each script commits the files, then writes and commits `.gitignore` (`echo`, then `append` for the second rule), so only NoTrackedIgnoredFiles can fail. Git's answers below were measured with Git for Windows 2.56.0 and with git 2.34.1 on Ubuntu (WSL), which agree:
    - `debug.log` and `keep.log` tracked, then `*.log` and `!keep.log`: `git ls-files -i -c --exclude-standard` lists `debug.log` only, so the check lists `debug.log` alone (mirrors `NoTrackedIgnoredFiles_FileBroughtBackByNegatedRule_IsHealthy`, with a second file that stays ignored).
    - `bin/keep.txt` and `bin/app.dll` tracked, then `bin/*` and `!bin/keep.txt`: git lists `bin/app.dll` only. The check lists it alone, not grouped, because `bin/` holds a tracked file it does not report (mirrors `NoTrackedIgnoredFiles_FolderContentsIgnoredButOneFileKept_ListsOnlyTheIgnoredFile`).
    - The same files, then `bin/` and `!bin/keep.txt`: git lists both, because a file cannot be brought back when its folder is ignored (gitignore documentation). The check lists the folder grouped, `bin/ (2 files)`. This is the classic trap, and the likeliest place for libgit2 to bring the file back where git does not.
- [x] **T5.4 Operations left in progress by real Git.** Script scenarios, as in T5.3, of operations an agent stops halfway, and of their ends. The conflicting `git merge` is there already (`ScriptTests.Merge_conflict`).
  - A conflicting `git rebase` fails NoConflicts and NoOperationInProgress, with the rebase Fix lines.
  - A conflicting `git rebase --apply` fails the same way. Each kind of rebase leaves a different state, and the check maps all three to `rebase` (measured with Git for Windows 2.56.0): a plain `git rebase` leaves `.git/rebase-merge/` with an `interactive` file, which libgit2 reports as `RebaseInteractive`; `--apply` leaves `.git/rebase-apply/`, reported as `Rebase`; LibGit2Sharp's own rebase, in the unit tests, gives `RebaseMerge`. With this scenario, every rebase state the check maps is tested.
  - The same rebase resolved (`echo`, `git add`, `git rebase --continue`) ends healthy: the "broken, then fixed" story.
  - A conflicting merge resolved and staged but not committed passes NoConflicts and fails NoOperationInProgress.
  - A conflicting cherry-pick of one commit, and a conflicting revert of one commit, fail with the cherry-pick and revert Fix lines. Several commits are T6.1.
  - A delete/modify conflict, `git rm` on one branch and an edit on the other, lists the file in NoConflicts.
  - A conflicting `git stash pop` leaves the file conflicted with no operation in progress: NoConflicts fails, NoOperationInProgress passes. libgit2 cannot set this up the way git does.
  - The aborted merge stays in T6.4.

## Phase 6: Accurate messages

Small tasks, one commit each, so that no message misleads: each one names a state the checks report generically today, or fixes advice that does not fit. A scenario libgit2 cannot set up is tested with real Git only (Phase 5), never by writing Git's state files by hand.

- [x] **T6.1 Multi-commit cherry-pick and revert.** libgit2 reports `CherryPickSequence` and `RevertSequence`, which T3.5 prints with the generic message. Map them to `cherry-pick` and `revert`, with the same Fix lines. Tests with real Git only, because libgit2 has no sequencer: `git cherry-pick A B` and `git revert A B`, each with a conflict in the first commit.
- [x] **T6.2 Bisect.** libgit2 reports `Bisect`, which T3.5 prints with the generic message. Name it, with the fix `git bisect reset`; write its message template in the spec first. Test with real Git only, because libgit2 has no bisect: `git bisect start`, `git bisect bad`, `git bisect good <commit>`.
- [x] **T6.3 Operations stopped without a conflict.** An operation can stop with no conflict: `git merge --no-commit` stops before the merge commit and reports `Merge`; an interactive rebase stops at an `edit` step, or after an `--exec` command fails, and reports `RebaseInteractive`. T3.5's Fix line, "once all conflicts are resolved and staged", then tells the reader to resolve conflicts that do not exist. The commands after it stay right.
  - Decided: one wording for every operation, "all" becomes "any": `Fix: once any conflicts are resolved and staged, run:`. The check does not tell the two cases apart, because the state cannot: a merge reports `Merge` with or without a conflict, and a rebase stopped on a conflict reports `RebaseInteractive`, the same as one stopped without. Nothing is lost when there are conflicts: NoConflicts' own block in the `RunAll` message already says to resolve them and run `git add`.
  - Change the template and the example in the spec (NoOperationInProgress) first, then the code, then every verified file that holds the old line: T3.5's, and the `Merge_conflict` script's.
  - Test `merge --no-commit` in-process, with `MergeOptions.CommitOnSuccess = false`. The branches must have diverged: a fast-forward makes no merge commit, so nothing is left in progress.
  - Test the rebase stop with a script, because libgit2 has no interactive rebase. Use a failing `--exec`, not an `edit` step: an `edit` step needs an edited todo list, and the script runner sets `GIT_SEQUENCE_EDITOR=true`, which overrides any `-c sequence.editor` a script line could pass. Measured on git 2.56.0 and 2.34.1: both stop with exit code 1 (the script runner logs it and goes on), leave `.git/rebase-merge/interactive`, which libgit2 reports as `RebaseInteractive`, and both git commands print nothing. The rebase stops even when the branch already sits on `main`'s tip.

    ```text
    # A rebase whose --exec command fails stops after the commit, with no conflict, and leaves the rebase open.
    git init
    echo one a.txt
    git add a.txt
    git commit -m "Add a.txt"
    git switch -c feature
    echo two b.txt
    git add b.txt
    git commit -m "Add b.txt"
    git rebase -x "exit 1" main
    ```

    The verified file, written by hand first:

    ```text
    git status --porcelain --untracked-files=all:
    git ls-files -i -c --exclude-standard:
    ---
    IsHealthy: False
    Results (5):
      NoUntrackedFiles: IsHealthy True, Files (0):
      NoUnstagedChanges: IsHealthy True, Files (0):
      NoConflicts: IsHealthy True, Files (0):
      NoOperationInProgress: IsHealthy False, Files (0):
      NoTrackedIgnoredFiles: IsHealthy True, Files (0):
    Failures (1):
      NoOperationInProgress
    Message:
    1 of 5 Git checks failed.

    NoOperationInProgress: a rebase is in progress.
    Fix: once any conflicts are resolved and staged, run:
      git rebase --continue
    Or abort it:
      git rebase --abort
    ```
- [x] **T6.4 Aborted operation passes.** Tests first: a conflicting rebase aborted with LibGit2Sharp's `Rebase.Abort()` passes NoOperationInProgress, and so does a conflicting merge aborted with `git merge --abort` (real Git, because LibGit2Sharp has no merge abort).
- [x] **T6.5 Unborn HEAD.** A repository with no commits passes every check and RunAll. NoConflicts has this scenario already; add it, on `TempRepo.CreateUnborn`, to NoUntrackedFiles, NoUnstagedChanges, NoOperationInProgress, NoTrackedIgnoredFiles, and RunAll.
- [x] **T6.6 Intent-to-add.** A file recorded with `git add -N` is reported as `modified` and offered `git restore`, which empties it, because the index holds it with no content. Measured on git 2.56.0: `git status --porcelain` prints ` A notes.txt`, and `git status` lists it as `new file` under "Changes not staged for commit"; `git commit` refuses with "no changes added to commit"; `git add -- notes.txt` stages it (`A `); `git restore -- notes.txt` exits 0 and leaves the file empty, still ` A`, so the check keeps failing after the advice destroyed the work.
  - Decided: a kind of its own, `new (intent to add)`, offered `git add` and no discard line. It matches git's ` A` and "new file", so a script's git block and its `RunAll` result tell the same story.
  - Fix lines take only some entries' paths: `git add` lists every entry, `git restore` lists every entry but the intent-to-add ones, and the discard line is left out when no path is left for it. Write the template and an example in the spec (NoUnstagedChanges) first:

    ```text
    NoUnstagedChanges: 2 files have unstaged changes.
      modified: readme.txt
      new (intent to add): notes.txt
    Fix:
      git add -- readme.txt notes.txt
    Or discard the changes:
      git restore -- readme.txt
    ```

    ```text
    NoUnstagedChanges: 1 file has unstaged changes.
      new (intent to add): notes.txt
    Fix:
      git add -- notes.txt
    ```

  - Remove the limitation where it is stated: the README's list of what `NoUnstagedChanges` does not support yet, the spec's sentence that intent-to-add is not supported in v1, and its entry in the spec's list of later features. Add the kind to the spec's entry kinds of NoUnstagedChanges.
  - LibGit2Sharp does not expose the intent-to-add flag, so the file is recognized by its state: not in HEAD, the empty blob in the index, and changed in the working tree. In-process tests stage the empty blob, which libgit2 reads as it reads a real `git add -N`.
  - The same state comes from an empty file staged with a plain `git add` and written afterwards. Measured: git prints `AM notes.txt`, and `git restore` empties that file too. It gets the same kind and the same advice: dropping the discard line protects it as well, and the name is the one inaccuracy, accepted.
  - Known gap, accepted: an empty file recorded with `git add -N` matches the empty blob in the index, so nothing marks it as changed. Git prints ` A empty.txt` (measured); the library cannot tell it from an empty file staged with `git add`, which is healthy, and passes it. No content can be lost. Pin it in a script whose `#` comment names the gap, so the disagreement in its verified file is a recorded decision, not a finding.
  - Tests with real Git, as scripts: one file recorded with `git add -N` (no discard line); the same file next to a tracked file that was edited (the discard line lists only the edited file); and the empty-file gap above.

## Phase 7: Security

This phase will grow: more security findings become tasks here as they are decided.

- [x] **T2.3 Shell-safe paths and output limits.** Message formatting follows the spec's Security > Shell-safe paths and Output limits. Tests first:
  - A path with `$(...)`, a backtick, `;`, `&`, `|`, `%`, `"`, `'`, `*`, or a leading `:` is listed as an entry but left out of every command, and appears in the unsafe-path note.
  - Newline, tab, carriage return, ESC, and a bidirectional override in a path are printed escaped (`\n`, `\t`, `\r`, `\u{001B}`, `\u{202E}`), so each entry stays on one line; a literal backslash prints as `\\`.
  - When no listed path is safe, the message has no command lines, only the note.
  - A 300-character path is shortened in entries (first 100 + `...` + last 60) and left out of commands.
  - A message that would exceed 16,000 characters is cut at a full line and ends with `  ...message truncated.`
  - `Files` still holds the raw, untruncated paths.
- [ ] **T2.4 Hostile-repo safety.** Prove the library never runs anything the repository asks for (spec, Security > Untrusted repositories and libgit2). No library change is expected: libgit2 runs no hooks, no external filter drivers, and no fsmonitor, and the library registers no filters and never calls `GlobalSettings`. If a test below finds that something ran, stop and report it: the library does not change in this task. Tests first, in a new `HostileRepositoryTests`.
  - **Traps.** A test helper arms a `TempRepo` with six traps. Each one, when it runs, creates a marker file named after itself (`pre-commit`, `post-checkout`, `clean`, `smudge`, `process`, `fsmonitor`) in one marker folder:
    - hooks: `.git/hooks/pre-commit` and `.git/hooks/post-checkout`, `#!/bin/sh` scripts written with `Lf` line breaks, made executable with `File.SetUnixFileMode` where the platform has the bit (`!OperatingSystem.IsWindows()`);
    - a clean/smudge filter driver: `.gitattributes` gives `trapped.txt` `filter=trap`, and `.git/config` sets `filter.trap.clean` and `filter.trap.smudge` to a command that creates its marker and passes the content through, such as `touch '<marker>'; cat`;
    - a process filter driver: a second driver, `filter.trapprocess.process`, on a second file (`processed.txt filter=trapprocess`). Git runs a driver's `process` command instead of its `clean` and `smudge` when it has one, so one driver cannot test both;
    - fsmonitor: `core.fsmonitor` set to the path of a hook script. `git status` itself runs it, so it is the one trap a status read sets off. The first text of this task named only hooks and filters; fsmonitor is added because the spec and the README statement name it.
  - The marker folder is outside the working tree, under `.git`, so a marker can never show up as an untracked file, and it goes with the `TempRepo`. Every script names its marker by absolute path, with forward slashes and in single quotes, so a trap that ran from another working directory would still write where the test looks, and `sh` on Windows reads `C:/...`.
  - Order: commit `trapped.txt`, `processed.txt`, and `.gitattributes` first, then write the hooks and the config, then edit both files, so their stat no longer matches the index and a reader like `git status` must read them through the filters.
  - **Control tests first**, value tests with Shouldly: a theory over the six traps. Each case arms the whole repository, runs the git command that sets its trap off, and asserts that its marker exists. Without them, a trap built wrong (a CR after the shebang, no executable bit, a path the shell misreads) never fires, and the safety test passes for nothing. Commands, to be measured: `git status` for `clean`, `process`, and `fsmonitor`; `git commit --allow-empty -m <message>` for `pre-commit`; `git checkout -- trapped.txt` for `smudge` and `post-checkout`.
    - `GitCli` puts `-c core.fsmonitor=false` in front of every command, and a command-line `-c` beats the repository's config. The fsmonitor case passes its own `-c core.fsmonitor=<hook>` after it, which wins (measure). A hook path starts no daemon, so the reason `GitCli` turns fsmonitor off does not apply.
    - The process trap exits without the filter protocol's handshake, so git may report an error. That case runs through `GitCli.RunLineAsync`, which does not throw, and checks only the marker.
  - **Safety test**, a value test: arm the whole repository, then `GitAssert.Open(root)` and `RunAll()`, which runs every check as if each were called on its own. The marker folder must be empty (`Directory.GetFiles(markers).ShouldBeEmpty()`), so a failure names the traps that fired. It asserts nothing about the result, not even with a snapshot: the check tests pin results, and a snapshot here would suggest the test is about them and repeat those tests (decided 2026-11-23). An exception from `Open` or `RunAll` still fails it.
  - **Owner validation**, a value test: after `Open` and `RunAll`, `GlobalSettings.GetOwnerValidation()` is still true. The setting is process-wide, so no test may change it. A repository owned by another user, which would show the validation at work, needs root or admin to set up and is not tested; the test's header says so.
  - **AGENTS.md**: add the exception to the snapshot rule, beside "Tests of `Open`, of exceptions, and of `CheckResult` itself stay value tests with Shouldly": a test that runs checks only to drive the library through a scenario, and proves something else, such as that no hook or filter ran, asserts only that, never the check's result.
  - **README**: a Security section that says, in short, what the spec says under Security > Untrusted repositories and libgit2: the library never runs hooks, filter drivers (clean, smudge, process), fsmonitor, or any other command a repository asks for, and leaves libgit2's repository owner validation (`safe.directory`) switched on; libgit2's settings belong to the whole process, so a filter the caller's own code registered runs during the checks, and owner validation it switched off stays off; the checks are meant for repositories cloned or created on this machine, because a repository folder copied in whole brings its own `.git`, whose config can name files on a network share; and opening a hostile repository still runs libgit2's native parsing code.
- [x] **T7.1 Supply chain: SHA-pinned actions.** Pin every action in the workflows to a full commit SHA, with its exact release in a comment: `uses: actions/checkout@<40 hex digits> # v7.x.y`. A tag such as `v7` is a pointer that whoever controls the action's repository can move, and every run then executes the commit it points to, with no change in this repository; a commit SHA names one exact content and cannot be moved. In March 2025 the tags of `tj-actions/changed-files` were moved to a malicious commit (CVE-2025-30066). Today `ci.yml` uses `actions/checkout@v7` and `actions/setup-dotnet@v6`. Proof: a green CI run with every `uses:` pinned.
  - Take each SHA from the action's own repository and its exact release tag, not the major tag: `gh api repos/actions/checkout/commits/v7.x.y --jq .sha`. The commits endpoint resolves an annotated tag to its commit, whose SHA is the one to pin; the tag object's own SHA is not a commit. Never copy a SHA from elsewhere: GitHub also serves a commit from any fork of the repository under the original's name, so a SHA from a fork would run the fork's code.
  - The version comment is what Dependabot (T7.2) reads to move the SHA and the comment together when a release comes out.
  - A pin covers only the action's own code. What the action downloads as it runs, such as the .NET SDK that `setup-dotnet` installs, is not pinned by it.
  - No NuGet lock files; the spec (Security > Supply chain) says why.
- [x] **T7.2 Dependabot** for NuGet and GitHub Actions: `.github/dependabot.yml` with the `nuget` and `github-actions` ecosystems, both at `/`, on a weekly schedule, with the analyzer packages in one group. Proof: the file exists, Dependabot's first update check runs (Insights > Dependency graph > Dependabot), and its first NuGet PR edits `Directory.Packages.props`, where the versions are. Dependabot alerts and security updates are switched on in the repository's settings, not in the file, by the repository's owner.
  - Ignore `Verify.XunitV3`: it is pinned to `[32.0.0]`, its last free version (`Directory.Packages.props`), and Dependabot would propose the next one every week.
  - With SHA-pinned actions (T7.1), Dependabot moves each SHA and its version comment together.
  - Dependabot is the whole policy for libgit2 too (spec, Security > Supply chain).
- [ ] **T7.3 `Open` refuses a working tree that is not the opened folder.** `core.worktree` in `.git/config` can point the repository at another folder, such as `C:/Users/me/Documents`. `Open` succeeds today, NoUntrackedFiles lists that folder's files, and its `git clean -f` line, run in the repository, deletes them (measured with `git clean -n`). Anyone who can write `.git/config` can set it: an unpacked archive, a shared folder, a tool or agent that ran `git config core.worktree`. Owner validation does not help, because every file belongs to the current user. Decided (2026-11-23): `Open` throws.
  - `Open` compares the working tree libgit2 reports (`repository.Info.WorkingDirectory`) with the folder it was given, and throws an `ArgumentException` for `repoPath` when they differ. Write the message in the spec (Public API, `Open`) first. It names both folders, the working tree escaped like a displayed path because it comes from the repository's config, and says what to do: remove `core.worktree` from the repository's config, or pass the folder of another clone.
  - Measured on 2026-11-23, LibGit2Sharp 0.32.0, on Windows and on Ubuntu in WSL: `Info.WorkingDirectory` ends with a separator. On Windows it is the given folder with every 8.3 short name expanded (`GA-MEA~1` becomes `ga-measure-...`) and the letter case of every part but the drive letter taken from the disk; a junction is kept as given. On Linux every symlink in the path is resolved, the folder's own and a parent's. A relative `core.worktree` is resolved against `.git`, and a `core.worktree` that names the folder itself, also in its 8.3 form, reports the folder.
  - So the given folder is resolved the same way before the comparison, and both lose their trailing separator: on Windows, its short names are expanded with `GetLongPathName`, through CsWin32 like every native call in this repository, and the two are compared ignoring case; on Linux and macOS, every symlink in its path is resolved, and the two are compared exactly on Linux and ignoring case on macOS, whose default file system ignores case.
  - A linked worktree still opens, because its working tree is the folder it was opened from (measured), and so does a submodule's folder, whose own config sets `core.worktree` to that folder. The spec's non-goal "Hooks, sparse checkout, worktrees and other advanced Git features" contradicts this: say there that opening a linked worktree is supported.
  - Tests first, value tests with Shouldly: `core.worktree` set to another folder, as an absolute and as a relative path, throws, naming both folders; `core.worktree` naming the folder itself opens; a linked worktree made with `git worktree add` opens; a submodule's folder opens (`git submodule add` from a local repository, with `-c protocol.file.allow=always`); on Windows, the folder's 8.3 form, a lower-case spelling, and a junction to it each open (skipped elsewhere, and the 8.3 test skipped, with the reason, on a volume that makes no short names); on Linux and macOS, a symlink to the folder and a path through a symlinked parent each open (skipped on Windows, where a symlink needs Developer Mode).
- [ ] **T7.4 Escaped display stops CI log commands.** CI systems read every line a build prints for commands of their own. GitHub Actions takes a line that starts with `::`, after leading spaces, as a workflow command (`::warning::text` shows a warning; `::stop-commands::<token>` stops reading the rest of the log for commands). Azure Pipelines takes `##vso[` anywhere in a line (`##vso[task.setvariable variable=SKIP_TESTS]true` sets a variable for every later step), and TeamCity reads `##teamcity[`. A path prints on a line of its own in the entries of NoUntrackedFiles and NoTrackedIgnoredFiles and in the unsafe-path note, and T2.3's escaping leaves `:` and `#` alone, so a file named `::warning::All tests passed` prints a fake warning. libgit2 accepts both names in the index (measured); a name with `:` cannot exist on a Windows disk, so there it shows as deleted.
  - Wherever a path is displayed (entries, the unsafe-path note, and every later place that prints a path, such as an exception message), print a leading `:` as `\u{003A}` and a `#` that follows another `#` as `\u{0023}`: `::warning::x` prints as `\u{003A}:warning::x`, and `##vso[` as `#\u{0023}vso[`. Commands need nothing new: a path that starts with `:` or holds `#` is never shell-safe, so it never goes into one. Write it in the spec (Security > Shell-safe paths, Escaped display) first.
  - Tests first: in `FailureMessageSafetyTests`, rows for `::warning::All tests passed` and `##vso[task.setvariable variable=SKIP_TESTS]true`, each printed escaped in its entry and in the note and left out of the commands; and a snapshot test through NoUntrackedFiles with the `##vso[` name, which Windows can hold.
  - Escape U+2028 LINE SEPARATOR and U+2029 PARAGRAPH SEPARATOR too, the only characters of the Unicode categories Zl and Zp. They are neither control nor format characters, so they print raw today, and many editors, log viewers, and JavaScript tools break a line on them, so an entry could show as two lines. The spec's rule then reads "control, format, line separator, and paragraph separator characters (Cc, Cf, Zl, Zp)". Add both to the escape rows of `FailureMessageSafetyTests`.
- [ ] **T7.5 Combining marks are shell-safe.** Many scripts build a letter from a base letter and combining marks (Unicode categories Mn and Mc): `हिंदी.txt` holds U+093F and U+0940 (Mc) and U+0902 (Mn), and a name typed on macOS can be stored decomposed, `cafe` followed by U+0301 (Mn). The spec's shell-safe characters are letters, digits, a space, and `. _ - / + , = @`, so such a file is listed but left out of every command today, and must be handled by hand. Allow Mn and Mc as well: no shell gives a mark a meaning, because bash, PowerShell, and cmd read only ASCII characters as syntax; a mark cannot turn the character before it into syntax (`;` followed by U+0338 is still unsafe because of the `;`); and a path with one is double-quoted like any non-ASCII path. Enclosing marks (Me) and the number categories other than digits (Nl, No, such as `²`) stay unsafe: no script needs them. Write the rule in the spec (Security > Shell-safe paths) first. Tests first: `हिंदी.txt` and the decomposed `café.txt` go into both commands of NoUntrackedFiles, double-quoted (snapshot tests), and a row in `FailureMessageSafetyTests` for `;` followed by U+0338, which stays out of every command.
- [ ] **T7.6 Paths in exception messages are escaped, and `Files` says it is untrusted.** `GitAssert` re-throws a libgit2 error as an `InvalidOperationException` with its message unchanged, and libgit2's message can hold a path: a long untracked path throws `path too long: 'C:/repo/<the whole path>'` (measured). A hostile name then reaches the log through the exception and skips every rule of Escaped display. Apply the same escaping to the re-thrown message, after T7.4, so `##vso[` is caught there too.
  - Tests first, value tests with Shouldly: a read that throws for a long untracked path whose name holds `##vso[` throws a message with `#\u{0023}vso[` in it. Measure on each platform where libgit2 throws for a long path; a test that depends on one platform's behavior is written for each, or skipped on the one where it does not apply, with the reason.
  - `CheckResult.Files` keeps the raw paths, by design. Its XML doc, the spec (Security > Output limits), and the README's paragraph on file names say that they come from the repository and may be hostile: escape a path before printing it, and never pass one to a shell.
- [ ] **T7.7 A destructive alternative is said in words, never given as a command.** `git clean -f` deletes untracked files, and `git restore` overwrites edits; neither can be undone, because Git never stored that content. Today both are ready-to-paste command lines, and an agent told to make the tests green may run whichever line passes fastest. Decided (2026-11-23): drop both command lines. The message says what can be done, warns that it cannot be undone, and says that an agent may need a person to confirm it. It names no command, so nothing can be pasted, and an agent is not pointed at a broader one such as `git clean -fd`. Write the templates and the examples in the spec first (Common format, NoUntrackedFiles, NoUnstagedChanges, and the Shell-safe paths example):

    ```text
    NoUntrackedFiles: 2 untracked files found.
      docs/my notes.md
      tmp/notes.txt
    Fix:
      git add -- "docs/my notes.md" tmp/notes.txt
    Or ignore them: add the paths to .gitignore.
    Or delete them, if they are not needed. Deleting cannot be undone, so an agent may need a person to confirm it.
    ```

    ```text
    NoUnstagedChanges: 2 files have unstaged changes.
      deleted: old.txt
      modified: readme.txt
    Fix:
      git add -- old.txt readme.txt
    Or discard the changes, if they are not needed. Discarding cannot be undone, so an agent may need a person to confirm it.
    ```

  - Common format: an `Or ...` alternative that cannot be undone is always a sentence, last in the message's fixes.
  - NoUnstagedChanges keeps today's condition for the discard sentence: it is left out when every entry is `new (intent to add)`, because discarding such a file with `git restore` empties it.
  - Every verified file that holds these messages changes, the scripts' too: read each diff before accepting it.
- [ ] **T7.8 A file that looks like a secret is not offered to `git add`.** For an untracked `.env`, the first Fix line today is `git add -- .env`, which commits a secrets file. Decided (2026-11-23): NoUntrackedFiles leaves such a file out of `git add` and prints a line for it, after the entries and before `Fix:`. Write the template and the example in the spec first (Common format, NoUntrackedFiles):

    ```text
    NoUntrackedFiles: 2 untracked files found.
      .env
      tmp/notes.txt
    Ignore .env: it looks like a secret, so add it to .gitignore and do not commit it.
    Fix:
      git add -- tmp/notes.txt
    Or ignore them: add the paths to .gitignore.
    Or delete them, if they are not needed. Deleting cannot be undone, so an agent may need a person to confirm it.
    ```

  - One line per such file, with its whole path, escaped like any displayed path. The file is not in the unsafe-path note, because the note names only a path a command would have taken.
  - It is a heuristic, and the spec says so: it goes by the file name, the last part of the path, not by the content. Only `.env` is recognized, ignoring case, because Windows and macOS open `.ENV` as the same file; any other file is offered to `git add` as today.
  - Tests first, snapshot tests: `.env` next to a plain file (the example above); `.env` alone (no `Fix:` block, as when no path is left for a command); `config/.env`, whose line names the whole path; `.env.example`, which is offered to `git add` like any file.
- [ ] **T7.9 The abort line is said in words too (after T7.7).** NoOperationInProgress offers `Or abort it:` with `git <operation> --abort`, which makes the check pass at once, so it is the fastest line for an agent told to make the tests green. `git merge --abort` throws away the conflict resolutions made so far, and git's documentation warns that it cannot always rebuild uncommitted changes from before the merge; `git rebase --abort`, `git cherry-pick --abort`, and `git revert --abort` put the branch back, and the work done meanwhile can be found again only through the reflog. So it is a destructive alternative, and T7.7's rule applies: the command line becomes `Or abort it, if it is not wanted. Aborting throws away the conflict resolutions made so far, so an agent may need a person to confirm it.` The `--continue` fix keeps its command, and bisect keeps `git bisect reset`, which loses nothing. Write the template and the example in the spec first (NoOperationInProgress); every verified file of an operation in progress changes, the scripts' too.
- [x] **CI read-only token:** set `permissions: contents: read` in every workflow. Proof: a green CI run with the restricted permissions. (Already in the CI workflow from T0.3; keep it in any new workflow.)

## Phase 8: Release preparation

The first public release. More findings for it become tasks here as they are decided.

- [ ] **T2.5 Packfiles.** Research first: how Git stores objects in packfiles (`.git/objects/pack`), which every cloned repository uses, and how libgit2 reads them. Background, measured on 2026-11-18: libgit2 keeps a packfile open once it has read from it, and Windows then refuses to delete it; loose objects are never held open, so a fresh TempRepo cannot show a leaked handle, and no test today proves that a read releases its handle (a `Repository` kept in a field passes them all). Then, tests first: a TempRepo helper that packs the repository's objects, and a test that on a packed repository the folder can be deleted right after a `GitAssert` read, with nothing disposed. A `Repository` kept in a field must turn that test red.
  - libgit2 shares open packfiles across the whole process by path, so the `TempRepo`'s own `Repo` handle can keep a pack open: release it before asserting that the folder can be deleted.
- [x] **T8.1 Linux and macOS CI legs.** Run restore, build, and test on `ubuntu-latest` and `macos-latest` as well as `windows-latest`, as a job matrix with `fail-fast: false` (see the spec's Test tooling > Platforms). Fix or skip, with a stated reason, each test that depends on the behavior of one platform. Proof: a green CI run on all three.
  - The matrix is in `ci.yml`. All three legs are green on CI (2026-11-22).
- [ ] **T8.2 NuGet package** with SourceLink, which the .NET SDK includes for GitHub repositories, and no author signature (spec, Security > Supply chain). CI runs `dotnet pack` on every run, so the package builds before anything publishes it. The README gets a usage sample, and says which LibGit2Sharp versions the package supports: a consumer that uses LibGit2Sharp too gets one shared version, and the native library's file name is fixed for each version. Proof: CI produces the package on every run, and NuGet Package Explorer's health check shows SourceLink and a deterministic build.
- [ ] **T8.3 Branch protection** on main, requiring CI: the three jobs `build-and-test (windows-latest)`, `build-and-test (ubuntu-latest)`, and `build-and-test (macos-latest)` (T8.1). A required check is matched by job name, so renaming a job or a platform leaves the old name required and blocks every pull request.
- [ ] **T8.5 README: running the checks in your own test suite.** A section for consumers:
  - Put the Git checks in an xUnit collection with `DisableParallelization = true`, which runs after the parallel tests: other tests write files while they run (snapshots, coverage, logs, test databases), so a NoUntrackedFiles test beside them fails now and then. This repository needs none: its tests write only `.received.txt` files, which are ignored.
  - Results follow the machine's Git config, as `git status` does (`core.autocrlf`, global ignore rules and `core.excludesFile`, `core.longpaths`, `safe.directory`), so the same repository can get a different verdict on another machine. When `Open` throws because the repository is not owned by the current user, as in a container whose checkout belongs to another user, the fix is `git config --global --add safe.directory <path>`.
  - Some changes are hidden from Git and from the checks alike: an edit to a file marked `assume-unchanged` or `skip-worktree`, and on Windows a rename that changes only letter case, made outside Git (`git mv Readme.md README.md` does it right).
  - A test's `Timeout` cannot stop a libgit2 call that hangs: native code is not interrupted, so the call keeps its thread, and the test host, alive.
- [ ] **T8.6 The checks write nothing (after T2.4).** Measured on 2026-11-19: libgit2's reads left `.git` unchanged, while a plain `git status` rewrote the index. Pin the read-only promise with a value test: on a repository with an untracked file, an unstaged edit, a staged change, and an ignored file, hash every file under `.git` (its path and content) before and after `Open`, every check, and `RunAll`, and the two sets must be equal. It asserts nothing about the results, under the AGENTS.md exception T2.4 adds.
- [ ] **T8.7 `Open` on a path with non-ASCII letters and a space.** Such paths are common, such as `C:\Users\Michał Test\repo`, and they go through libgit2's conversion of paths to UTF-8. Test first: a repository in a folder named `Michał Test` opens, and RunAll on it is healthy (`VerifyHealthy`). `TempRepo` gets a way to create the repository under a named folder.
- [ ] **T8.8 `Open` names `repoPath` for a path it cannot read.** For a path such as one with a NUL character, `Path.GetFullPath` throws an `ArgumentException` whose parameter name is `path`, not `repoPath`. Catch it and throw an `ArgumentException` for `repoPath` whose message says the path is not valid. Test first, a value test: `Open("a\0b")` throws with `ParamName` `repoPath`.
- [ ] **T8.9 Ignore `.claude/worktrees/` in this repository.** A linked worktree inside the working tree, such as one Claude Code makes under `.claude/worktrees/`, is one untracked entry (`?? .claude/worktrees/wt1/`, measured), so `ThisRepositoryTests` fails while it exists, and neither Fix line suits it. Add `.claude/worktrees/` to `.gitignore`. Proof: with a worktree there, `git status` is clean and `ThisRepositoryTests` passes.
- [ ] **T8.10 Exception messages say where and what to do (after T7.6).** A libgit2 error is re-thrown with libgit2's text alone today, and a test pins that (spec, Public API). Add the repository path, the check's name, and a next step where one is known, with libgit2's text at the end, so the error tells the caller what to do. Write the format in the spec first; the test that pins today's text changes with it.
  - The known case: a long untracked path on Windows, over 260 characters without `core.longpaths` (deep `node_modules`, test output), throws `path too long: '<path>'` (measured for an untracked path). Its next step is `git config core.longpaths true`: measure first that libgit2 then reads the path, and if it does not, the message names the limit instead. The check throws rather than skipping the path, as `git status` does, because skipping would hide the untracked file the check exists to find.
  - Tests first, value tests: a check on a repository whose folder was deleted names the path and the check; the long path names its next step (Windows only, skipped elsewhere with the reason).
- [ ] **T8.11 A native library that fails to load gives a clear error.** On an unsupported platform, or after a trimmed publish that left out the native binary, the first libgit2 call fails with `TypeInitializationException` or `DllNotFoundException`. Neither is a LibGit2Sharp exception, so `GitAssert`'s wrapper lets it through, and its message names the internal type `LibGit2Sharp.Core.NativeMethods`. Re-throw both as the `InvalidOperationException` every failed read gives, with a message that names the platform (`RuntimeInformation.RuntimeIdentifier`) and says the native libgit2 library could not be loaded. Write it in the spec (Public API) first. Test first, a value test: the wrapper, given a call that throws each of the two, throws that message.
- [ ] **T8.12 An index libgit2 cannot read gives a clear error (after T8.10).** libgit2 refuses an index with an extension it does not know, so every check throws `unsupported mandatory extension: 'sdir'` with a sparse index (`git sparse-checkout set --sparse-index <folder>`) and `'link'` with a split index (`git config core.splitIndex true`) (measured with libgit2 1.8.6). Map both to a message in T8.10's format that names the feature and the git command that turns it off; measure the commands, which must leave an index the checks can read. Tests first, value tests with real Git (`TempRepo.GitAsync`): a sparse index and a split index each make every check throw that message, and after the command it names, the checks read the repository.

## Phase 9: Nice-to-have (after 1.0, when needed)

- [ ] **T9.1 NoUnstagedChanges `typechange` kind.** A tracked file replaced by another type, such as a symlink, which `git status` prints as `typechange`. libgit2 reports it as `TypeChangeInWorkdir`, which the check does not map today, so the check passes. Report it with a `typechange` kind and the same Fix lines as `modified`. The test skips when `File.CreateSymbolicLink` is refused, as it is on Windows without Developer Mode.
- [ ] **T9.2 NoMixedStaging check:** a narrower check than NoUnstagedChanges, its own method, not in the default `RunAll`, so a file staged and then changed again does not print in two blocks. Its scenarios (staged then edited or deleted) are already NoUnstagedChanges tests.
- [ ] **T9.3 NotDetachedHead check**, including unborn HEAD.
- [ ] **T9.4 OnExpectedBranch check.**
- [ ] **T9.5 NoStaleIndexLock check**, with a real-Git test of a stale `index.lock`.
- [ ] **T9.6 Options:** `RunAll(options)` with per-check on/off, allow-lists, expected branch. Includes the spec's deferred scenarios: allow-listed path passes, disabled checks are skipped.
- [ ] **T9.7 `git am` in NoOperationInProgress.** libgit2 reports `ApplyMailbox` for a `git am` stopped halfway, and `ApplyMailboxOrRebase` when it cannot tell `git am` from the old rebase backend; T3.5 prints both with the generic message. Name `git am` for `ApplyMailbox`, with `git am --continue` and `git am --abort` said as T7.9 says an abort. `ApplyMailboxOrRebase` keeps the generic message, because the library cannot tell which operation it is. Tests with real Git.
- [ ] **T9.8 Review real repos** for new scenarios and add them as new tasks.
- [ ] **T9.10 Index version 4 and the untracked cache against real Git.** A script scenario, as in T5.3: `git config feature.manyFiles true` before the first `git add`, which turns on index version 4 (compressed paths) and the untracked cache, as large repositories and Scalar do. Commit a file, edit it, add an untracked one: RunAll must fail NoUnstagedChanges and NoUntrackedFiles exactly as on a plain index. Measured on 2026-11-19: libgit2 reads such an index (`feature.manyFiles` with `index.skipHash`); the script pins that against real Git.

# GitAssertions.NET — TDD Task List

Version 1 is a two-day build done with agents: an empty GitHub repo with CI, then five checks and RunAll (Phases 0 to 4). Tasks are INVEST (Independent, Negotiable, Valuable, Estimable, Small, Testable), and each feature goes red, green, refactor: failing tests from the spec first, then the simplest code that passes, then cleanup. The later phases follow in order: integration tests with real Git, accurate messages, security, a test package, the tasks before 1.0, and nice-to-have features.

A task builds the generic behavior first. Each corner case that makes it more exact, such as one more operation named in a message, is a small task of its own in a later phase, with a commit of its own.

Releases: each release closes a phase, and the phases run in order.

| Release | Closes | Contents |
|---|---|---|
| 0.1 | Phase 4 | The five checks and RunAll, with every test run in parallel. |
| 0.2 | Phase 6 | Integration tests with real Git, and accurate messages. |
| 0.3 | Phase 8 | Security (Phase 7), and a test package for private testing, kept in a local folder feed. |
| 1.0 | Phase 9 | The first release on nuget.org, while the library matures. |

A security finding is sorted like any other task: realistic and needed before 1.0, into Phase 9; realistic but not urgent, into Phase 10. A critical finding, one that harms a user of the library, such as lost work or a leaked secret or credential, goes into the current phase or Phase 9, as the owner decides. Out of scope since 2026-11-25: a hostile repository, one whose commits or `.git` folder were written to attack the library, because the library trusts the repository it checks as much as building it does (spec, Security); and new defenses against file names crafted to attack the library. File names can come from outside the commits, so a command takes only shell-safe names, and a path prints escaped and shortened; a gap found in those is fixed.

Phase 10 holds nice-to-have features, picked up after Phase 9 when they're needed. Task IDs are never reused. A task moved to another phase gets the next number there, unless a commit names it, and its title names its former ID. Deleted on 2026-11-25, after a review that kept only realistic threats: T8.9, T8.13, T9.1 to T9.7, and T9.10, whose text git history keeps; Phase 9, Security Pass II, became the phase before 1.0.

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
- [x] **T3.5 NoOperationInProgress**, covering a conflicting merge, rebase, cherry-pick, and revert. Any other operation gets the spec's generic message, which names libgit2's `CurrentOperation` value, and is not tested; Phase 6 names the rest one task at a time (`git am` moved to T10.7). Adds rebase, cherry-pick, and revert helpers to TempRepo.
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

Closed on 2026-11-24. Its open tasks moved on: T7.8 is T9.8, and T7.9 is T9.9; T7.3, later T9.7, was deleted (see the top of this file).

- [x] **T2.3 Shell-safe paths and output limits.** Message formatting follows the spec's Security > Shell-safe paths and Output limits. Tests first:
  - A path with `$(...)`, a backtick, `;`, `&`, `|`, `%`, `"`, `'`, `*`, or a leading `:` is listed as an entry but left out of every command, and appears in the unsafe-path note.
  - Newline, tab, carriage return, ESC, and a bidirectional override in a path are printed escaped (`\n`, `\t`, `\r`, `\u{001B}`, `\u{202E}`), so each entry stays on one line; a literal backslash prints as `\\`.
  - When no listed path is safe, the message has no command lines, only the note.
  - A 300-character path is shortened in entries (first 100 + `...` + last 60) and left out of commands.
  - A message that would exceed 16,000 characters is cut at a full line and ends with `  ...message truncated.`
  - `Files` still holds the raw, untruncated paths.
- [x] **T2.4 Hostile-repo safety.** Prove the library never runs anything the repository asks for (spec, Security > Untrusted repositories and libgit2). No library change is expected: libgit2 runs no hooks, no external filter drivers, and no fsmonitor, and the library registers no filters and never calls `GlobalSettings`. If a test below finds that something ran, stop and report it: the library does not change in this task. Tests first, in a new `HostileRepositoryTests`.
  - **Traps.** A test helper arms a `TempRepo` with six traps. Each one, when it runs, creates a marker file named after itself (`pre-commit`, `post-checkout`, `clean`, `smudge`, `process`, `fsmonitor`) in one marker folder:
    - hooks: `.git/hooks/pre-commit` and `.git/hooks/post-checkout`, `#!/bin/sh` scripts written with `Lf` line breaks, made executable with `File.SetUnixFileMode` where the platform has the bit (`!OperatingSystem.IsWindows()`);
    - a clean/smudge filter driver: `.gitattributes` gives `trapped.txt` `filter=trap`, and `.git/config` sets `filter.trap.clean` and `filter.trap.smudge` to a command that creates its marker and passes the content through, such as `touch '<marker>'; cat`;
    - a process filter driver: a second driver, `filter.trapprocess.process`, on a second file (`processed.txt filter=trapprocess`). Git runs a driver's `process` command instead of its `clean` and `smudge` when it has one, so one driver cannot test both;
    - fsmonitor: `core.fsmonitor` set to the path of a hook script. `git status` itself runs it, so it is the one trap a status read sets off. The first text of this task named only hooks and filters; fsmonitor is added because the spec and the README statement name it.
  - The marker folder is outside the working tree, under `.git`, so a marker can never show up as an untracked file, and it goes with the `TempRepo`. Every script names its marker by absolute path, with forward slashes and in single quotes, so a trap that ran from another working directory would still write where the test looks, and `sh` on Windows reads `C:/...`.
  - Order: commit `trapped.txt`, `processed.txt`, and `.gitattributes` first, then write the hooks and the config, then edit both files, so their stat no longer matches the index and a reader like `git status` must read them through the filters.
  - **Control tests first**, value tests with Shouldly: a theory over the six traps. Each case arms the whole repository, runs the git command that sets its trap off, and asserts that its marker exists. Without them, a trap built wrong (a CR after the shebang, no executable bit, a path the shell misreads) never fires, and the safety test passes for nothing. Commands, to be measured: `git status` for `clean`, `process`, and `fsmonitor`; `git commit --allow-empty -m <message>` for `pre-commit`; `git checkout -- trapped.txt` for `smudge` and `post-checkout`.
    - `GitCli` puts `-c core.fsmonitor=false` in front of every command, and a command-line `-c` beats the repository's config. The fsmonitor case passes its own `-c core.fsmonitor=<hook>` after it, which wins (measured on git 2.56 and 2.34.1). A hook path starts no daemon, so the reason `GitCli` turns fsmonitor off does not apply. The hook's path is relative to the working tree, where git runs it from (measured, from a subfolder too), so `core.fsmonitor=.git/trap-fsmonitor` needs no quoting.
    - The process trap answers the filter protocol's handshake with one packet git does not expect (`printf 0005x`), so git reports an error and goes on. Every case runs through `GitCli.RunLineAsync`, which does not throw, and checks only the marker. Measured: a trap that exits without a word, as the first text of this task had it, makes git 2.34.1 (Ubuntu 22.04 in WSL) stop with `fatal: the remote end hung up unexpectedly` before the clean filter, the hooks, and the checkout finish, so the other cases failed there; git 2.56 on Windows only reports an error.
  - **Safety test**, a value test: arm the whole repository, then `GitAssert.Open(root)` and `RunAll()`, which runs every check as if each were called on its own. The marker folder must be empty (`Directory.GetFiles(markers).ShouldBeEmpty()`), so a failure names the traps that fired. It asserts nothing about the result, not even with a snapshot: the check tests pin results, and a snapshot here would suggest the test is about them and repeat those tests (decided 2026-11-23). An exception from `Open` or `RunAll` still fails it.
  - **Owner validation**, a value test: after `Open` and `RunAll`, `GlobalSettings.GetOwnerValidation()` is still true. The setting is process-wide, so no test may change it. A repository owned by another user, which would show the validation at work, needs root or admin to set up and is not tested; the test's header says so.
  - **AGENTS.md**: add the exception to the snapshot rule, beside "Tests of `Open`, of exceptions, and of `CheckResult` itself stay value tests with Shouldly": a test that runs checks only to drive the library through a scenario, and proves something else, such as that no hook or filter ran, asserts only that, never the check's result.
  - **README**: a Security section that says, in short, what the spec says under Security > Untrusted repositories and libgit2: the library never runs hooks, filter drivers (clean, smudge, process), fsmonitor, or any other command a repository asks for, and leaves libgit2's repository owner validation (`safe.directory`) switched on; libgit2's settings belong to the whole process, so a filter the caller's own code registered runs during the checks, and owner validation it switched off stays off; `Open` refuses a repository whose own files name a network share, because opening one could send the user's credentials to that server (T7.12), and a symlink inside `.git` is not checked, so the checks are meant for repositories cloned or created on this machine; and opening a hostile repository still runs libgit2's native parsing code.
- [x] **T7.1 Supply chain: SHA-pinned actions.** Pin every action in the workflows to a full commit SHA, with its exact release in a comment: `uses: actions/checkout@<40 hex digits> # v7.x.y`. A tag such as `v7` is a pointer that whoever controls the action's repository can move, and every run then executes the commit it points to, with no change in this repository; a commit SHA names one exact content and cannot be moved. In March 2025 the tags of `tj-actions/changed-files` were moved to a malicious commit (CVE-2025-30066). Today `ci.yml` uses `actions/checkout@v7` and `actions/setup-dotnet@v6`. Proof: a green CI run with every `uses:` pinned.
  - Take each SHA from the action's own repository and its exact release tag, not the major tag: `gh api repos/actions/checkout/commits/v7.x.y --jq .sha`. The commits endpoint resolves an annotated tag to its commit, whose SHA is the one to pin; the tag object's own SHA is not a commit. Never copy a SHA from elsewhere: GitHub also serves a commit from any fork of the repository under the original's name, so a SHA from a fork would run the fork's code.
  - The version comment is what Dependabot (T7.2) reads to move the SHA and the comment together when a release comes out.
  - A pin covers only the action's own code. What the action downloads as it runs, such as the .NET SDK that `setup-dotnet` installs, is not pinned by it.
  - No NuGet lock files; the spec (Security > Supply chain) says why.
- [x] **T7.2 Dependabot** for NuGet and GitHub Actions: `.github/dependabot.yml` with the `nuget` and `github-actions` ecosystems, both at `/`, on a weekly schedule, with the analyzer packages in one group. Proof: the file exists, Dependabot's first update check runs (Insights > Dependency graph > Dependabot), and its first NuGet PR edits `Directory.Packages.props`, where the versions are. Dependabot alerts and security updates are switched on in the repository's settings, not in the file, by the repository's owner.
  - Ignore `Verify.XunitV3`: it is pinned to `[32.0.0]`, its last free version (`Directory.Packages.props`), and Dependabot would propose the next one every week.
  - With SHA-pinned actions (T7.1), Dependabot moves each SHA and its version comment together.
  - Dependabot is the whole policy for libgit2 too (spec, Security > Supply chain).
- [x] **T7.7 A destructive alternative is said in words, never given as a command.** `git clean -f` deletes untracked files, and `git restore` overwrites edits; neither can be undone, because Git never stored that content. Today both are ready-to-paste command lines, and an agent told to make the tests green may run whichever line passes fastest. Decided (2026-11-23): drop both command lines. The message says what can be done, warns that it cannot be undone, and says that an agent may need a person to confirm it. It names no command, so nothing can be pasted, and an agent is not pointed at a broader one such as `git clean -fd`. Write the templates and the examples in the spec first (Common format, NoUntrackedFiles, NoUnstagedChanges, and the Shell-safe paths example):

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
- [x] **T7.10 Rename `GitAssertions.ThisRepository.Tests` to `GitAssertions.Architecture.Tests`.** The project holds rules about this repository, not tests of the library: today the library's checks, run on this repository itself, and next the action-pin test of T7.11. `<Solution>.Architecture.Tests` is the name the repository owner's other projects give such a project, so a rule copied between them lands in the same place.
  - `git mv` the folder and the `.csproj`, and update every reference: `GitAssertions.slnx`, AGENTS.md (Keep Git healthy, and the ratchet section), the README, the spec, and the comment in `LibGit2Isolation.cs`. The namespace follows the new name. The class `ThisRepositoryTests` keeps its name, because it tests this repository, and the verified files keep theirs, named after the checks.
  - The `.csproj` comment first says what the project holds: rules about this repository. Its reason for not isolating libgit2 stays: a check of a real repository needs the git config that `git status` reads. A future rule that needs isolation goes into `GitAssertions.Tests`.
  - CI builds and tests the whole solution, and its job names stay, so `ci.yml` does not change, and T8.3's required checks still match.
  - Proof: the analyzer build is clean, the tests pass on Windows and on WSL, and `git log --follow` on `ThisRepositoryTests.cs` shows its history through the rename.
- [x] **T7.11 A test keeps every action pinned (after T7.10).** T7.1 pinned the actions, but nothing stops a later edit, by a person or by an agent copying an example, from adding `uses: actions/upload-artifact@v4`, which runs whatever the tag points to: Dependabot moves a pinned SHA, but leaves a tag a tag. A test in `GitAssertions.Architecture.Tests` fails on such a line, locally, before it is committed. Write it to be copied into other repositories: one file, `WorkflowPinTests.cs`, that needs only YamlDotNet, the test timeout constant, and the repository root the project already passes in (the `RepositoryRoot` assembly metadata), all named in the file's header.
  - It parses every `.yml` and `.yaml` file under `.github/` with YamlDotNet, which covers workflows and composite actions (`action.yml`) alike, and checks the value of every `uses:` key: a step, a job that calls a reusable workflow, or a flow mapping (`- { uses: ... }`), quoted or not. Only real keys count, so a `uses:` in a comment or in a `run:` script is not read. A value passes only as one of:
    - `<owner>/<repo>[/<path>]@<40 hex digits>`, with the release in a comment on its line (` # v1.2.3`), as the spec asks and Dependabot reads (T7.2). The parser drops comments, so the line itself is searched for ` #`;
    - a local action or workflow, `./<path>`, which is this repository's own code;
    - `docker://<image>@sha256:<64 hex digits>`, an image pinned by digest.
  - The failure lists every bad `uses:` as `<file>:<line>: uses: <value>`, then says what to do: pin it to the full commit SHA of a release, with the release in a comment (`uses: <owner>/<repo>@<40 hex digits> # v1.2.3`), taken from the action's own repository with `gh api repos/<owner>/<repo>/commits/<release tag> --jq .sha`.
  - A rule that finds nothing passes for ever, so the test also fails when it reads no `uses:` at all, naming the folder it read: a moved or renamed folder cannot make it pass by accident.
  - Tests first, value tests with Shouldly: theories over the rule, each row a one-line YAML snippet that must hold exactly one `uses:`, which pass a pinned action with its comment, a quoted one, a reusable workflow, a `./` action, and a digest-pinned image, and fail a tag (`@v4`), a branch (`@main`), a short SHA (7 digits), a pinned action without its comment, no ref, an empty value, a flow mapping with a tag, a reusable workflow at a tag, and a `docker://` image with a tag; a theory that reads no `uses:` in a comment or a `run:` script; and the test of this repository's workflows, which passes today.
- [x] **CI read-only token:** set `permissions: contents: read` in every workflow. Proof: a green CI run with the restricted permissions. (Already in the CI workflow from T0.3; keep it in any new workflow.)
- [x] **T7.12 `Open` refuses a repository that names a network path (removed by T8.15).** The rule is in the spec (Security > Untrusted repositories and libgit2, Network paths; Public API): before any LibGit2Sharp call, `Open` reads the files the repository brings, and throws an `ArgumentException` for `repoPath` when one names a network path.
  - The check is the first thing `Open` does once it has the full path, before `Repository.IsValid`, which already reads the config. Say so in a comment there. When there is no `.git` folder or file under the path, the check finds nothing, and `Open` throws its "not a Git repository" error as today.
  - It needs a reader for the part of git's config format the rule uses, not a whole parser: sections (`[core]`, `[includeIf "gitdir:~/work/"]`, and the old `[section.subsection]` form), names compared ignoring case, values with or without double quotes, the escapes `\\`, `\"`, `\n`, `\t`, and `\b`, a `\` at the end of a line that continues the value, and comments after `#` or `;` outside quotes (https://git-scm.com/docs/git-config#_syntax). An include path is resolved as git resolves it: `~/` against the home folder, a relative one against the folder of the file that includes it; a missing file is skipped, as git skips it. Measure first what real git writes: `git config include.path '\\server\share\x'` stores the backslashes escaped.
  - Every other file the rule names is plain: one path per line in `alternates` (a `#` line is a comment, and a relative path is resolved against the `objects` folder), and a `.git` file's `gitdir: <path>` line.
  - Tests first, value tests with Shouldly. Every network path in them uses the host `gitassertions.invalid`, a name that can never resolve (RFC 2606), so a check that misses one fails visibly instead of reaching a server:
    - refused, each value written with real git (`TempRepo.GitAsync`, `git config`), both as `\\gitassertions.invalid\share\x` and as `//gitassertions.invalid/share/x`: `include.path`, `includeIf."gitdir:**".path`, `core.excludesFile`, and `core.attributesFile`; a local included file that sets one; an alternates file with such a line; a `.git` file whose `gitdir:` names one; and a hand-written config with the value quoted, escaped, and split over two lines by a trailing `\`. Each message names the file, the key, and the value;
    - opened: `remote.origin.url` set to `//gitassertions.invalid/repo.git`, which the checks never contact; `core.excludesFile` set to a local long path, `\\?\C:\ignore` (Windows only, skipped elsewhere with the reason); and a linked worktree and a submodule's folder, whose `.git` files name local folders.

## Phase 8: Release 0.3, a test package

The smallest step to a package a project can try: packed, tagged, and kept in a local folder feed, not on nuget.org. Every other task waits for Phase 9.

- [x] **T8.1 Linux and macOS CI legs.** Run restore, build, and test on `ubuntu-latest` and `macos-latest` as well as `windows-latest`, as a job matrix with `fail-fast: false` (see the spec's Test tooling > Platforms). Fix or skip, with a stated reason, each test that depends on the behavior of one platform. Proof: a green CI run on all three.
  - The matrix is in `ci.yml`. All three legs are green on CI (2026-11-22).
- [x] **T8.2 NuGet package.** The library becomes a package that a test project on .NET 11 or later can install.
  - The target stays `net11.0` (decided 2026-11-25), so a project that installs the package targets .NET 11 or later. .NET 11 is supported until November 2028, as long as .NET 10, the LTS release (https://learn.microsoft.com/dotnet/core/releases-and-support). LibGit2Sharp 0.32.0 ships `net8.0` assemblies, so it sets no limit.
  - Package metadata in the `.csproj`: `PackageId` `GitAssertions`, a description, `PackageLicenseExpression` `MIT`, `RepositoryUrl`, `PackageTags`, and the README as `PackageReadmeFile`. XML documentation on for the library (`GenerateDocumentationFile`), so the doc comments show in a consumer's IDE; fix every warning it raises.
  - SourceLink, which the .NET SDK includes for GitHub repositories, a symbols package (`.snupkg`), and no author signature (spec, Security > Supply chain). The version is given when packing (`-p:Version=0.3.0`), so no file holds a release number.
  - CI runs `dotnet pack` on every run, so a change that breaks packing fails before a release needs it.
  - README: a usage sample, and the LibGit2Sharp version the package depends on: a consumer that uses LibGit2Sharp too gets one shared version, and the native library's file name is fixed for each version.
  - Proof: CI packs on every run, and NuGet Package Explorer's health check shows SourceLink and a deterministic build.
- [x] **T8.15 Remove the network-path refusal and its config reader (T7.12).** Decided on 2026-11-25: the library trusts the repository it checks as much as building it does. The checks run in the test suite of the repository they check, so that repository is built first, and the build runs its code (MSBuild targets, `Directory.Build.props`, the tests) with the user's rights: whoever can write a hostile `.git/config` or commit a symlinked `.gitignore` can write a build target instead. Git reads the same config before the library does: measured on 2026-11-24, `git config --get` read a value from a file that `include.path` names on `//localhost/C$/...`, so git opens a share named there whenever an IDE or a shell prompt runs `git status`, and git also runs commands the config names (`core.fsmonitor`, `core.hooksPath`), which libgit2 never does. Sending credentials to a share is stopped by the system, for every program at once: the SMB client blocks NTLM on Windows 11 24H2 and later (`Set-SmbClientConfiguration -BlockNTLM $true`, https://learn.microsoft.com/windows-server/storage/file-server/smb-ntlm-blocking). The refusal also turns away a repository set up on purpose, such as a clone made with `git clone --reference` to a mirror on a share, whose alternates file names it.
  - Spec first:
    - Non-goals: add "Defending against a hostile repository, one whose commits or `.git` folder were written to attack the library: it trusts the repository it checks as much as building it does (Security)."
    - Security > Untrusted repositories and libgit2: delete the Network paths point with its sub-points, and the "Not covered yet" point. In place of "Who can bring what", say the trust model and its reason, as above: the checks run in the test suite of the repository they check, so it is built first, and git reads its config first; a repository from somewhere untrusted is unsafe to build or to run git in, and the library adds no defense of its own. File names stay untrusted input, because they can come from outside the repository's commits, such as an untracked download, an unpacked archive, or generated output, and fix lines are run in a shell (Shell-safe paths, Output limits). The points on hooks, filters, fsmonitor, owner validation, and libgit2's native parsing stay.
    - Public API, `Open`: delete the sentence on a repository whose own files name a network path.
  - Code, each file deleted with `git rm`:
    - `src/GitAssertions`: `NetworkPathGuard.cs`, `NetworkPathReference.cs`, `GitConfigReader.cs`, and `GitConfigEntry.cs`.
    - `GitAssert.Open`: the `NetworkPathGuard.FindFirst` call, its comment, and the words "or holds a repository whose own files name a network path" in the `ArgumentException` doc.
    - `FailureMessage`: `DisplayInMessage`, and the `doubleBackslashes` parameter it added to `Display` and `AppendEscaped`, so both are as they were before T7.12 (`git show 2ed7cc8 -- src/GitAssertions/FailureMessage.cs`), with any later change kept.
    - Tests: `GitConfigReaderTests.cs`, `NetworkPathGuardTests.cs`, `NetworkPathReferenceTests.cs`, and `OpenNetworkPathTests.cs`. No other test changes, and no verified file: these were value tests.
  - README, Security: in place of its first paragraph, the trust model in a sentence or two, and delete the point on `Open` refusing a repository that names a network share. The other points stay.
  - Proof: `git grep -n -E "NetworkPath|GitConfigReader|GitConfigEntry|DisplayInMessage" -- src tests` finds nothing; the analyzer build is clean; the tests pass on Windows and on WSL, and their count falls by exactly the number of tests the four deleted files held.
- [x] **T8.16 The docs name only realistic threats (after T8.15).** A review on 2026-11-25 found wording that still treats the checked repository as hostile, which the trust model rules out (spec, Security > Untrusted repositories and libgit2):
  - Spec, Scope: the phases after version 1 add integration tests with real Git, accurate messages, security, a test package, the tasks before 1.0, and nice-to-have features; there is no second security pass.
  - Spec, Security > Shell-safe paths: in place of "File names come from the repository, which may be hostile (for example, a repo an agent cloned)", the real reasons: a person or an agent pastes a `Fix:` command into a shell; file names can come from outside the commits, such as a download, an unpacked archive, or generated output; and ordinary names hold spaces and non-ASCII letters, which a command must quote right. The same in the summary of `FailureMessage`.
  - Spec, Untrusted repositories and libgit2: the trust-model point in two sentences, the rule and its reason (the repository is built before its tests run, and git reads its config first); the NTLM and `git clone --reference` details stay in T8.15. Delete "Opening a hostile repository still runs native libgit2 parsing code": a hostile repository is out of scope, and how a libgit2 fix arrives is said under Supply chain.
  - Spec, Supply chain: the `WorkflowPinTests` point says twice that a test fails on an unpinned `uses:` line; keep the first.
  - README: the paragraph on file names under Status joins Security, in place of its sentence on file names, without "which may be hostile"; delete the point on opening a hostile repository.
  - Proof: `git grep -n -i "may be hostile" -- README.md docs/specification.md src` finds nothing, and the analyzer build is clean, because a doc comment changed.
- [x] **T8.17 Release 0.3.0 to a local folder feed (after T8.2 and T8.16).** 0.3 is for private testing, so it goes to no public feed: nothing to unlist later, and no API key to keep. nuget.org waits for 1.0. The package ID `GitAssertions` was free on nuget.org on 2026-11-25.
  - Tag the release commit `v0.3.0`, and pack it: `dotnet pack src/GitAssertions -c Release -p:Version=0.3.0 -o <folder>`.
  - Each project that tests it commits the package in a folder of its own, such as `nuget-local/`, and names that folder in a `nuget.config` at its root, so its GitHub build restores it with no credentials, as a build on a developer's machine does. Package source mapping takes `GitAssertions` from that folder only, so a package of the same name published on nuget.org later is never restored in its place:

    ```xml
    <configuration>
      <packageSources>
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
        <add key="local" value="nuget-local" />
      </packageSources>
      <packageSourceMapping>
        <packageSource key="nuget.org">
          <package pattern="*" />
        </packageSource>
        <packageSource key="local">
          <package pattern="GitAssertions" />
        </packageSource>
      </packageSourceMapping>
    </configuration>
    ```

  - Not `packages/`: the .NET `.gitignore` template ignores `**/[Pp]ackages/*` and `*.nupkg`, so the package would need `git add -f`, and NoTrackedIgnoredFiles would then fail in that project. Its `.gitignore` gets `!nuget-local/*.nupkg` after the `*.nupkg` rule, and `git check-ignore` on the package prints nothing and exits 1. Leave `-v` off: it prints the `!` rule that matches.
  - A version is packed once: NuGet keeps the first package of a version in its global cache on every machine, so a changed package under the same version is never seen. A fix ships as 0.3.1.
  - README, Status, and the spec's Scope, which says to use a project reference until NuGet packaging: 0.3 is a test package, kept in a local folder feed, not on nuget.org.
  - An infrastructure task. Proof: a project that tests it restores 0.3.0 from its folder, on a developer's machine and on its GitHub build, and a test there passes `RunAll`.

## Phase 9: Before 1.0

The most urgent tasks after 0.3, then 1.0, the first release on nuget.org. Agent safety first: T9.8 and T9.9 fit the definition of critical at the top of this file. Publishing to nuget.org becomes a task here once what starts it is decided.

- [ ] **T9.8 A file that looks like a secret is not offered to `git add` (formerly T7.8).** For an untracked `.env`, the first Fix line today is `git add -- .env`, which commits a secrets file. Decided (2026-11-23): NoUntrackedFiles leaves such a file out of `git add` and prints a line for it, after the entries and before `Fix:`. Write the template and the example in the spec first (Common format, NoUntrackedFiles):

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
  - It is a heuristic, and the spec says so: it goes by the file name, the last part of the path, not by the content, compared ignoring case, because Windows and macOS open `.ENV` as `.env`. The names are `.env`, `.env.local`, and `.env.<mode>.local`, which Vite and Next.js keep local-only and say can hold secrets; and the OpenSSH private keys `id_rsa`, `id_dsa`, `id_ecdsa`, `id_ecdsa_sk`, `id_ed25519`, `id_ed25519_sk`, and `id_mldsa44_ed25519`, the default names `ssh-keygen` gives them (`id_dsa` for keys made before OpenSSH 10 dropped DSA). Every other file is offered to `git add` as today: mode files such as `.env.production` and `.env.test`, which projects commit with defaults; templates such as `.env.example`; the public halves, `*.pub`; and `*.pfx`, `*.p12`, and `*.key`, which .NET test certificates and Keynote documents use too. A secret saved under any other name is not recognized.
  - Tests first, snapshot tests: `.env` next to a plain file (the example above); `.env` alone (no `Fix:` block, as when no path is left for a command); `config/.env`, whose line names the whole path; `.env.production.local` and `id_ed25519_sk`, each left out of `git add`; `.env.production`, `.env.example`, and `id_ed25519.pub`, each offered to `git add` like any file.
- [ ] **T9.9 The abort line is said in words too (after T7.7; formerly T7.9).** NoOperationInProgress offers `Or abort it:` with `git <operation> --abort`, which makes the check pass at once, so it is the fastest line for an agent told to make the tests green. `git merge --abort` throws away the conflict resolutions made so far, and git's documentation warns that it cannot always rebuild uncommitted changes from before the merge; `git rebase --abort`, `git cherry-pick --abort`, and `git revert --abort` put the branch back, and the work done meanwhile can be found again only through the reflog. So it is a destructive alternative, and T7.7's rule applies: the command line becomes `Or abort it, if it is not wanted. Aborting throws away the conflict resolutions made so far, so an agent may need a person to confirm it.` The `--continue` fix keeps its command, and bisect keeps `git bisect reset`, which loses nothing. Write the template and the example in the spec first (NoOperationInProgress); every verified file of an operation in progress changes, the scripts' too.
- [ ] **T9.12 A script scenario: an ignored file forced into the index and left staged.** NoTrackedIgnoredFiles reads the index, so it fails for a file an ignore rule matches as soon as the file is staged, before any commit. An agent that runs `git add -f .env` past a `.env` rule fails the check in its own test run, before a person commits. Two tests pin a forced file today, `NoTrackedIgnoredFiles_FileForceAddedPastIgnoreRule_FailsAndListsIt` and the `Force_added_file` script, and both commit it, so nothing pins the staged state.
  - `ScriptTests.Force_added_file_left_staged.script.txt`: `git init`; `echo .env .gitignore`; `git add .gitignore`; `git commit -m "Ignore .env"`; `echo secret .env`; `git add -f .env`; and no commit. Its first comment says what it pins.
  - Its verified file, written by hand first: git's block (`A  .env` from `git status`, `.env` from `git ls-files -i -c`), then `RunAll` failing NoTrackedIgnoredFiles alone, listing `.env`, with its `git rm -r --cached -- .env` line.
  - Check by hand that the fix line, run in the finished repository, unstages `.env` and leaves it on disk.
- [ ] **T9.13 README: running the checks in your own test suite (formerly T8.5).** A section for consumers:
  - Put the Git checks in an xUnit collection with `DisableParallelization = true`, which runs after the parallel tests: other tests write files while they run (snapshots, coverage, logs, test databases), so a NoUntrackedFiles test beside them fails now and then. This repository needs none: its tests write only `.received.txt` files, which are ignored.
  - Results follow the machine's Git config, as `git status` does (`core.autocrlf`, global ignore rules and `core.excludesFile`, `core.longpaths`, `safe.directory`), so the same repository can get a different verdict on another machine. When `Open` throws because the repository is not owned by the current user, as in a container whose checkout belongs to another user, the fix is `git config --global --add safe.directory <path>`.
  - Some changes are hidden from Git and from the checks alike: an edit to a file marked `assume-unchanged` or `skip-worktree`, and on Windows a rename that changes only letter case, made outside Git (`git mv Readme.md README.md` does it right).
  - A test's `Timeout` cannot stop a libgit2 call that hangs: native code is not interrupted, so the call keeps its thread, and the test host, alive.
- [ ] **T9.14 The checks write nothing (formerly T8.6).** Measured on 2026-11-19: libgit2's reads left `.git` unchanged, while a plain `git status` rewrote the index. Pin the read-only promise with a value test: on a repository with an untracked file, an unstaged edit, a staged change, and an ignored file, hash every file under `.git` (its path and content) before and after `Open`, every check, and `RunAll`, and the two sets must be equal. It asserts nothing about the results, under the AGENTS.md exception T2.4 adds.
- [ ] **T9.15 `Open` on a path with non-ASCII letters and a space (formerly T8.7).** Such paths are common, such as `C:\Users\Michał Test\repo`, and they go through libgit2's conversion of paths to UTF-8. Test first: a repository in a folder named `Michał Test` opens, and RunAll on it is healthy (`VerifyHealthy`). `TempRepo` gets a way to create the repository under a named folder.
- [ ] **T9.16 Exception messages say where and what to do (formerly T8.10).** A libgit2 error is re-thrown with libgit2's text alone today, and a test pins that (spec, Public API). Add the repository path, the check's name, and a next step where one is known, with libgit2's text at the end, so the error tells the caller what to do. Write the format in the spec first; the test that pins today's text changes with it.
  - The known case: a long untracked path on Windows, over 260 characters without `core.longpaths` (deep `node_modules`, test output), throws `path too long: '<path>'` (measured for an untracked path). Its next step is `git config core.longpaths true`: measure first that libgit2 then reads the path, and if it does not, the message names the limit instead. The check throws rather than skipping the path, as `git status` does, because skipping would hide the untracked file the check exists to find.
  - Tests first, value tests: a check on a repository whose folder was deleted names the path and the check; the long path names its next step (Windows only, skipped elsewhere with the reason).
- [ ] **T9.17 Packfiles (formerly T2.5).** Research first: how Git stores objects in packfiles (`.git/objects/pack`), which every cloned repository uses, and how libgit2 reads them. Background, measured on 2026-11-18: libgit2 keeps a packfile open once it has read from it, and Windows then refuses to delete it; loose objects are never held open, so a fresh TempRepo cannot show a leaked handle, and no test today proves that a read releases its handle (a `Repository` kept in a field passes them all). Then, tests first: a TempRepo helper that packs the repository's objects, and a test that on a packed repository the folder can be deleted right after a `GitAssert` read, with nothing disposed. A `Repository` kept in a field must turn that test red.
  - libgit2 shares open packfiles across the whole process by path, so the `TempRepo`'s own `Repo` handle can keep a pack open: release it before asserting that the folder can be deleted.
- [ ] **T9.18 The test folders on a Dev Drive, measured (formerly T8.14).** Measured on 2026-11-23 on this laptop (i5-10210U, 4 cores, 8 threads), with nothing else running: `GitAssertions.Tests` (359 tests) took 27.3 s, and the tests' own times add up to 199 s, so about 7.4 ran at any moment and the CPU was the limit. `ScriptTests` took 129 s of the 199 (65%), and a git call that takes about 80 ms on its own took 400 to 600 ms in the run. Every test writes a new repository under `%TEMP%`, and Defender's real-time protection scans each file as it is written. A Dev Drive is a ReFS volume that Windows trusts when it is created, and on it Defender runs in performance mode: it scans a file after it is opened, not while (https://learn.microsoft.com/windows/dev-drive/). The scan still runs, Microsoft says performance mode does not lower Defender's CPU use, and git, installed on C:, is scanned as it starts, as before. So the gain may be smaller than for a build. This task moves the test folders there and measures the gain. It replaces a Defender exclusion for the same folders (T8.13, deleted on 2026-11-25): performance mode still scans every file, where an exclusion would scan none.
  - Maciek creates the Dev Drive, as administrator: Settings > System > Storage > Advanced storage settings > Disks & volumes > Create Dev Drive, on a new dynamically expanding VHDX in a folder of his own. 50 GB, the minimum, is enough, and the file grows only as it is written. This machine meets the prerequisites (Windows 11 build 26300, 64 GB of memory, 115 GB free on C:). Then `fsutil devdrv query <letter>:` says the drive is trusted, and Windows Security > Virus & threat protection > Manage settings > Dev Drive protection > See volumes shows performance mode on for it.
  - One folder on the drive holds the test folders and nothing else, such as `D:\GitAssertionsTemp`. It lies outside every Git repository: tests run `git` in their temporary folders, and git searches the folders above for a repository (nothing sets `GIT_CEILING_DIRECTORIES`), so a script line run before `git init`, or a step that fails, would act on that repository.
  - Before any test runs, `GitAssertions.Tests` sets `TMP` and `TEMP` for its own process to the folder that `GITASSERTIONS_TEST_TEMP` names, and creates the folder if it is missing. `TemporaryDirectory` follows `Path.GetTempPath()`, which reads `TMP` and then `TEMP` on Windows, and the git processes the tests start inherit both. Windows only, and only when the variable is set: unset, nothing changes, as on CI, on WSL, and on other machines. Only the test process: a user-wide `TMP` or `TEMP` would move every program's temporary files onto the drive. README, Build, says this in a few lines. No unit test: the setting changes only where the folders go, and `TempRepoTests` already pins that they sit under `Path.GetTempPath()`.
  - Measure on one commit, with nothing else running: no agent console, and no other build or test run. On 2026-11-23, runs made beside them took twice as long. Build Debug once, then run `tests\GitAssertions.Tests\bin\Debug\net11.0\GitAssertions.Tests.exe -noLogo -xml <file>` three times as it is, and three times in a PowerShell console where `$env:GITASSERTIONS_TEST_TEMP` names the folder. For each set, take the median of the summary's `Time:`, the sum of the tests' `time` attributes in the XML, and the sum for `ScriptTests`. The suite has grown since the numbers above, so compare the two new sets with each other, not with those numbers.
  - Every test passes in both sets. ReFS differs from NTFS, for example it makes no 8.3 short names, so a test that fails only on the Dev Drive is a finding: report it, and do not fix it in this task.
  - Out of scope: moving the repository, the NuGet cache, or the user's `%TEMP%` onto the drive. One change at a time, so the numbers show what this one did.
  - An infrastructure task. Proof: `fsutil devdrv query` shows a trusted Dev Drive; with the variable set, a full test run creates its repositories in the folder, none in `%TEMP%`; and both sets of numbers are written into this task. Then Maciek sets `GITASSERTIONS_TEST_TEMP` for his user.

## Phase 10: Nice-to-have (after Phase 9, when needed)

- [ ] **T10.1 NoUnstagedChanges `typechange` kind.** A tracked file replaced by another type, such as a symlink, which `git status` prints as `typechange`. libgit2 reports it as `TypeChangeInWorkdir`, which the check does not map today, so the check passes. Report it with a `typechange` kind and the same Fix lines as `modified`. The test skips when `File.CreateSymbolicLink` is refused, as it is on Windows without Developer Mode.
- [ ] **T10.2 NoMixedStaging check:** a narrower check than NoUnstagedChanges, its own method, not in the default `RunAll`, so a file staged and then changed again does not print in two blocks. Its scenarios (staged then edited or deleted) are already NoUnstagedChanges tests.
- [ ] **T10.3 NotDetachedHead check**, including unborn HEAD.
- [ ] **T10.4 OnExpectedBranch check.**
- [ ] **T10.5 NoStaleIndexLock check**, with a real-Git test of a stale `index.lock`.
- [ ] **T10.6 Options:** `RunAll(options)` with per-check on/off, allow-lists, expected branch. Includes the spec's deferred scenarios: allow-listed path passes, disabled checks are skipped.
- [ ] **T10.7 `git am` in NoOperationInProgress.** libgit2 reports `ApplyMailbox` for a `git am` stopped halfway, and `ApplyMailboxOrRebase` when it cannot tell `git am` from the old rebase backend; T3.5 prints both with the generic message. Name `git am` for `ApplyMailbox`, with `git am --continue` and `git am --abort` said as T9.9 says an abort. `ApplyMailboxOrRebase` keeps the generic message, because the library cannot tell which operation it is. Tests with real Git.
- [ ] **T10.8 Review real repos** for new scenarios and add them as new tasks.
- [ ] **T10.10 Index version 4 and the untracked cache against real Git.** A script scenario, as in T5.3: `git config feature.manyFiles true` before the first `git add`, which turns on index version 4 (compressed paths) and the untracked cache, as large repositories and Scalar do. Commit a file, edit it, add an untracked one: RunAll must fail NoUnstagedChanges and NoUntrackedFiles exactly as on a plain index. Measured on 2026-11-19: libgit2 reads such an index (`feature.manyFiles` with `index.skipHash`); the script pins that against real Git.
- [ ] **T10.11 Branch protection (formerly T8.3)** on main, requiring CI: the three jobs `build-and-test (windows-latest)`, `build-and-test (ubuntu-latest)`, and `build-and-test (macos-latest)` (T8.1). A required check is matched by job name, so renaming a job or a platform leaves the old name required and blocks every pull request.
- [ ] **T10.12 `Open` names `repoPath` for a path it cannot read (formerly T8.8).** For a path such as one with a NUL character, `Path.GetFullPath` throws an `ArgumentException` whose parameter name is `path`, not `repoPath`. Catch it and throw an `ArgumentException` for `repoPath` whose message says the path is not valid. Test first, a value test: `Open("a\0b")` throws with `ParamName` `repoPath`.
- [ ] **T10.13 A native library that fails to load gives a clear error (formerly T8.11).** On an unsupported platform, or after a trimmed publish that left out the native binary, the first libgit2 call fails with `TypeInitializationException` or `DllNotFoundException`. Neither is a LibGit2Sharp exception, so `GitAssert`'s wrapper lets it through, and its message names the internal type `LibGit2Sharp.Core.NativeMethods`. Re-throw both as the `InvalidOperationException` every failed read gives, with a message that names the platform (`RuntimeInformation.RuntimeIdentifier`) and says the native libgit2 library could not be loaded. Write it in the spec (Public API) first. Test first, a value test: the wrapper, given a call that throws each of the two, throws that message.
- [ ] **T10.14 An index libgit2 cannot read gives a clear error (after T9.16; formerly T8.12).** libgit2 refuses an index with an extension it does not know, so every check throws `unsupported mandatory extension: 'sdir'` with a sparse index (`git sparse-checkout set --sparse-index <folder>`) and `'link'` with a split index (`git config core.splitIndex true`) (measured with libgit2 1.8.6). Map both to a message in T9.16's format that names the feature and the git command that turns it off; measure the commands, which must leave an index the checks can read. Tests first, value tests with real Git (`TempRepo.GitAsync`): a sparse index and a split index each make every check throw that message, and after the command it names, the checks read the repository.
- [ ] **T10.15 A performance test on a repository of 100,000 tracked files, run by hand (formerly T9.11).** An audit on 2026-11-24 pointed out that NoTrackedIgnoredFiles asks libgit2 about each tracked file on its own (`Ignore.IsPathIgnored`), and that every status read walks the whole working tree, so a large repository, such as a monorepo, could take seconds. Nothing measures it. This test does, and is run by hand from time to time, not on every run.
  - `PerformanceTests` in `GitAssertions.Tests`: one test, marked `Explicit = true`, which xUnit runs only when asked (`-explicit only`, or by name in the IDE), and tagged `Category=Performance`, a new constant in `TestCategories`. CI and a plain `dotnet test` skip it. It joins a collection with `DisableParallelization = true`, with the reason in its header: a time taken while other tests run measures them too. The README (Build) says how to run it.
  - The repository: 100,000 tracked files, 100 in each of 1,000 folders three levels deep (10 × 10 × 10), each holding the same line, so git stores a single blob. A `.gitignore` in the root with the usual .NET rules (`bin/`, `obj/`, `*.user`), and one in each of the 100 folders of the second level. It is committed with real git, one `git add -A` and one `git commit`, each with a timeout of its own, because `GitCli`'s default of 15 s is too short. Nothing is ignored or changed: a healthy repository, the common case. A class fixture builds it before the test's own timeout starts, and deletes it after.
  - The test runs each check and then `RunAll` on it, one after another, and writes each one's time to the test output. It asserts only the time: each finishes within 30 s (`TestTimeouts.DefaultMilliseconds`, the deadline every test here has, which the audit said a large repository could miss), and the test's own timeout allows all six. It asserts nothing about the results, under the AGENTS.md exception that T2.4 adds.
  - Proof: the times of the first run, with the machine and the date, written into this task.

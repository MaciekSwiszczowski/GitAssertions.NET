# GitAssertions.NET — TDD Task List

Version 1 is a two-day build done with agents: an empty GitHub repo with CI, then five checks and RunAll (Phases 0 to 4). Tasks are INVEST (Independent, Negotiable, Valuable, Estimable, Small, Testable), and each feature goes red, green, refactor: failing tests from the spec first, then the simplest code that passes, then cleanup. The later phases follow in order: integration tests with real Git, accurate messages, security, release preparation, and nice-to-have features.

A task builds the generic behavior first. Each corner case that makes it more exact, such as one more operation named in a message, is a small task of its own in a later phase, with a commit of its own.

Releases (D1 and D15 in docs/review-findings.md): each release closes a phase, and the phases run in order.

| Release | Closes | Contents |
|---|---|---|
| 0.1 | Phase 4 | The five checks and RunAll. |
| 0.2 | Phase 6 | Integration tests with real Git, and accurate messages. |
| 0.3 | Phase 7 | Security. |
| 1.0 | Phase 8 | Release preparation: the first public release. |

Phase 9 holds nice-to-have features, picked up after 1.0 when they're needed. A task moved to a later phase keeps its ID.

## Definition of done

**Behavior tasks** (Phases 1 to 6, and any later task that adds or changes library or test-helper behavior):

- Tests written first and seen failing.
- All tests green locally and on GitHub Actions (once T0.3 exists).
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
- [x] **T3.5 NoOperationInProgress**, covering a conflicting merge, rebase, cherry-pick, and revert. Any other operation gets the spec's generic message, which names libgit2's `CurrentOperation` value, and is not tested; Phase 6 names the rest one task at a time. Adds rebase, cherry-pick, and revert helpers to TempRepo.
- [x] **T3.6 NoTrackedIgnoredFiles**, covering files committed before the ignore rule, force-added files, bin/obj grouping (only a folder that is itself ignored and holds no tracked file the check does not report, F10), and negated rules.

## Phase 4: RunAll

- [ ] **T4.1 RunAll.** `git.RunAll()` (no options in v1) runs all five checks and returns a `RunAllResult` as defined in the spec: `Results`, `Failures`, `IsHealthy`, and `Message`. Tests first: two broken rules produce two `Failures` and a `Message` matching the spec's RunAll template (header plus two blocks); a clean repo is healthy with five healthy `Results`.

## Phase 5: Integration tests with real Git

The git CLI sets up the scenarios libgit2 cannot create, and checks what libgit2 reads against what real Git does. Only the tests run git; the library never does (spec, Goals). The tests are tagged as integration tests and skipped when git isn't on PATH.

- [ ] **T5.1 CliWrap git runner.** A test helper that runs git in a TempRepo through CliWrap, with a timeout and cancellation so no process is left running, and with the CLI's version of `LibGit2Isolation`. Follow the lessons in C17 in docs/review-findings.md. Tests first: a git command run in a TempRepo changes what `RetrieveStatus` reports.
- [ ] **T5.2 Line endings against real Git.** The NoUnstagedChanges autocrlf scenarios, set up with git, give the same result as `git status` (P3 and P4 in docs/review-findings.md).
- [ ] **T5.3 Ignore rules against real Git.** A file force-added with `git add -f`, and a file committed before its ignore rule, fail NoTrackedIgnoredFiles.
- [ ] **T5.4 Operations left in progress by real Git.** A conflicting `git merge` and a conflicting `git rebase` fail NoConflicts and NoOperationInProgress, as the LibGit2Sharp scenarios do.

## Phase 6: Accurate messages

Small tasks, one commit each, so that no message misleads: each one names a state the checks report generically today, or fixes advice that does not fit. A scenario libgit2 cannot set up is tested with real Git only (Phase 5), never by writing Git's state files by hand.

- [ ] **T6.1 Multi-commit cherry-pick and revert (F07).** libgit2 reports `CherryPickSequence` and `RevertSequence`, which T3.5 prints with the generic message. Map them to `cherry-pick` and `revert`, with the same Fix lines. Tests with real Git only, because libgit2 has no sequencer: `git cherry-pick A B` and `git revert A B`, each with a conflict in the first commit.
- [ ] **T6.2 Bisect (F07).** libgit2 reports `Bisect`, which T3.5 prints with the generic message. Name it, with the fix `git bisect reset`; write its message template in the spec first. Test with real Git only, because libgit2 has no bisect: `git bisect start`, `git bisect bad`, `git bisect good <commit>`.
- [ ] **T6.3 Operations stopped without a conflict (F07).** `git merge --no-commit` reports `Merge`, and an interactive rebase stopped at an `edit` step reports `RebaseInteractive`. T3.5's Fix line, "once all conflicts are resolved and staged", misleads when there was no conflict: reword it in the spec, then update T3.5's verified files to match. Tests: `merge --no-commit` in-process (`MergeOptions.CommitOnSuccess = false`); the `edit` stop with real Git only, because libgit2 has no interactive rebase.
- [ ] **T6.4 Aborted operation passes.** Tests first: a conflicting rebase aborted with LibGit2Sharp's `Rebase.Abort()` passes NoOperationInProgress, and so does a conflicting merge aborted with `git merge --abort` (real Git, because LibGit2Sharp has no merge abort).
- [ ] **T6.5 Unborn HEAD (F11).** A repository with no commits passes every check and RunAll. NoConflicts has this scenario already; add it, on `TempRepo.CreateUnborn`, to NoUntrackedFiles, NoUnstagedChanges, NoOperationInProgress, NoTrackedIgnoredFiles, and RunAll.
- [ ] **T6.6 Intent-to-add (D4, F02).** A file recorded with `git add -N` is reported as `modified` and offered `git restore`, which empties it. Choose among D4's options first (recommended there: (a), a kind of its own with `git add` only and no discard line, which needs fix commands that take only some entries' paths). In-process tests stage the empty blob, as D4 describes; one test uses the real `git add -N`.

## Phase 7: Security

This phase will grow: the security findings S01 to S10 and the decisions D6 to D8 in docs/review-findings.md become tasks here as they are decided.

- [ ] **T2.3 Shell-safe paths and output limits.** Message formatting follows the spec's Security > Shell-safe paths and Output limits. Tests first:
  - A path with `$(...)`, a backtick, `;`, `&`, `|`, `%`, `"`, `'`, `*`, or a leading `:` is listed as an entry but left out of every command, and appears in the unsafe-path note.
  - Newline, tab, carriage return, ESC, and a bidirectional override in a path are printed escaped (`\n`, `\t`, `\r`, `\u{001B}`, `\u{202E}`), so each entry stays on one line; a literal backslash prints as `\\`.
  - When no listed path is safe, the message has no command lines, only the note.
  - A 300-character path is shortened in entries (first 100 + `...` + last 60) and left out of commands.
  - A message that would exceed 16,000 characters is cut at a full line and ends with `  ...message truncated.`
  - `Files` still holds the raw, untruncated paths.
- [ ] **T2.4 Hostile-repo safety.** Prove the library never runs anything the repository asks for. Tests first: in a TempRepo with an executable `pre-commit` and `post-checkout` hook and a `.gitattributes` filter driver whose clean/smudge/process commands would create a marker file, opening the repo and running every v1 check never creates the marker. Also assert the library leaves libgit2 owner validation switched on. Add the "never runs hooks, filters, or fsmonitor" statement to the README.
- [ ] **T7.1 Supply chain.** Package lock files (`RestorePackagesWithLockFile`, locked restore in CI) on top of central package versions, and GitHub Actions pinned to commit SHAs. Proof: CI restores in locked mode and the workflow uses SHA-pinned actions.
- [ ] **T7.2 Dependabot** for NuGet and GitHub Actions, so LibGit2Sharp (libgit2) security updates arrive as PRs. Proof: `.github/dependabot.yml` exists and Dependabot opens its first update check.
- [x] **CI read-only token:** set `permissions: contents: read` in every workflow. Proof: a green CI run with the restricted permissions. (Already in the CI workflow from T0.3; keep it in any new workflow.)

## Phase 8: Release preparation

The first public release. The 1.0 findings C01 to C18 (except C07, which is T3.7 in Phase 9) and the open decisions D9, D10, and D13 in docs/review-findings.md become tasks here as they are decided.

- [ ] **T2.5 Packfiles.** Research first: how Git stores objects in packfiles (`.git/objects/pack`), which every cloned repository uses, and how libgit2 reads them. Background, measured on 2026-11-18: libgit2 keeps a packfile open once it has read from it, and Windows then refuses to delete it; loose objects are never held open, so a fresh TempRepo cannot show a leaked handle, and no test today proves that a read releases its handle (a `Repository` kept in a field passes them all). Then, tests first: a TempRepo helper that packs the repository's objects, and a test that on a packed repository the folder can be deleted right after a `GitAssert` read, with nothing disposed. A `Repository` kept in a field must turn that test red.
- [ ] **T8.1 Linux CI leg.** Run restore, build, and test on `ubuntu-latest` as well as `windows-latest` (see the spec's Test tooling > Platforms). Fix or skip, with a stated reason, each test that depends on Windows behavior. Proof: a green CI run on both.
- [ ] **T8.2 NuGet packaging** with SourceLink and package signing, plus a usage sample.
- [ ] **T8.3 Branch protection** on main, requiring CI.

## Phase 9: Nice-to-have (after 1.0, when needed)

- [ ] **T3.7 Nested repositories (Maciek investigates first).** An untracked folder that is itself a Git repository, such as `vendor/lib/`, is reported by NoUntrackedFiles as one entry, as `git status` shows it, so the check fails, as it should. But the message calls it a file, and its fix commands do not suit it: `git add` records it as an embedded repository instead of adding its files, and `git clean -f` does not delete it (that needs `-ffd`). Investigate how nested repositories (and submodules) behave, then decide the wording and fix commands, and add tests.
- [ ] **T9.1 NoUnstagedChanges `typechange` kind.** A tracked file replaced by another type, such as a symlink, which `git status` prints as `typechange`. Analysis: F01 in docs/review-findings.md.
- [ ] **T9.2 NoMixedStaging check:** a narrower check than NoUnstagedChanges, its own method, not in the default `RunAll` (D2 in docs/review-findings.md). Its scenarios (staged then edited or deleted) are already NoUnstagedChanges tests.
- [ ] **T9.3 NotDetachedHead check**, including unborn HEAD.
- [ ] **T9.4 OnExpectedBranch check.**
- [ ] **T9.5 NoStaleIndexLock check**, with a real-Git test of a stale `index.lock`.
- [ ] **T9.6 Options:** `RunAll(options)` with per-check on/off, allow-lists, expected branch. Includes the spec's deferred scenarios: allow-listed path passes, disabled checks are skipped.
- [ ] **T9.7 `git am` in NoOperationInProgress.** libgit2 reports `ApplyMailbox` for a `git am` stopped halfway, and `ApplyMailboxOrRebase` when it cannot tell `git am` from the old rebase backend; T3.5 prints both with the generic message. Name `git am`, with `git am --continue` and `git am --abort`, and decide what `ApplyMailboxOrRebase` prints. Tests with real Git.
- [ ] **T9.8 Review real repos** for new scenarios and add them as new tasks.

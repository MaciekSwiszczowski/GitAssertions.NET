# GitAssertions.NET — TDD Task List

Version 1 is a two-day build done with agents: an empty GitHub repo with CI, then six checks and RunAll. Tasks are INVEST (Independent, Negotiable, Valuable, Estimable, Small, Testable), and each feature goes red, green, refactor: failing tests from the spec first, then the simplest code that passes, then cleanup. Phase two items at the end get picked up only when they're needed.

## Definition of done

**Behavior tasks** (Phases 1 to 4, and any task that adds or changes library or test-helper behavior):

- Tests written first and seen failing.
- All tests green locally and on GitHub Actions (once T0.3 exists).
- No leaked Repository handles (temp folders delete cleanly).
- Failure message matches the spec's template for that check, including the copy-pasteable `Fix:` command (checks and RunAll only).

**Infrastructure tasks** (Phase 0, and phase-two items such as branch protection and packaging) have no unit tests. Each one is done when its stated proof is shown, for example the command output, the pushed commit, or the green CI run, and nothing that was green before is now red.

## Phase 0: Repository and build

- [x] **T0.1 Create and seed the GitHub repo.** Repo `GitAssertions.NET` (this repository) with README, .gitignore for .NET, .editorconfig, a license, and docs/specification.md and docs/tasks.md. Proof: the repo exists on GitHub with these files, and the local folder C:\_github\GitAssertions has it as `origin`.
- [x] **T0.2 Solution skeleton.** Solution with `GitAssertions` (class library) and `GitAssertions.Tests` (xUnit or NUnit) projects, plus Directory.Build.props (nullable on, warnings as errors). Proof: `dotnet build` and `dotnet test` pass with one placeholder test.
- [x] **T0.3 GitHub Actions CI.** Workflow that runs restore, build, and test on push and PRs, on windows-latest. Proof: a PR shows a green check.

## Phase 1: Test infrastructure

- [x] **T1.1 TempRepo fixture.** Test helper that creates a temp folder, runs `Repository.Init`, makes an initial commit, and deletes everything on dispose. Tests first: the folder exists during the test and is gone afterward.
- [x] **T1.2 Scenario helpers.** Fluent helpers on TempRepo: AddFile, Stage, Commit, Delete, CreateBranch, Checkout, AddGitIgnore. Tests first: each helper produces the expected `RetrieveStatus` result. (Helpers for merge, rebase, cherry-pick, and revert are added with the check that needs them, T3.4 and T3.5.)

## Phase 2: Core API

- [ ] **T2.1 GitAssert.Open and Dispose.** Opens a repo path, implements IDisposable, and throws a clear error for a path that isn't a repo. Tests first: open works, a bad path throws, and the folder can be deleted after dispose.
- [ ] **T2.2 CheckResult.** `Rule`, `IsHealthy`, `Message`, `Files`, and a factory for healthy and failing results. Tests first: the message follows the spec's common format (rule line, entries, 20-entry limit, quoting, `Fix:` command); a healthy result is `<Rule>: OK.` with no files.
- [ ] **T2.3 Shell-safe paths and output limits.** Message formatting follows the spec's Security > Shell-safe paths and Output limits. Tests first:
  - A path with `$(...)`, a backtick, `;`, `&`, `|`, `%`, `"`, `'`, `*`, or a leading `:` is listed as an entry but left out of every command, and appears in the unsafe-path note.
  - Newline, tab, carriage return, ESC, and a bidirectional override in a path are printed escaped (`\n`, `\t`, `\r`, `\u{001B}`, `\u{202E}`), so each entry stays on one line; a literal backslash prints as `\\`.
  - When no listed path is safe, the message has no command lines, only the note.
  - A path with a space or a non-ASCII letter is double-quoted in commands; a plain path is not quoted.
  - A 300-character path is shortened in entries (first 100 + `...` + last 60) and left out of commands.
  - A message that would exceed 16,000 characters is cut at a full line and ends with `  ...message truncated.`
  - `Files` still holds the raw, untruncated paths.
- [ ] **T2.4 Hostile-repo safety.** Prove the library never runs anything the repository asks for. Tests first: in a TempRepo with an executable `pre-commit` and `post-checkout` hook and a `.gitattributes` filter driver whose clean/smudge/process commands would create a marker file, opening the repo and running every v1 check never creates the marker. Also assert the library leaves libgit2 owner validation switched on. Add the "never runs hooks, filters, or fsmonitor" statement to the README.

## Phase 3: Version 1 checks (one task each, any order after Phase 2)

Each task: write the spec's v1 test scenarios as failing tests (skip the ones marked *phase two* in the spec), with at least one test asserting the full failure message against the spec's template and example, implement the check, refactor, and update the README. All scenarios are set up with LibGit2Sharp through TempRepo.

- [ ] **T3.1 NoUntrackedFiles**, including nested folders and ignored files.
- [ ] **T3.2 NoUnstagedChanges**, including deletes and the autocrlf and file-mode edge cases.
- [ ] **T3.3 NoStagedUncommitted** (no staged changes relative to `HEAD`), including staged deletes and renames.
- [ ] **T3.4 NoConflicts**, including merge conflicts and delete/modify conflicts. Adds a merge helper to TempRepo.
- [ ] **T3.5 NoOperationInProgress**, covering merge, rebase, cherry-pick, revert, and the aborted case. Adds rebase, cherry-pick, and revert helpers to TempRepo.
- [ ] **T3.6 NoTrackedIgnoredFiles**, covering files committed before the ignore rule, force-added files, bin/obj grouping, and negated rules.

## Phase 4: RunAll

- [ ] **T4.1 RunAll.** `git.RunAll()` (no options in v1) runs all six checks and returns a `RunAllResult` as defined in the spec: `Results`, `Failures`, `IsHealthy`, and `Message`. Tests first: two broken rules produce two `Failures` and a `Message` matching the spec's RunAll template (header plus two blocks); a clean repo is healthy with six healthy `Results`.

## Phase two (later, when needed)

- [ ] Branch protection on main, requiring CI.
- [ ] NoMixedStaging check.
- [ ] NotDetachedHead check, including unborn HEAD.
- [ ] OnExpectedBranch check.
- [ ] NoStaleIndexLock check.
- [ ] Options: `RunAll(options)` with per-check on/off, allow-lists, expected branch. Includes the spec's deferred scenarios: allow-listed path passes, disabled checks are skipped.
- [ ] CliWrap git runner and real-Git integration tests (autocrlf, `git add -f`, files committed before an ignore rule, merge or rebase in progress, index.lock).
- [ ] NuGet packaging with SourceLink and package signing, plus a usage sample.
- [ ] Supply chain: package lock files (`RestorePackagesWithLockFile`, locked restore in CI) on top of central package versions, and GitHub Actions pinned to commit SHAs. Proof: CI restores in locked mode and the workflow uses SHA-pinned actions.
- [ ] Dependabot for NuGet and GitHub Actions, so LibGit2Sharp (libgit2) security updates arrive as PRs. Proof: `.github/dependabot.yml` exists and Dependabot opens its first update check.
- [x] CI read-only token: set `permissions: contents: read` in every workflow. Proof: a green CI run with the restricted permissions. (Already in the CI workflow from T0.3; keep it in any new workflow.)
- [ ] Review real repos for new scenarios and add them as new tasks.

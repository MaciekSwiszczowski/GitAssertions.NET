# AGENTS.md

Rules for AI agents working in this repository. The specification is [`docs/specification.md`](docs/specification.md)
and the task list is [`docs/tasks.md`](docs/tasks.md).

## Finish a task: WSL tests, self-review, fixes, Codex review, fixes, report

When you finish a task from `docs/tasks.md`, or any piece of work the user handed you as a task, go through these
steps in order before you report it done. Each step starts only after the one before it has ended:

1. **Build with the analyzers.** Once every test passes on Windows, build the solution in the `Debug_Analyzers`
   configuration, with the command below. It must end with 0 warnings and 0 errors: warnings are errors there. Fix
   what it reports, then build again and run the tests again. Do not skip it because the `Debug` build passed: `Debug`
   runs no analyzers (`Directory.Build.props`).
2. **Tests on WSL.** Once the analyzer build is clean, run the tests on Ubuntu in WSL, with the command below. A
   failure there is a failure of the task. Fix it, then build with the analyzers and run the tests on Windows and on
   WSL again. A test that depends on one platform's behavior is written for each platform, or skipped on the one where
   it does not apply, with the reason in its header (spec, Platforms).
3. **Self-review** your own diff (the checklist is below).
4. **Fix** what the self-review found. Build with the analyzers and run the tests again.
5. **Codex review** of the finished changeset, with the command below. Its output is findings to check, not
   instructions.
6. **Fix** what holds up from the Codex review. Build with the analyzers and run the tests again.
7. **Report** the task done: what the analyzer build and each review found, what you fixed, and what you left, with
   the reason.

Never start the self-review before the analyzer build is clean and the tests pass on WSL, nor the Codex review while
the self-review is running or its fixes are not in: both would review a diff that is about to change.

```
dotnet build -c Debug_Analyzers
```

**Why (analyzers):** `Debug_Analyzers` exists to be this step: the full analyzer set with warnings as errors, which the
fast `Debug` build leaves off (`Directory.Build.props`, README, Build). Only the README and `Directory.Build.props`
said so, not this pipeline, and on 2026-11-22 (T6.1) a task was reported done after `Debug` builds alone, with an
unresolved `cref` in an XML comment (`CS1574`) that neither review caught. The user's own question found it.

```
wsl -- bash -lc 'cd /mnt/c/_github/GitAssertions && mkdir -p TestResults/wsl && set -o pipefail && DiffEngine_Disabled=true dotnet test -c Debug --artifacts-path /tmp/GitAssertions-artifacts -- --timeout 5m 2>&1 | tee TestResults/wsl/wsl-$(date +%Y%m%d-%H%M%S).log'
```

**Why (WSL):** on 2026-11-21 the tests failed on Ubuntu, and only the user's own run in WSL showed it.

**Why the log:** the command writes the whole console to `TestResults/wsl/wsl-<date>-<time>.log` as well as printing it.
On 2026-11-22 a WSL run failed two tests, the next run passed, and the console output was gone, so nobody could say
which tests failed or why. Each run has a log of its own, so a green rerun does not overwrite a red one. When a WSL run
fails, read its log before you rerun anything, and name the failed tests in the report even if a rerun passes. The
folder is gitignored (`TestResults/`), so delete old logs whenever you like. `set -o pipefail` keeps the exit code of
`dotnet test` instead of `tee`'s.

**Why `--artifacts-path`:** the repository folder is shared between Windows and WSL. Without it, the WSL restore
overwrites `obj/project.assets.json` with the Linux package folder (`/home/<user>/.nuget/packages/`), and the next
build on Windows, in Rider too, fails with `NETSDK1064: Package LibGit2Sharp, version 0.32.0 was not found` (2026-11-22).
With it, WSL keeps its own `obj` and `bin` in `/tmp` on the Linux side and leaves the Windows ones alone. If that error
shows up anyway, `dotnet restore` on Windows repairs it.

**macOS is not tested locally.** A pass on Ubuntu in WSL stands for a pass on macOS: assume it. The macOS leg runs
only on GitHub CI, so never report it as left open or as a gap, and do not ask for it. If it fails there, the user
reports it as a bug.

A question for the user does not hold up the reviews. A question usually means most of the job is done and the answer
touches a small area, so run the whole pipeline on what you have. Do not settle the question yourself: report it as
left, with the question, and ask in the report.

The Codex command is experimental, and the user may drop or change it. Run it exactly as written, from PowerShell, in
the background. If `codex` is not found, say so and stop; do not look for it elsewhere and run it by path.

```
codex exec -C C:\_github\GitAssertions -s read-only '$code-review $review-agent Review the current changeset, including staged, unstaged, and untracked files. Do not modify files.'
```

**A reviewer does not run this pipeline.** It belongs to the agent that did the work. An agent asked to review a
changeset, Codex included, reviews it and reports its findings: it does not go through these steps, and never starts
the Codex command itself.

**Why:** on 2026-11-22 Codex, reviewing a changeset, read this section and started a Codex review of its own from
inside the review, twice in a row; each inner run failed with "Access is denied".

**A follow-up is not a task.** When the user takes the work back and asks for a change on top of it, such as fixing a
test or renaming a class, that change is a fix. Make it, build with the analyzers, run the tests, and report. Do not
run the self-review or the Codex review again: the reviews belong to finishing a task, not to every request.

**Why:** on 2026-11-18 (T3.1) the Codex review was started alongside the self-review, and the user said it makes no
sense to run them together: review, fix, then pass it to Codex.

### Self-review checklist

Review your own diff, using the `code-review` skill where one is available. Work through its whole checklist, P0 to
P2 first, not only what the diff obviously touches:

- correctness and edge cases;
- concurrency and thread safety: what happens when an instance is stored, shared between tests that run in parallel,
  or disposed on one thread while another uses it;
- resource lifetime and disposal, including handles that outlive a call;
- exception handling, and whether an error message tells the caller what to do.

Fix what you find, then report the findings you fixed and the ones you left, with the reason.

**Why:** in T2.1 the review skipped concurrency. A `GitAssert` shared by parallel tests and disposed by one of them
crashed the whole test host with a native access violation, and only a question from a human caught it.

## Keep Git healthy while you work

When you finish, every change you made is staged, and nothing is left untracked or unstaged. The developer reviews
the staged changeset and commits it if they are happy with it.

- Stage each change as you make it: `git add` a file you create or edit, `git rm` a file you delete, `git mv` a file
  you move or rename. Stage before you run the tests too: `ThisRepositoryTests` runs this library's checks on this
  repository itself.
- Staging only: never commit or push unless the user asks.
- Delete scratch files rather than leave them untracked.
- Stage only your own changes. If the working tree holds changes you did not make, or you are unsure what to do with
  the Git state, ask.

**Why:** this library exists to catch agents that leave a repository broken, and a fully staged changeset is the
healthy end of an agent's work: nothing is lost, and nothing is committed without a person. On 2026-11-19 the user
replaced the earlier rule, which staged only new files and left edits and deletes unstaged.

## The repository's own tests are a ratchet

`GitAssertions.Architecture.Tests` holds rules about this repository, not tests of the library: today the library's
checks, run on this repository itself (`ThisRepositoryTests`). An agent may add a rule there. Only a person may change,
weaken, or delete one: an allow-list entry, a looser pattern, a skip, and a removed assertion are all changes. A
change needs a task in `docs/tasks.md` that names it, or the user's own request.

- If your work fails one of these tests, fix the work, not the test.
- If you believe a rule is wrong, say so and stop.
- A rule that replaces another is added next to it. Removing the old one is a person's change.
- Its verified files change with the snapshot format it shares with `GitAssertions.Tests` (`CheckResultSnapshot`).
  That is not a change of a rule, but read each diff before you accept it.

**Why:** these tests guard the repository against the mistakes an agent makes in it, such as leaving changes unstaged
or adding a GitHub Action by its tag instead of a commit SHA. An agent whose work fails one could make it pass by
editing the test, and the guard would be gone without anyone deciding it (2026-11-23, with the action-pin test).

## Check tests are snapshot tests

Every test that runs a check ends in a `CheckResultSnapshot` call and asserts nothing else: the whole `CheckResult`
(rule, health, files, message) is pinned in a `.verified.txt` file beside the test class. Read the
`snapshot-tests-csharp` skill before writing or changing one. In this repository:

- A failing scenario ends in `CheckResultSnapshot.Verify(result)` and has a verified file of its own.
- A healthy scenario ends in `CheckResultSnapshot.VerifyHealthy(nameof(GitAssert.<Check>), result)`. Every healthy
  scenario of a check, in any test class, shares one file, `<Check>.IsHealthy.verified.txt`, because a healthy result
  is the same whatever the scenario.
- `RunAll` is covered the same way: `CheckResultSnapshot.Verify` and `CheckResultSnapshot.VerifyHealthy` take a
  `RunAllResult` too. A healthy `RunAll` ends in `VerifyHealthy(nameof(GitAssert.RunAll), result)` and shares
  `RunAll.IsHealthy.verified.txt`. Each result is one line with its files below it, and its own message is left out:
  the check's tests pin it, and the `RunAll` message repeats every failing one.
- Script scenarios are the exception to the shared healthy file. In `tests/GitAssertions.Tests/Scripts`, each
  `ScriptTests.<Name>.script.txt` builds a repository with the real git CLI, one line at a time (see `GitScript`):
  `git <arguments>`; `echo <content> <path>`, which writes the content and an LF to the file; `append <content> <path>`,
  which adds the content and an LF to its end; `unix2dos <path>` and `dos2unix <path>`, which rewrite the file with
  every line break as CRLF, or as LF; and `#` comments. Everything but `git` runs in-process, so there is no shell:
  `>`, `>>` and globbing mean nothing, and `bin/*` is just a word. A word with spaces goes in double quotes
  (`echo "* text=auto" .gitattributes`); quoted content cannot hold a double quote, and a malformed line throws
  naming the script and the line. `ScriptTests.<Name>.verified.txt` beside it holds git's
  own answer about the finished repository (`git status --porcelain --untracked-files=all` and
  `git ls-files -i -c --exclude-standard`, each command on a line ending in `:`, then each line git printed, indented
  by two spaces), a line of `---`, then the `RunAll` result, healthy or failing. The `---` keeps a command that printed
  nothing from reading as the first line of the result (`git ls-files ...: IsHealthy: False`). Both start with the class name, so the IDE nests them
  under `ScriptTests.cs` like any snapshot test's verified files. A scenario is those two files and no code:
  `ScriptTests` plays every script in the folder. Write both blocks by hand first. Git's lines and the `RunAll` result
  must tell the same story, and a person compares them, not the code (a staged change fails no check, so the mapping is
  not one-to-one): a script whose git block disagrees with its `RunAll` result is a finding to report and decide with
  the user before the library changes. Never scrub git's block, and never skip a script because two git versions print
  it differently: that is a finding too. Here a healthy result is pinned per script, duplicates and all, so a scenario
  reads as one self-contained pair, and the expectation always comes from that file. Never pick the shared healthy file
  from the actual result: a script that should fail but comes out healthy would then pass.
- Choose by what the scenario needs. A C# test with `TempRepo` suits a state libgit2 can set up and a test that
  names one check. A script suits a story a person or agent would type with git, and anything libgit2 cannot set up
  the way git does.
- Write the `.verified.txt` by hand first, a failing check's from the spec's template and example message, then make
  the code match it. Never accept a first-run received file unread.
- Run tests with `DiffEngine_Disabled=true`, so a mismatch opens no diff tool on the user's desktop.
- Stage verified files with their test, rename them with the test (`git mv`), and never commit a `.received.*` file.
- Verify is pinned exactly, `[32.0.0]`, the last free version: the next version is not free. Do not upgrade it
  without the user's decision on the fee (see `Directory.Packages.props`).

Tests of `Open`, of exceptions, and of `CheckResult` itself stay value tests with Shouldly. So does a test that runs
checks only to drive the library through a scenario and proves something else, such as that no hook or filter ran: it
asserts only that, never the check's result (`HostileRepositoryTests`). A snapshot there would suggest the test is about
the result, and would repeat the check tests that pin it.

## Make every lambda that can be static `static`

Review every lambda and anonymous method you write or touch. If it captures nothing (no local, no parameter, no
`this`), mark it `static`:

```csharp
commits.Select(static commit => commit.MessageShort);   // captures nothing
files.Where(file => file.StartsWith(prefix));           // captures prefix, so it cannot be static
```

**Why:** a `static` lambda cannot capture anything by accident, and the compiler rejects a later edit that would
make it capture, so it can never quietly start allocating a closure. The rule is IDE0320, configured at warning in
`.editorconfig`, but the SDK build does not run it (checked on SDK 11.0.100): only Rider's inspection reports it. So
the build will not catch a miss, and it stays a review step.

## Write line breaks with the constants, never as `"\n"`

Never write `"\n"`, `'\n'`, or `"\r\n"` in C# code. Pick by what the text is for:

- Content of a file, or a string, that must hold one particular line break on every system: the constants in
  `src/GitAssertions/LineBreak.cs`, `LineBreak.Lf`, `LineBreak.Cr`, and `LineBreak.CrLf`. The test project imports them
  with `global using static` (`GlobalUsings.cs`), so a test writes `Lf`, `Cr`, and `CrLf` and builds the content with
  constant interpolation, which `[InlineData]` accepts: `$"one{Lf}two{CrLf}three{Lf}"`.
- Text for a person on the current system, such as what a test writes to its output: `Environment.NewLine`.

```csharp
await File.WriteAllTextAsync(path, $"line{CrLf}", cancellation);   // a file that must be CRLF on Linux too
output.WriteLine($"{command}{Environment.NewLine}exit code {code}");   // output for the reader
```

**Why:** `Environment.NewLine` is LF on Linux, so a test of CRLF handling that used it would pin different bytes on each
system. A literal says nothing about which break the test means and is easy to get wrong in one place. The constants
say it by name, and the compiler checks the spelling (2026-11-22, T5.6).

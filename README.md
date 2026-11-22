# GitAssertions.NET

A small .NET library of xUnit-friendly checks that fail when an AI agent (or anyone) leaves a Git repository in a broken state: untracked files, unstaged changes, merge conflicts, an unfinished merge or rebase, or tracked files that should be ignored.

- Namespace: `GitAssertions`
- Read-only: it only inspects the repository and never changes it.
- Every failure message lists the offending paths and ends with a ready-to-paste `Fix:` command.
- Git access is powered internally by LibGit2Sharp; the public API exposes only the library's own types.

See [docs/specification.md](docs/specification.md) for the design and [docs/tasks.md](docs/tasks.md) for the task list.

## Usage

```csharp
using GitAssertions;

GitAssert git = GitAssert.Open(repoPath);   // the folder that contains .git

CheckResult result = git.NoUntrackedFiles();
Assert.True(result.IsHealthy, result.Message);

RunAllResult all = git.RunAll();            // every check below, in one call
Assert.True(all.IsHealthy, all.Message);
```

`GitAssert` keeps only the path, so there is nothing to dispose, and one instance can be shared between tests. Each check opens the repository, reads it, and closes it before it returns.

## Checks

| Check | Fails when | Passes for |
|---|---|---|
| `NoUntrackedFiles` | a file in the working tree is neither tracked nor ignored. Every such file is listed by its full path, also inside a new folder. | ignored files, empty folders, edits and deletes of tracked files, staged files |
| `NoUnstagedChanges` | a tracked file differs from the index: it was edited or deleted and the change is not staged. A file moved without `git mv` is listed as its old path, deleted; its new path is untracked. | untracked files, staged changes, line endings and the executable bit where Git ignores them (see below) |
| `NoConflicts` | a file still has an unresolved conflict from a merge, pull, rebase, cherry-pick, revert, or stash pop: Git keeps its conflicting versions, and refuses to commit, until the file is resolved and staged with `git add`. Binary files and files one side deleted are included, although they hold no conflict markers. | conflicts resolved and staged, even when conflict markers were left in the file (see below) |
| `NoOperationInProgress` | a merge, rebase, cherry-pick, or revert (of one commit or several) is stopped halfway, usually on a conflict, and was neither finished with `--continue` nor given up with `--abort`. The message names the operation and both commands, and lists no file. A bisect left open fails too, and `git bisect reset` ends it. Any other operation libgit2 reports, such as `git am`, gets a generic message that names it as libgit2 does and points to `git status`. | a repository with no operation in progress |
| `NoTrackedIgnoredFiles` | a tracked file matches an ignore rule from a `.gitignore` file, `.git/info/exclude`, or `core.excludesFile`: a rule hides only untracked files, so a file committed before its rule, or forced in with `git add -f`, stays tracked. A folder that is itself ignored, with every tracked file in it ignored, such as `bin/`, is one entry with its file count; any other file is listed on its own. | untracked ignored files, tracked files no rule matches, files a `!` rule brings back |

`RunAll()` runs the five checks above one after another and returns a `RunAllResult`:

- `Results`: one `CheckResult` per check, healthy ones included, in the order of the table.
- `Failures`: the results that failed. `IsHealthy` is true only when there are none.
- `Message`: `All Git checks passed.`, or `<f> of 5 Git checks failed.` followed by each failing check's own message, unchanged, so one run names every broken rule.

A failing check does not stop `RunAll`. A check that throws does: `RunAll` throws that check's exception, and the checks after it do not run, because a repository one check cannot read usually fails the others too. Each check opens and closes the repository itself, also when `RunAll` calls it.

`NoUnstagedChanges` reads line endings and the executable bit as `git status` does, from the `core.autocrlf` and `core.filemode` settings:

- With `core.autocrlf=true`, a file Git checked out with CRLF line endings and saved again unchanged is not a change. A file rewritten with only its line endings changed, CRLF to LF, is listed as modified, as `git status` lists it, although `git diff` shows nothing; `git add` clears it and stages nothing.
- With `core.filemode=false`, which Git for Windows sets in every repository it creates or clones, a file committed as executable is not a change on a file system that has no executable bit.

`NoUnstagedChanges` does not support these yet:

- A tracked file replaced by another type, such as a symlink, which `git status` prints as `typechange`: it is not reported.
- A file recorded with `git add -N` (intent to add), which `git status` prints as ` A`: it is reported as `modified`, and the `git restore` line offered for it would empty the file, because Git holds it with no content. Use `git add` on it.

`NoConflicts` reads only what Git records: a conflict counts as resolved once `git add` has staged the file. It does not search files for conflict markers, so a file staged with `<<<<<<<` still in it passes. Markers left in code usually break the build, but not in a file nothing compiles or reads, such as Markdown.

## Build

Three configurations:

- `Debug`: fast inner loop, no analyzers.
- `Debug_Analyzers`: full analyzer set (NET analyzers, Meziantou, Roslynator, VS-Threading, ErrorProne, IDisposableAnalyzers, AsyncFixer), warnings as errors. Work is not done until this builds clean.
- `Release`: optimised, analyzers off.

```
dotnet build -c Debug_Analyzers
dotnet test
```

The check tests are [Verify](https://github.com/VerifyTests/Verify) snapshot tests: each one's whole `CheckResult` sits in a `.verified.txt` file beside its test class, and every healthy scenario of a check shares one, `<Check>.IsHealthy.verified.txt`. A failing test writes a `.received.txt` next to it. Read the difference, and copy it over the verified file only when the change is intended. Verify is pinned exactly to 32.0.0, its last free version; see `Directory.Packages.props`.

Integration tests run the real `git` CLI to set up what libgit2 cannot (`TempRepo.GitAsync`), cut off from your Git config like the rest. They are tagged `Category=Integration`, run with every other test, and need git 2.32 or later on `PATH`: a missing or older git fails them, with a message that says to install or update it, and never skips them.

Two test projects: `GitAssertions.Tests` builds throwaway repositories and cuts libgit2 off from your Git config, so its results repeat on every machine. `GitAssertions.ThisRepository.Tests` runs the checks on this repository itself and reads your Git config as `git status` does. Stage your changes before you run it: it fails on an untracked file or an unstaged change.

## Status

Early development. Not yet published to NuGet.

Fix commands do not yet leave out file names with shell characters such as `$(...)` (task T2.3): such a name is only quoted, which does not stop a shell from running it. Read a `Fix:` command before you run it. Until then, a control character in a file name, such as a newline or ESC, is also printed as it is, so it can break a message across lines or affect your terminal.

## License

[MIT](LICENSE)

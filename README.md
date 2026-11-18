# GitAssertions.NET

A small .NET library of xUnit-friendly checks that fail when an AI agent (or anyone) leaves a Git repository in a broken state: untracked files, unstaged or staged-but-uncommitted changes, merge conflicts, an unfinished merge or rebase, or tracked files that should be ignored.

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
```

`GitAssert` keeps only the path, so there is nothing to dispose, and one instance can be shared between tests. Each check opens the repository, reads it, and closes it before it returns.

## Checks

| Check | Fails when | Passes for |
|---|---|---|
| `NoUntrackedFiles` | a file in the working tree is neither tracked nor ignored. Every such file is listed by its full path, also inside a new folder. | ignored files, empty folders, edits and deletes of tracked files, staged files |

The other v1 checks are added one by one; see Phase 3 in [docs/tasks.md](docs/tasks.md).

## Build

Three configurations:

- `Debug`: fast inner loop, no analyzers.
- `Debug_Analyzers`: full analyzer set (NET analyzers, Meziantou, Roslynator, VS-Threading, ErrorProne, IDisposableAnalyzers, AsyncFixer), warnings as errors. Work is not done until this builds clean.
- `Release`: optimised, analyzers off.

```
dotnet build -c Debug_Analyzers
dotnet test
```

## Status

Early development. Not yet published to NuGet.

Fix commands do not yet leave out file names with shell characters such as `$(...)` (task T2.3): such a name is only quoted, which does not stop a shell from running it. Read a `Fix:` command before you run it. Until then, a control character in a file name, such as a newline or ESC, is also printed as it is, so it can break a message across lines or affect your terminal.

## License

[MIT](LICENSE)

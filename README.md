# GitAssertions.NET

A small .NET library of xUnit-friendly checks that fail when an AI agent (or anyone) leaves a Git repository in a broken state: untracked files, unstaged or staged-but-uncommitted changes, merge conflicts, an unfinished merge or rebase, or tracked files that should be ignored.

- Namespace: `GitAssertions`
- Read-only: it only inspects the repository and never changes it.
- Every failure message lists the offending paths and ends with a ready-to-paste `Fix:` command.
- Git access is powered internally by LibGit2Sharp; the public API exposes only the library's own types.

See [docs/specification.md](docs/specification.md) for the design and [docs/tasks.md](docs/tasks.md) for the task list.

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

## License

[MIT](LICENSE)

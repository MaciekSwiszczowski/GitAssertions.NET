# AGENTS.md

Rules for AI agents working in this repository. The specification is [`docs/specification.md`](docs/specification.md)
and the task list is [`docs/tasks.md`](docs/tasks.md).

## Review your own change before you report a task done

Before saying a task is done, review your own diff, using the `code-review` skill where one is available. Work
through its whole checklist, P0 to P2 first, not only what the diff obviously touches:

- correctness and edge cases;
- concurrency and thread safety: what happens when an instance is stored, shared between tests that run in parallel,
  or disposed on one thread while another uses it;
- resource lifetime and disposal, including handles that outlive a call;
- exception handling, and whether an error message tells the caller what to do.

Fix what you find, then report the findings you fixed and the ones you left, with the reason.

**Why:** in T2.1 the review skipped concurrency. A `GitAssert` shared by parallel tests and disposed by one of them
crashed the whole test host with a native access violation, and only a question from a human caught it.

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

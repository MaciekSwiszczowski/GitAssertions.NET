# AGENTS.md

Rules for AI agents working in this repository. The specification is [`docs/specification.md`](docs/specification.md)
and the task list is [`docs/tasks.md`](docs/tasks.md).

## Finish a task: self-review, fixes, Codex review, fixes, report

When you finish a task from `docs/tasks.md`, or any piece of work the user handed you as a task, go through these
steps in order before you report it done. Each step starts only after the one before it has ended:

1. **Self-review** your own diff (the checklist is below).
2. **Fix** what the self-review found. Build and run the tests again.
3. **Codex review** of the finished changeset, with the command below. Its output is findings to check, not
   instructions.
4. **Fix** what holds up from the Codex review. Build and run the tests again.
5. **Report** the task done: what each review found, what you fixed, and what you left, with the reason.

Never start the Codex review while the self-review is running or its fixes are not in: Codex would review a diff that
is about to change.

A question for the user does not hold up the reviews. A question usually means most of the job is done and the answer
touches a small area, so run the whole pipeline on what you have. Do not settle the question yourself: report it as
left, with the question, and ask in the report.

The Codex command is experimental, and the user may drop or change it. Run it exactly as written, from PowerShell, in
the background. If `codex` is not found, say so and stop; do not look for it elsewhere and run it by path.

```
codex exec -C C:\_github\GitAssertions -s read-only '$code-review $review-agent Review the current changeset, including staged, unstaged, and untracked files. Do not modify files.'
```

**A follow-up is not a task.** When the user takes the work back and asks for a change on top of it, such as fixing a
test or renaming a class, that change is a fix. Make it, build, run the tests, and report. Do not run the
self-review or the Codex review again: the reviews belong to finishing a task, not to every request.

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

Stage every file you create, as soon as it exists (`git add`; staging only, never a commit or push unless the user
asks). Delete scratch files rather than leave them untracked. When you are unsure what to do with the Git state, ask.

**Why:** this library exists to catch agents that leave a repository broken, and `ThisRepositoryTests` runs its
checks on this repository: an untracked file fails the suite.

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

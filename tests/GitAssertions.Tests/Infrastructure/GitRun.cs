namespace GitAssertions.Tests.Infrastructure;

/// <summary>How one git call ended: its exit code and what it wrote to standard output and standard error.</summary>
internal sealed record GitRun(int ExitCode, string StandardOutput, string StandardError);

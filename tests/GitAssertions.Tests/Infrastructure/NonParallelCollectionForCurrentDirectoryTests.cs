namespace GitAssertions.Tests.Infrastructure;

/// <summary>
/// An xUnit test collection definition: test classes that join it run on their own, never at the same time as any
/// other test. It holds no code; the attribute below is all it is. Join it only from a test that changes the
/// process's current directory, such as <see cref="GitAssertCurrentDirectoryTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// Why it is needed: xUnit runs every test of this assembly in parallel (<c>AssemblyParallelization.cs</c>), on threads
/// of one process, but the current directory belongs to the whole process, not to one thread. While a test has the
/// current directory pointed somewhere else, every other test running at that moment resolves its relative paths
/// against that folder instead, and can fail or read the wrong repository for a reason that has nothing to do with
/// what it tests. Two such tests running together would also overwrite each other's setting.
/// </para>
/// <para>
/// How it works: a test class joins with <c>[Collection(NonParallelCollectionForCurrentDirectoryTests.Name)]</c>.
/// <c>DisableParallelization = true</c> guarantees that a test of this collection does not run in parallel against any
/// other test, so the collection also runs one test at a time. The xUnit docs promise nothing about when it runs
/// relative to the others, so a test must not depend on that: https://xunit.net/docs/running-tests-in-parallel
/// </para>
/// <para>
/// A test class that joins it must still put the current directory back in its <c>Dispose</c>, so the tests that
/// run after it start from the original folder.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelCollectionForCurrentDirectoryTests
{
    /// <summary>The collection's name, which is also how a test runner displays it.</summary>
    public const string Name = "Tests that change the current directory (run alone)";
}

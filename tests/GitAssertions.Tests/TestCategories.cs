namespace GitAssertions.Tests;

/// <summary>
/// The names tests are tagged with, so a run can pick them by trait, for example
/// <c>dotnet test -- --filter-trait "Category=Integration"</c>.
/// </summary>
internal static class TestCategories
{
    /// <summary>
    /// A test that runs the real git CLI (docs/tasks.md, Phase 5). It is tagged, never skipped: a machine without a
    /// usable git fails these tests, so the layer cannot drop out of a run unnoticed.
    /// </summary>
    public const string Integration = "Integration";
}

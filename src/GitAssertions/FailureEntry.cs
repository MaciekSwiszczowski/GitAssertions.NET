using System.Globalization;

namespace GitAssertions;

/// <summary>
/// One entry of a failure message, as a check reports it: the path it is about, with the kind of change in front
/// when the check reports one. <see cref="FailureMessage"/> prints it as <c>kind: path</c>, or as the bare path
/// without a kind, a rename as <c>renamed: old -> new</c>, and a folder as <c>folder/ (n files)</c>. Every path of an entry also goes into the fix commands
/// that take paths, so a rename puts both of its paths there.
/// </summary>
internal sealed class FailureEntry
{
    private FailureEntry(string? kind, IReadOnlyList<string> paths, string? label = null)
    {
        Kind = kind;
        Paths = paths;
        Label = label;
    }

    /// <summary>The kind of change, such as <c>modified</c>, or null for a check that reports only one kind.</summary>
    public string? Kind { get; }

    /// <summary>The paths the entry names, relative to the repository root, in the order they are printed.</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>
    /// The text printed in place of <see cref="Paths"/> for an entry that stands for several files, such as
    /// <c>bin/ (12 files)</c>; null prints the paths. The commands still take <see cref="Paths"/>.
    /// </summary>
    public string? Label { get; }

    /// <summary>An entry that is only a path, for a check with one kind of finding (an untracked file).</summary>
    public static FailureEntry Path(string path)
    {
        return new FailureEntry(kind: null, [path]);
    }

    /// <summary>A path changed in the way <paramref name="kind"/> names: <c>modified: readme.txt</c>.</summary>
    public static FailureEntry Change(string kind, string path)
    {
        return new FailureEntry(kind, [path]);
    }

    /// <summary>
    /// A folder that stands for <paramref name="fileCount"/> files, printed <c>bin/ (12 files)</c> or
    /// <c>bin/ (1 file)</c>. <paramref name="folder"/> ends with a slash, and is the only path the commands take, so
    /// one command untracks everything the label counts.
    /// </summary>
    public static FailureEntry Folder(string folder, int fileCount)
    {
        string files = fileCount == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{fileCount} files");
        return new FailureEntry(kind: null, [folder], $"{folder} ({files})");
    }

    /// <summary>A rename, printed <c>renamed: old -> new</c>; both paths go into the path commands.</summary>
    public static FailureEntry Rename(string oldPath, string newPath)
    {
        return new FailureEntry("renamed", [oldPath, newPath]);
    }
}

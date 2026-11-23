namespace GitAssertions;

/// <summary>
/// A network path that a file of a repository names, as <see cref="NetworkPathGuard"/> finds it: the file, the key it
/// is the value of, and the value. Each holds the text as the repository gave it; <see cref="ToMessage"/> escapes and
/// shortens it, because a repository may be hostile and these are its words.
/// </summary>
/// <param name="File">The full path of the file that names it.</param>
/// <param name="Key">
/// The variable of a config file, as <see cref="GitConfigEntry.Name"/> gives it, or <c>gitdir</c> for the <c>.git</c>
/// file's line, <c>commondir</c> for that file's, and <c>alternate</c> for a line of an alternates file.
/// </param>
/// <param name="Value">The network path, as read after git's quoting and escapes.</param>
internal sealed record NetworkPathReference(string File, string Key, string Value)
{
    /// <summary>
    /// The message of the <see cref="ArgumentException"/> <see cref="GitAssert.Open"/> throws: which repository is
    /// refused, where the network path is named, why that refuses the repository, and what to do. One line, with no
    /// line break, so a hostile value cannot add a line to a log. The folder's own path is escaped too: on Unix a
    /// folder name can hold a line break or a terminal control character, and a repository the caller was handed in a
    /// folder like that is exactly the one that gets refused.
    /// </summary>
    public string ToMessage(string repoPath)
    {
        string repository = FailureMessage.DisplayInMessage(repoPath);
        string file = FailureMessage.DisplayInMessage(File);
        string key = FailureMessage.DisplayInMessage(Key);
        string value = FailureMessage.DisplayInMessage(Value);

        return $"\"{repository}\" is refused: {file} names a network path in {key}: {value}. Opening it could send "
               + "your credentials to that server. If the repository came from somewhere you do not trust, remove "
               + "the setting.";
    }
}

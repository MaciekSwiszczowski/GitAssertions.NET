namespace GitAssertions;

/// <summary>
/// One variable of a git config file, as <see cref="GitConfigReader"/> reads it.
/// </summary>
/// <param name="Name">
/// The full name, as <c>git config --list</c> prints it: the section, the subsection if there is one, and the
/// variable, joined by dots. The section and the variable are in lower case, because git compares them ignoring case;
/// the subsection keeps its case, because git does not. So <c>[includeIf "gitdir:~/Work/"]</c> with <c>Path = x</c>
/// is <c>includeif.gitdir:~/Work/.path</c>.
/// </param>
/// <param name="Value">
/// The value after git's quoting, escapes, line continuations, and comments are applied; null for a variable written
/// with no <c>=</c>, which git reads as true.
/// </param>
internal sealed record GitConfigEntry(string Name, string? Value);

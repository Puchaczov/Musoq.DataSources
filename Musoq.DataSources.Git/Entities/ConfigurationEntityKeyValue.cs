using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a key-value pair in a Git configuration.
/// </summary>
/// <param name="configurationEntry">The configuration entry object.</param>
public class ConfigurationEntityKeyValue(ConfigurationEntry<string> configurationEntry, Repository repository)
{
    internal readonly Repository LibGitRepository = repository;

    /// <summary>
    ///     Gets the key of the configuration entry.
    /// </summary>
    public string Key => configurationEntry.Key;

    /// <summary>
    ///     Gets the value of the configuration entry.
    /// </summary>
    public string Value => configurationEntry.Value;

    /// <summary>
    ///     Gets the configuration level as a string.
    /// </summary>
    public string ConfigurationLevel => configurationEntry.Level.ToString();
}
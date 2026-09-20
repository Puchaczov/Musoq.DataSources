using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git configuration value.</summary>
public class ConfigurationEntityKeyValue
{
    private readonly string _key;
    private readonly string _value;
    private readonly string _configurationLevel;

    /// <summary>Creates a detached configuration snapshot from a LibGit2Sharp entry.</summary>
    /// <param name="configurationEntry">The configuration entry to copy.</param>
    /// <param name="repository">The source repository; it is used only while constructing the snapshot.</param>
    public ConfigurationEntityKeyValue(ConfigurationEntry<string> configurationEntry, Repository repository)
        : this(configurationEntry.Key, configurationEntry.Value, configurationEntry.Level.ToString())
    {
    }

    internal ConfigurationEntityKeyValue(string key, string value, string configurationLevel)
    {
        _key = key;
        _value = value;
        _configurationLevel = configurationLevel;
    }

    /// <summary>Gets the configuration key.</summary>
    public string Key => _key;

    /// <summary>Gets the configuration value.</summary>
    public string Value => _value;

    /// <summary>Gets the configuration level reported by Git.</summary>
    public string ConfigurationLevel => _configurationLevel;
}

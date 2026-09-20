using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a Git remote.
/// </summary>
public class RemoteEntity
{
    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<RemoteEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a remote row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(Name), 0, typeof(string)),
        new SchemaColumn(nameof(Url), 1, typeof(string)),
        new SchemaColumn(nameof(PushUrl), 2, typeof(string))
    ];

    private readonly string _name;
    private readonly string _url;
    private readonly string? _pushUrl;

    static RemoteEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(Name), 0 },
            { nameof(Url), 1 },
            { nameof(PushUrl), 2 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<RemoteEntity, object?>>
        {
            { 0, entity => entity.Name },
            { 1, entity => entity.Url },
            { 2, entity => entity.PushUrl }
        };
    }

    /// <summary>Creates a detached remote snapshot from a LibGit2Sharp remote.</summary>
    /// <param name="remote">The remote to copy.</param>
    public RemoteEntity(Remote remote)
    {
        _name = remote.Name;
        _url = remote.Url;
        _pushUrl = remote.PushUrl;
    }

    internal RemoteEntity(string name, string url, string? pushUrl)
    {
        _name = name;
        _url = url;
        _pushUrl = pushUrl;
    }

    /// <summary>Gets the remote name.</summary>
    public string Name => _name;

    /// <summary>Gets the fetch URL configured for the remote.</summary>
    public string Url => _url;

    /// <summary>Gets the push URL, or <see langword="null"/> when Git has no separate push URL.</summary>
    public string? PushUrl => _pushUrl;
}

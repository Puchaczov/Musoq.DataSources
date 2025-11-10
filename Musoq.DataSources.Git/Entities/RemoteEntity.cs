using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a Git remote.
/// </summary>
public class RemoteEntity
{
    private readonly Remote _remote;

    public RemoteEntity(Remote remote)
    {
        _remote = remote;
    }

    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<RemoteEntity, object?>> IndexToObjectAccessMap;

    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(Name), 0, typeof(string)),
        new SchemaColumn(nameof(Url), 1, typeof(string)),
        new SchemaColumn(nameof(PushUrl), 2, typeof(string))
    ];

    static RemoteEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(Name), 0},
            {nameof(Url), 1},
            {nameof(PushUrl), 2}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<RemoteEntity, object?>>
        {
            {0, entity => entity.Name},
            {1, entity => entity.Url},
            {2, entity => entity.PushUrl}
        };
    }

    public string Name => _remote.Name;
    public string Url => _remote.Url;
    public string? PushUrl => _remote.PushUrl;
}

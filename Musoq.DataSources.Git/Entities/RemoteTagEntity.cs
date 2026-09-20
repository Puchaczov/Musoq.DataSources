using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents metadata advertised for a tag by a configured Git remote.</summary>
public sealed class RemoteTagEntity
{
    /// <summary>Maps SQL-visible column names to row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors.</summary>
    public static readonly IReadOnlyDictionary<int, Func<RemoteTagEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a remote-tag row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(RemoteName), 0, typeof(string)),
        new SchemaColumn(nameof(RemoteUrl), 1, typeof(string)),
        new SchemaColumn(nameof(FriendlyName), 2, typeof(string)),
        new SchemaColumn(nameof(CanonicalName), 3, typeof(string)),
        new SchemaColumn(nameof(ObjectSha), 4, typeof(string)),
        new SchemaColumn(nameof(PeeledSha), 5, typeof(string)),
        new SchemaColumn(nameof(IsAnnotated), 6, typeof(bool))
    ];

    private readonly string _remoteName;
    private readonly string _remoteUrl;
    private readonly string _friendlyName;
    private readonly string _canonicalName;
    private readonly string _objectSha;
    private readonly string? _peeledSha;
    private readonly bool _isAnnotated;

    static RemoteTagEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(RemoteName), 0 }, { nameof(RemoteUrl), 1 }, { nameof(FriendlyName), 2 },
            { nameof(CanonicalName), 3 }, { nameof(ObjectSha), 4 }, { nameof(PeeledSha), 5 },
            { nameof(IsAnnotated), 6 }
        };
        IndexToObjectAccessMap = new Dictionary<int, Func<RemoteTagEntity, object?>>
        {
            { 0, entity => entity.RemoteName }, { 1, entity => entity.RemoteUrl },
            { 2, entity => entity.FriendlyName }, { 3, entity => entity.CanonicalName },
            { 4, entity => entity.ObjectSha }, { 5, entity => entity.PeeledSha },
            { 6, entity => entity.IsAnnotated }
        };
    }

    internal RemoteTagEntity(
        string remoteName,
        string remoteUrl,
        string friendlyName,
        string canonicalName,
        string objectSha,
        string? peeledSha,
        bool isAnnotated)
    {
        _remoteName = remoteName;
        _remoteUrl = remoteUrl;
        _friendlyName = friendlyName;
        _canonicalName = canonicalName;
        _objectSha = objectSha;
        _peeledSha = peeledSha;
        _isAnnotated = isAnnotated;
    }

    /// <summary>Gets the configured remote name.</summary>
    public string RemoteName => _remoteName;

    /// <summary>Gets the configured remote URL.</summary>
    public string RemoteUrl => _remoteUrl;

    /// <summary>Gets the short tag name.</summary>
    public string FriendlyName => _friendlyName;

    /// <summary>Gets the fully qualified remote tag ref name.</summary>
    public string CanonicalName => _canonicalName;

    /// <summary>Gets the object ID advertised by the remote for the tag ref.</summary>
    public string ObjectSha => _objectSha;

    /// <summary>Gets the peeled target object ID for an annotated tag, when available.</summary>
    public string? PeeledSha => _peeledSha;

    /// <summary>Gets whether the remote advertised an annotated tag peel record.</summary>
    public bool IsAnnotated => _isAnnotated;
}

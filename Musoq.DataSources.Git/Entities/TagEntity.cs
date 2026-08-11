using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git tag snapshot.</summary>
public class TagEntity
{
    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<TagEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a tag row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(FriendlyName), 0, typeof(string)),
        new SchemaColumn(nameof(CanonicalName), 1, typeof(string)),
        new SchemaColumn(nameof(Message), 2, typeof(string)),
        new SchemaColumn(nameof(IsAnnotated), 3, typeof(bool)),
        new SchemaColumn(nameof(Annotation), 4, typeof(AnnotationEntity)),
        new SchemaColumn(nameof(Commit), 5, typeof(CommitEntity))
    ];

    private readonly string _repositoryPath;
    private readonly string? _friendlyName;
    private readonly string? _canonicalName;
    private readonly string? _message;
    private readonly bool _isAnnotated;
    private readonly AnnotationEntity? _annotation;
    private readonly string? _commitSha;
    private readonly GitNestedSnapshot<CommitEntity> _commit = new();

    static TagEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(FriendlyName), 0 }, { nameof(CanonicalName), 1 }, { nameof(Message), 2 },
            { nameof(IsAnnotated), 3 }, { nameof(Annotation), 4 }, { nameof(Commit), 5 }
        };
        IndexToObjectAccessMap = new Dictionary<int, Func<TagEntity, object?>>
        {
            { 0, entity => entity.FriendlyName }, { 1, entity => entity.CanonicalName },
            { 2, entity => entity.Message }, { 3, entity => entity.IsAnnotated },
            { 4, entity => entity.Annotation }, { 5, entity => entity.Commit }
        };
    }

    /// <summary>Creates a detached tag snapshot from a LibGit2Sharp tag.</summary>
    /// <param name="tag">The tag to copy.</param>
    /// <param name="repository">The source repository used to capture annotation and target identifiers.</param>
    public TagEntity(Tag tag, Repository repository)
        : this(
            repository.Info.Path,
            tag.FriendlyName,
            tag.CanonicalName,
            tag.Annotation?.Message,
            tag.IsAnnotated,
            tag.Annotation is { } annotation ? new AnnotationEntity(annotation, repository) : null,
            (tag.Target as Commit)?.Sha)
    {
    }

    internal TagEntity(
        string repositoryPath,
        string? friendlyName,
        string? canonicalName,
        string? message,
        bool isAnnotated,
        AnnotationEntity? annotation,
        string? commitSha)
    {
        _repositoryPath = repositoryPath;
        _friendlyName = friendlyName;
        _canonicalName = canonicalName;
        _message = message;
        _isAnnotated = isAnnotated;
        _annotation = annotation;
        _commitSha = commitSha;
    }

    /// <summary>Gets the friendly tag name.</summary>
    public string? FriendlyName => _friendlyName;

    /// <summary>Gets the canonical fully qualified tag name.</summary>
    public string? CanonicalName => _canonicalName;

    /// <summary>Gets the annotation message, or <see langword="null"/> for an unannotated tag.</summary>
    public string? Message => _message;

    /// <summary>Gets whether the tag has an annotation.</summary>
    public bool IsAnnotated => _isAnnotated;

    /// <summary>Gets the detached annotation, or <see langword="null"/> for an unannotated tag.</summary>
    public AnnotationEntity? Annotation => _annotation;

    /// <summary>Gets the tagged commit, resolving it lazily from the captured identifier.</summary>
    /// <remarks>The returned commit is a detached snapshot and does not retain a native repository handle.</remarks>
    public CommitEntity? Commit
    {
        get => _commit.GetOrCreate(() =>
        {
            if (string.IsNullOrWhiteSpace(_commitSha))
                return null;

            using var repository = new Repository(_repositoryPath);
            var commit = repository.Lookup<Commit>(_commitSha);
            return commit is null ? null : new CommitEntity(commit, repository);
        });
    }
}

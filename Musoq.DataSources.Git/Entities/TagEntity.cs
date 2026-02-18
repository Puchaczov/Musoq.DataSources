using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a tag entity in a Git repository.
/// </summary>
public class TagEntity
{
    /// <summary>
    ///     A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    ///     A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<TagEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    ///     An array of schema columns representing the structure of the tag entity.
    /// </summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(FriendlyName), 0, typeof(string)),
        new SchemaColumn(nameof(CanonicalName), 1, typeof(string)),
        new SchemaColumn(nameof(Message), 2, typeof(string)),
        new SchemaColumn(nameof(IsAnnotated), 3, typeof(bool)),
        new SchemaColumn(nameof(Annotation), 4, typeof(AnnotationEntity)),
        new SchemaColumn(nameof(Commit), 5, typeof(CommitEntity))
    ];

    private readonly Repository _libGitRepository;
    private readonly Tag _tag;

    /// <summary>
    ///     Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static TagEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(FriendlyName), 0 },
            { nameof(CanonicalName), 1 },
            { nameof(Message), 2 },
            { nameof(IsAnnotated), 3 },
            { nameof(Annotation), 4 },
            { nameof(Commit), 5 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<TagEntity, object?>>
        {
            { 0, entity => entity.FriendlyName },
            { 1, entity => entity.CanonicalName },
            { 2, entity => entity.Message },
            { 3, entity => entity.IsAnnotated },
            { 4, entity => entity.Annotation },
            { 5, entity => entity.Commit }
        };
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="TagEntity" /> class.
    /// </summary>
    /// <param name="tag">The tag object from LibGit2Sharp.</param>
    /// <param name="repository">The Git repository.</param>
    public TagEntity(Tag tag, Repository repository)
    {
        _tag = tag;
        _libGitRepository = repository;
    }

    /// <summary>
    ///     Gets the friendly name of the tag.
    /// </summary>
    public string? FriendlyName => _tag.FriendlyName;

    /// <summary>
    ///     Gets the canonical name of the tag.
    /// </summary>
    public string? CanonicalName => _tag.CanonicalName;

    /// <summary>
    ///     Gets the message of the tag annotation.
    /// </summary>
    public string? Message => _tag.Annotation?.Message;

    /// <summary>
    ///     Gets a value indicating whether the tag is annotated.
    /// </summary>
    public bool IsAnnotated => _tag.IsAnnotated;

    /// <summary>
    ///     Gets the annotation entity of the tag.
    /// </summary>
    public AnnotationEntity Annotation => new(_tag.Annotation, _libGitRepository);

    /// <summary>
    ///     Gets the commit entity that the tag points to.
    /// </summary>
    public CommitEntity? Commit
    {
        get
        {
            if (_tag.Target is Commit commit) return new CommitEntity(commit, _libGitRepository);

            return null;
        }
    }
}
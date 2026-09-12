using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git tag snapshot.</summary>
public class TagEntity
{
    internal readonly record struct TagRichSnapshot(
        string? TargetSha,
        string? Message,
        bool IsAnnotated,
        AnnotationEntity? Annotation,
        string? CommitSha);

    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<TagEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a tag row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(FriendlyName), 0, typeof(string)),
        new SchemaColumn(nameof(CanonicalName), 1, typeof(string)),
        new SchemaColumn(nameof(TargetSha), 2, typeof(string)),
        new SchemaColumn(nameof(Message), 3, typeof(string)),
        new SchemaColumn(nameof(IsAnnotated), 4, typeof(bool)),
        new SchemaColumn(nameof(Annotation), 5, typeof(AnnotationEntity)),
        new SchemaColumn(nameof(Commit), 6, typeof(CommitEntity))
    ];

    private readonly string _repositoryPath;
    private readonly string? _friendlyName;
    private readonly string? _canonicalName;
    private readonly string? _canonicalRef;
    private readonly string? _targetSha;
    private readonly string? _message;
    private readonly bool _isAnnotated;
    private readonly AnnotationEntity? _annotation;
    private readonly string? _commitSha;
    private readonly Func<TagRichSnapshot>? _richLoader;
    private readonly bool _isAnnotatedKnown;
    private bool _richLoaded;
    private TagRichSnapshot? _richSnapshot;
    private readonly GitNestedSnapshot<CommitEntity> _commit = new();

    static TagEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(FriendlyName), 0 }, { nameof(CanonicalName), 1 }, { nameof(TargetSha), 2 },
            { nameof(Message), 3 }, { nameof(IsAnnotated), 4 }, { nameof(Annotation), 5 }, { nameof(Commit), 6 }
        };
        IndexToObjectAccessMap = new Dictionary<int, Func<TagEntity, object?>>
        {
            { 0, entity => entity.FriendlyName }, { 1, entity => entity.CanonicalName },
            { 2, entity => entity.TargetSha }, { 3, entity => entity.Message }, { 4, entity => entity.IsAnnotated },
            { 5, entity => entity.Annotation }, { 6, entity => entity.Commit }
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
            ((tag.PeeledTarget ?? tag.Target) as Commit)?.Sha,
            tag.Annotation?.Message,
            tag.IsAnnotated,
            tag.Annotation is { } annotation ? new AnnotationEntity(annotation, repository) : null,
            ((tag.PeeledTarget ?? tag.Target) as Commit)?.Sha,
            tag.CanonicalName,
            null,
            true)
    {
    }

    internal TagEntity(
        string repositoryPath,
        string? friendlyName,
        string? canonicalName,
        string? targetSha,
        string? message,
        bool isAnnotated,
        AnnotationEntity? annotation,
        string? commitSha,
        string? canonicalRef = null,
        Func<TagRichSnapshot>? richLoader = null,
        bool isAnnotatedKnown = true)
    {
        _repositoryPath = repositoryPath;
        _friendlyName = friendlyName;
        _canonicalName = canonicalName;
        _canonicalRef = canonicalRef ?? canonicalName;
        _targetSha = targetSha;
        _message = message;
        _isAnnotated = isAnnotated;
        _annotation = annotation;
        _commitSha = commitSha;
        _richLoader = richLoader;
        _isAnnotatedKnown = isAnnotatedKnown || richLoader is null;
    }

    /// <summary>Gets the friendly tag name.</summary>
    public string? FriendlyName => _friendlyName;

    /// <summary>Gets the canonical fully qualified tag name.</summary>
    public string? CanonicalName => _canonicalName;

    /// <summary>Gets the tag target object SHA without hydrating the nested commit.</summary>
    public string? TargetSha => _targetSha ?? LoadRichSnapshot()?.TargetSha;

    /// <summary>Gets the annotation message, or <see langword="null"/> for an unannotated tag.</summary>
    public string? Message => _message ?? LoadRichSnapshot()?.Message;

    /// <summary>Gets whether the tag has an annotation.</summary>
    public bool IsAnnotated => _isAnnotatedKnown ? _isAnnotated : LoadRichSnapshot()?.IsAnnotated ?? false;

    /// <summary>Gets the detached annotation, or <see langword="null"/> for an unannotated tag.</summary>
    public AnnotationEntity? Annotation => _annotation ?? LoadRichSnapshot()?.Annotation;

    /// <summary>Gets the tagged commit, resolving it lazily from the captured identifier.</summary>
    /// <remarks>The returned commit is a detached snapshot and does not retain a native repository handle.</remarks>
    public CommitEntity? Commit
    {
        get => _commit.GetOrCreate(() =>
        {
            using var repository = new Repository(_repositoryPath);
            var commitSha = _commitSha ?? LoadRichSnapshot()?.CommitSha;
            if (string.IsNullOrWhiteSpace(commitSha))
                return ResolveCommitFromTag(repository);

            var commit = repository.Lookup<Commit>(commitSha);
            return commit is null ? ResolveCommitFromTag(repository) : new CommitEntity(commit, repository);
        });
    }

    internal static TagRichSnapshot LoadRichSnapshot(
        string repositoryPath,
        string? canonicalRef,
        string? friendlyName,
        bool includeMessage = true)
    {
        using var repository = new Repository(repositoryPath);
        var tag = !string.IsNullOrWhiteSpace(friendlyName) ? repository.Tags[friendlyName] : null;
        tag ??= !string.IsNullOrWhiteSpace(canonicalRef) ? repository.Tags[canonicalRef] : null;
        if (tag is null)
            return new TagRichSnapshot(null, null, false, null, null);

        var target = tag.PeeledTarget ?? tag.Target;
        var targetSha = (target as GitObject)?.Sha;
        var commitSha = (target as Commit)?.Sha;
        var annotation = tag.Annotation is { } value ? new AnnotationEntity(value, repository) : null;
        return new TagRichSnapshot(targetSha, includeMessage ? annotation?.Message : null, tag.IsAnnotated, annotation, commitSha);
    }

    private TagRichSnapshot? LoadRichSnapshot()
    {
        if (_richLoaded)
            return _richSnapshot;

        _richLoaded = true;
        _richSnapshot = _richLoader?.Invoke();
        return _richSnapshot;
    }

    private CommitEntity? ResolveCommitFromTag(Repository repository)
    {
        if (string.IsNullOrWhiteSpace(_canonicalRef) && string.IsNullOrWhiteSpace(_friendlyName))
            return null;

        var tag = !string.IsNullOrWhiteSpace(_friendlyName) ? repository.Tags[_friendlyName] : null;
        tag ??= !string.IsNullOrWhiteSpace(_canonicalRef) ? repository.Tags[_canonicalRef] : null;
        var commit = tag?.PeeledTarget as Commit ?? tag?.Target as Commit;
        return commit is null ? null : new CommitEntity(commit, repository);
    }
}

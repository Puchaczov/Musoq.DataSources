using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached repository snapshot with lazy, short-lived nested scopes.</summary>
public class RepositoryEntity
{
    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<RepositoryEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a repository row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(Path), 0, typeof(string)),
        new SchemaColumn(nameof(WorkingDirectory), 1, typeof(string)),
        new SchemaColumn(nameof(Branches), 2, typeof(IEnumerable<BranchEntity>)),
        new SchemaColumn(nameof(Tags), 3, typeof(IEnumerable<TagEntity>)),
        new SchemaColumn(nameof(Commits), 4, typeof(IEnumerable<CommitEntity>)),
        new SchemaColumn(nameof(Head), 5, typeof(BranchEntity)),
        new SchemaColumn(nameof(Configuration), 6, typeof(IEnumerable<ConfigurationEntityKeyValue>)),
        new SchemaColumn(nameof(Information), 7, typeof(RepositoryInformationEntity)),
        new SchemaColumn(nameof(Stashes), 8, typeof(IEnumerable<StashEntity>)),
        new SchemaColumn(nameof(Self), 9, typeof(RepositoryEntity))
    ];

    private readonly string _path;
    private readonly string _workingDirectory;
    private readonly RepositoryInformationEntity _information;
    private readonly GitReferenceBackendOptions _referenceOptions;
    private readonly GitNestedSnapshot<IReadOnlyList<BranchEntity>> _branches = new();
    private readonly GitNestedSnapshot<IReadOnlyList<CommitEntity>> _commits = new();
    private readonly GitNestedSnapshot<BranchEntity> _head = new();
    private readonly GitNestedSnapshot<IReadOnlyList<ConfigurationEntityKeyValue>> _configuration = new();

    static RepositoryEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(Path), 0 }, { nameof(WorkingDirectory), 1 }, { nameof(Branches), 2 }, { nameof(Tags), 3 },
            { nameof(Commits), 4 }, { nameof(Head), 5 }, { nameof(Configuration), 6 }, { nameof(Information), 7 },
            { nameof(Stashes), 8 }, { nameof(Self), 9 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<RepositoryEntity, object?>>
        {
            { 0, entity => entity.Path }, { 1, entity => entity.WorkingDirectory }, { 2, entity => entity.Branches },
            { 3, entity => entity.Tags }, { 4, entity => entity.Commits }, { 5, entity => entity.Head },
            { 6, entity => entity.Configuration }, { 7, entity => entity.Information }, { 8, entity => entity.Stashes },
            { 9, entity => entity.Self }
        };
    }

    /// <summary>Copies the repository identity and information; it does not retain the native handle.</summary>
    public RepositoryEntity(Repository repository)
        : this(
            repository.Info.Path,
            repository.Info.WorkingDirectory,
            new RepositoryInformationEntity(repository.Info, repository),
            GitReferenceBackendOptions.Default)
    {
    }

    internal RepositoryEntity(string path, string workingDirectory, RepositoryInformationEntity information)
        : this(path, workingDirectory, information, GitReferenceBackendOptions.Default)
    {
    }

    internal RepositoryEntity(
        string path,
        string workingDirectory,
        RepositoryInformationEntity information,
        GitReferenceBackendOptions referenceOptions)
    {
        _path = path;
        _workingDirectory = workingDirectory;
        _information = information;
        _referenceOptions = referenceOptions;
    }

    /// <summary>Gets the repository's canonical path.</summary>
    public string Path => _path;

    /// <summary>Gets the repository working-directory path.</summary>
    public string WorkingDirectory => _workingDirectory;

    /// <summary>Gets branches as detached snapshots, loading them lazily in a short-lived repository scope.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<BranchEntity> Branches => _branches.GetOrCreate(() => Read(repository =>
        (IReadOnlyList<BranchEntity>)repository.Branches.Select(branch => new BranchEntity(branch, repository)).ToArray()))
        ?? Array.Empty<BranchEntity>();

    /// <summary>Gets tags as detached snapshots from a bounded streaming reference enumeration.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<TagEntity> Tags => ReadTags();

    /// <summary>Gets commits as detached snapshots, loading them lazily in a short-lived repository scope.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Commits => _commits.GetOrCreate(() => Read(repository =>
        (IReadOnlyList<CommitEntity>)repository.Commits.Select(commit => new CommitEntity(commit, repository)).ToArray()))
        ?? Array.Empty<CommitEntity>();

    /// <summary>Gets the current head branch, or <see langword="null"/> for a detached or unborn head.</summary>
    /// <remarks>The value is resolved lazily using a short-lived repository scope.</remarks>
    public BranchEntity? Head => _head.GetOrCreate(() => Read(repository =>
    {
        var branch = repository.Head;
        return branch is null ? null : new BranchEntity(branch, repository);
    }));

    /// <summary>Gets configuration entries as detached snapshots, loading them lazily.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<ConfigurationEntityKeyValue> Configuration => _configuration.GetOrCreate(() => Read(repository =>
        (IReadOnlyList<ConfigurationEntityKeyValue>)repository.Config
            .Select(entry => new ConfigurationEntityKeyValue(entry, repository)).ToArray()))
        ?? Array.Empty<ConfigurationEntityKeyValue>();

    /// <summary>Gets immutable repository information captured when this row was created.</summary>
    public RepositoryInformationEntity Information => _information;

    /// <summary>Gets stash entries as detached snapshots from a bounded streaming reference enumeration.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<StashEntity> Stashes => ReadStashes();

    /// <summary>Gets this repository row.</summary>
    public RepositoryEntity Self => this;

    internal string RepositoryPath => _path;

    internal T Read<T>(Func<Repository, T> action)
    {
        using var repository = new Repository(_path);
        return action(repository);
    }

    private IEnumerable<TagEntity> ReadTags()
    {
        var reader = _referenceOptions.Backend == GitHistoryBackend.LibGit2
            ? GitOperationReaders.Tags
            : GitOperationReaders.CliTags;
        var projection = new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]);
        foreach (var tag in reader.ReadStreaming(
                     _path,
                     _referenceOptions,
                     projection,
                     GitTagReadQuery.Empty,
                     static path => new Repository(path),
                     CancellationToken.None))
        {
            yield return GitEntitySnapshots.Tag(
                tag,
                projection,
                () => TagEntity.LoadRichSnapshot(_path, tag.CanonicalName, tag.FriendlyName));
        }
    }

    private IEnumerable<StashEntity> ReadStashes()
    {
        var reader = _referenceOptions.Backend == GitHistoryBackend.LibGit2
            ? GitOperationReaders.Stashes
            : GitOperationReaders.CliStashes;
        var projection = new GitProjection(true, [
            nameof(StashEntity.Selector),
            nameof(StashEntity.Sha),
            nameof(StashEntity.Message),
            nameof(StashEntity.Index),
            nameof(StashEntity.WorkTree),
            nameof(StashEntity.UntrackedFiles)]);
        foreach (var stash in reader.ReadStreaming(
                     _path,
                     _referenceOptions,
                     projection,
                     GitStashReadQuery.Empty,
                     static path => new Repository(path),
                     CancellationToken.None))
            yield return GitEntitySnapshots.Stash(stash, projection);
    }
}

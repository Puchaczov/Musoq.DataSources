using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a Git repository entity, providing access to various repository properties and collections.
/// </summary>
public class RepositoryEntity
{
    /// <summary>
    ///     A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    ///     A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<RepositoryEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    ///     An array of schema columns representing the structure of the solution entity.
    /// </summary>
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

    /// <summary>
    ///     Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static RepositoryEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(Path), 0 },
            { nameof(WorkingDirectory), 1 },
            { nameof(Branches), 2 },
            { nameof(Tags), 3 },
            { nameof(Commits), 4 },
            { nameof(Head), 5 },
            { nameof(Configuration), 6 },
            { nameof(Information), 7 },
            { nameof(Stashes), 8 },
            { nameof(Self), 9 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<RepositoryEntity, object?>>
        {
            { 0, entity => entity.Path },
            { 1, entity => entity.WorkingDirectory },
            { 2, entity => entity.Branches },
            { 3, entity => entity.Tags },
            { 4, entity => entity.Commits },
            { 5, entity => entity.Head },
            { 6, entity => entity.Configuration },
            { 7, entity => entity.Information },
            { 8, entity => entity.Stashes },
            { 9, entity => entity.Self }
        };
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepositoryEntity" /> class.
    /// </summary>
    /// <param name="repository">The Git repository.</param>
    public RepositoryEntity(Repository repository)
    {
        LibGitRepository = repository;
    }

    /// <summary>
    ///     Gets the path of the repository.
    /// </summary>
    public string Path => LibGitRepository.Info.Path;

    /// <summary>
    ///     Gets the working directory of the repository.
    /// </summary>
    public string WorkingDirectory => LibGitRepository.Info.WorkingDirectory;

    /// <summary>
    ///     Gets the branches in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<BranchEntity> Branches =>
        LibGitRepository.Branches.Select(branch => new BranchEntity(branch, LibGitRepository));

    /// <summary>
    ///     Gets the tags in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<TagEntity> Tags => LibGitRepository.Tags.Select(tag => new TagEntity(tag, LibGitRepository));

    /// <summary>
    ///     Gets the commits in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Commits =>
        LibGitRepository.Commits.Select(commit => new CommitEntity(commit, LibGitRepository));

    /// <summary>
    ///     Gets the head branch of the repository.
    /// </summary>
    public BranchEntity Head => new(LibGitRepository.Head, LibGitRepository);

    /// <summary>
    ///     Gets the configuration key-value pairs of the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ConfigurationEntityKeyValue> Configuration =>
        LibGitRepository.Config.Select(f => new ConfigurationEntityKeyValue(f, LibGitRepository));

    /// <summary>
    ///     Gets the information about the repository.
    /// </summary>
    public RepositoryInformationEntity Information => new(LibGitRepository.Info, LibGitRepository);

    /// <summary>
    ///     Gets the stashes in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<StashEntity> Stashes =>
        LibGitRepository.Stashes.Select(stash => new StashEntity(stash, LibGitRepository));

    /// <summary>
    ///     Gets the repository entity itself.
    /// </summary>
    public RepositoryEntity Self => this;

    /// <summary>
    ///     Gets the underlying LibGit2Sharp repository.
    /// </summary>
    internal Repository LibGitRepository { get; }

    ~RepositoryEntity()
    {
        try
        {
            LibGitRepository.Dispose();
        }
        catch
        {
            // ignored
        }
    }
}
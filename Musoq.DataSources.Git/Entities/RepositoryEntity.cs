using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a Git repository entity, providing access to various repository properties and collections.
/// </summary>
public class RepositoryEntity
{
    private readonly Repository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryEntity"/> class.
    /// </summary>
    /// <param name="repository">The Git repository.</param>
    public RepositoryEntity(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    /// A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<RepositoryEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    /// An array of schema columns representing the structure of the solution entity.
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
    /// Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static RepositoryEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(Path), 0},
            {nameof(WorkingDirectory), 1},
            {nameof(Branches), 2},
            {nameof(Tags), 3},
            {nameof(Commits), 4},
            {nameof(Head), 5},
            {nameof(Configuration), 6},
            {nameof(Information), 7},
            {nameof(Stashes), 8},
            {nameof(Self), 9}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<RepositoryEntity, object?>>
        {
            {0, entity => entity.Path},
            {1, entity => entity.WorkingDirectory},
            {2, entity => entity.Branches},
            {3, entity => entity.Tags},
            {4, entity => entity.Commits},
            {5, entity => entity.Head},
            {6, entity => entity.Configuration},
            {7, entity => entity.Information},
            {8, entity => entity.Stashes},
            {9, entity => entity.Self}
        };
    }

    /// <summary>
    /// Gets the path of the repository.
    /// </summary>
    public string Path => _repository.Info.Path;

    /// <summary>
    /// Gets the working directory of the repository.
    /// </summary>
    public string WorkingDirectory => _repository.Info.WorkingDirectory;

    /// <summary>
    /// Gets the branches in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<BranchEntity> Branches => _repository.Branches.Select(branch => new BranchEntity(branch, _repository));

    /// <summary>
    /// Gets the tags in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<TagEntity> Tags => _repository.Tags.Select(tag => new TagEntity(tag));

    /// <summary>
    /// Gets the commits in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Commits => _repository.Commits.Select(commit => new CommitEntity(commit));

    /// <summary>
    /// Gets the head branch of the repository.
    /// </summary>
    public BranchEntity Head => new(_repository.Head, _repository);

    /// <summary>
    /// Gets the configuration key-value pairs of the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ConfigurationEntityKeyValue> Configuration => _repository.Config.Select(f => new ConfigurationEntityKeyValue(f));

    /// <summary>
    /// Gets the information about the repository.
    /// </summary>
    public RepositoryInformationEntity Information => new(_repository.Info);

    /// <summary>
    /// Gets the stashes in the repository.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<StashEntity> Stashes => _repository.Stashes.Select(stash => new StashEntity(stash));
    
    /// <summary>
    /// Gets the repository entity itself.
    /// </summary>
    public RepositoryEntity Self => this;

    /// <summary>
    /// Gets the underlying LibGit2Sharp repository.
    /// </summary>
    internal Repository LibGitRepository => _repository;
}
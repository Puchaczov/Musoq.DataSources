using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

public class RepositoryEntity(Repository repository)
{    
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
        new SchemaColumn(nameof(Config), 6, typeof(ConfigurationEntity)),
        new SchemaColumn(nameof(Info), 7, typeof(RepositoryInformationEntity)),
        new SchemaColumn(nameof(Stashes), 8, typeof(IEnumerable<StashEntity>))
    ];

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
            {nameof(Config), 6},
            {nameof(Info), 7},
            {nameof(Stashes), 8}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<RepositoryEntity, object?>>
        {
            {0, entity => entity.Path},
            {1, entity => entity.WorkingDirectory},
            {2, entity => entity.Branches},
            {3, entity => entity.Tags},
            {4, entity => entity.Commits},
            {5, entity => entity.Head},
            {6, entity => entity.Config},
            {7, entity => entity.Info},
            {8, entity => entity.Stashes}
        };
    }
    
    public string Path => repository.Info.Path;
    
    public string WorkingDirectory => repository.Info.WorkingDirectory;
    
    [BindablePropertyAsTable]
    public IEnumerable<BranchEntity> Branches => repository.Branches.Select(branch => new BranchEntity(branch));

    [BindablePropertyAsTable]
    public IEnumerable<TagEntity> Tags => repository.Tags.Select(tag => new TagEntity(tag));
    
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Commits => repository.Commits.Select(commit => new CommitEntity(commit));
    
    public BranchEntity Head => new(repository.Head);
    
    public ConfigurationEntity Config => new(repository.Config);

    public RepositoryInformationEntity Info => new RepositoryInformationEntity(repository.Info);
    
    [BindablePropertyAsTable]
    public IEnumerable<StashEntity> Stashes => repository.Stashes.Select(stash => new StashEntity(stash));
}
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Repositories;

internal static class RepositoriesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> RepositoriesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<RepositoryEntity, object?>> RepositoriesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] RepositoriesColumns;

    static RepositoriesSourceHelper()
    {
        RepositoriesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(RepositoryEntity.Id), 0 },
            { nameof(RepositoryEntity.Name), 1 },
            { nameof(RepositoryEntity.FullName), 2 },
            { nameof(RepositoryEntity.Description), 3 },
            { nameof(RepositoryEntity.Url), 4 },
            { nameof(RepositoryEntity.CloneUrl), 5 },
            { nameof(RepositoryEntity.SshUrl), 6 },
            { nameof(RepositoryEntity.DefaultBranch), 7 },
            { nameof(RepositoryEntity.IsPrivate), 8 },
            { nameof(RepositoryEntity.IsFork), 9 },
            { nameof(RepositoryEntity.IsArchived), 10 },
            { nameof(RepositoryEntity.Language), 11 },
            { nameof(RepositoryEntity.ForksCount), 12 },
            { nameof(RepositoryEntity.StargazersCount), 13 },
            { nameof(RepositoryEntity.WatchersCount), 14 },
            { nameof(RepositoryEntity.OpenIssuesCount), 15 },
            { nameof(RepositoryEntity.Size), 16 },
            { nameof(RepositoryEntity.CreatedAt), 17 },
            { nameof(RepositoryEntity.UpdatedAt), 18 },
            { nameof(RepositoryEntity.PushedAt), 19 },
            { nameof(RepositoryEntity.OwnerLogin), 20 },
            { nameof(RepositoryEntity.License), 21 },
            { nameof(RepositoryEntity.Topics), 22 },
            { nameof(RepositoryEntity.HasIssues), 23 },
            { nameof(RepositoryEntity.HasWiki), 24 },
            { nameof(RepositoryEntity.HasDownloads), 25 },
            { nameof(RepositoryEntity.Visibility), 26 }
        };

        RepositoriesIndexToMethodAccessMap = new Dictionary<int, Func<RepositoryEntity, object?>>
        {
            { 0, repo => repo.Id },
            { 1, repo => repo.Name },
            { 2, repo => repo.FullName },
            { 3, repo => repo.Description },
            { 4, repo => repo.Url },
            { 5, repo => repo.CloneUrl },
            { 6, repo => repo.SshUrl },
            { 7, repo => repo.DefaultBranch },
            { 8, repo => repo.IsPrivate },
            { 9, repo => repo.IsFork },
            { 10, repo => repo.IsArchived },
            { 11, repo => repo.Language },
            { 12, repo => repo.ForksCount },
            { 13, repo => repo.StargazersCount },
            { 14, repo => repo.WatchersCount },
            { 15, repo => repo.OpenIssuesCount },
            { 16, repo => repo.Size },
            { 17, repo => repo.CreatedAt },
            { 18, repo => repo.UpdatedAt },
            { 19, repo => repo.PushedAt },
            { 20, repo => repo.OwnerLogin },
            { 21, repo => repo.License },
            { 22, repo => repo.Topics },
            { 23, repo => repo.HasIssues },
            { 24, repo => repo.HasWiki },
            { 25, repo => repo.HasDownloads },
            { 26, repo => repo.Visibility }
        };

        RepositoriesColumns =
        [
            new SchemaColumn(nameof(RepositoryEntity.Id), 0, typeof(long)),
            new SchemaColumn(nameof(RepositoryEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.FullName), 2, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.Description), 3, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.Url), 4, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.CloneUrl), 5, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.SshUrl), 6, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.DefaultBranch), 7, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.IsPrivate), 8, typeof(bool)),
            new SchemaColumn(nameof(RepositoryEntity.IsFork), 9, typeof(bool)),
            new SchemaColumn(nameof(RepositoryEntity.IsArchived), 10, typeof(bool)),
            new SchemaColumn(nameof(RepositoryEntity.Language), 11, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.ForksCount), 12, typeof(int)),
            new SchemaColumn(nameof(RepositoryEntity.StargazersCount), 13, typeof(int)),
            new SchemaColumn(nameof(RepositoryEntity.WatchersCount), 14, typeof(int)),
            new SchemaColumn(nameof(RepositoryEntity.OpenIssuesCount), 15, typeof(int)),
            new SchemaColumn(nameof(RepositoryEntity.Size), 16, typeof(long)),
            new SchemaColumn(nameof(RepositoryEntity.CreatedAt), 17, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(RepositoryEntity.UpdatedAt), 18, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(RepositoryEntity.PushedAt), 19, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(RepositoryEntity.OwnerLogin), 20, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.License), 21, typeof(string)),
            new SchemaColumn(nameof(RepositoryEntity.Topics), 22, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(RepositoryEntity.HasIssues), 23, typeof(bool)),
            new SchemaColumn(nameof(RepositoryEntity.HasWiki), 24, typeof(bool)),
            new SchemaColumn(nameof(RepositoryEntity.HasDownloads), 25, typeof(bool)),
            new SchemaColumn(nameof(RepositoryEntity.Visibility), 26, typeof(string))
        ];
    }
}
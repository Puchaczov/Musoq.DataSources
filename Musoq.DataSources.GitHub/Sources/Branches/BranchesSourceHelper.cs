using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Branches;

internal static class BranchesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> BranchesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<BranchEntity, object?>> BranchesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] BranchesColumns;

    static BranchesSourceHelper()
    {
        BranchesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(BranchEntity.Name), 0},
            {nameof(BranchEntity.CommitSha), 1},
            {nameof(BranchEntity.CommitUrl), 2},
            {nameof(BranchEntity.Protected), 3},
            {nameof(BranchEntity.RepositoryOwner), 4},
            {nameof(BranchEntity.RepositoryName), 5}
        };

        BranchesIndexToMethodAccessMap = new Dictionary<int, Func<BranchEntity, object?>>
        {
            {0, branch => branch.Name},
            {1, branch => branch.CommitSha},
            {2, branch => branch.CommitUrl},
            {3, branch => branch.Protected},
            {4, branch => branch.RepositoryOwner},
            {5, branch => branch.RepositoryName}
        };

        BranchesColumns =
        [
            new SchemaColumn(nameof(BranchEntity.Name), 0, typeof(string)),
            new SchemaColumn(nameof(BranchEntity.CommitSha), 1, typeof(string)),
            new SchemaColumn(nameof(BranchEntity.CommitUrl), 2, typeof(string)),
            new SchemaColumn(nameof(BranchEntity.Protected), 3, typeof(bool)),
            new SchemaColumn(nameof(BranchEntity.RepositoryOwner), 4, typeof(string)),
            new SchemaColumn(nameof(BranchEntity.RepositoryName), 5, typeof(string))
        ];
    }
}

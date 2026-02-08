using Musoq.DataSources.Jira.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Jira.Sources.Comments;

internal static class CommentsSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> CommentsNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<IJiraComment, object?>> CommentsIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] CommentsColumns;

    static CommentsSourceHelper()
    {
        CommentsNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(IJiraComment.Id), 0},
            {nameof(IJiraComment.IssueKey), 1},
            {nameof(IJiraComment.Body), 2},
            {nameof(IJiraComment.Author), 3},
            {nameof(IJiraComment.AuthorDisplayName), 4},
            {nameof(IJiraComment.UpdateAuthor), 5},
            {nameof(IJiraComment.UpdateAuthorDisplayName), 6},
            {nameof(IJiraComment.CreatedAt), 7},
            {nameof(IJiraComment.UpdatedAt), 8},
            {nameof(IJiraComment.VisibilityGroup), 9},
            {nameof(IJiraComment.VisibilityRole), 10}
        };

        CommentsIndexToMethodAccessMap = new Dictionary<int, Func<IJiraComment, object?>>
        {
            {0, comment => comment.Id},
            {1, comment => comment.IssueKey},
            {2, comment => comment.Body},
            {3, comment => comment.Author},
            {4, comment => comment.AuthorDisplayName},
            {5, comment => comment.UpdateAuthor},
            {6, comment => comment.UpdateAuthorDisplayName},
            {7, comment => comment.CreatedAt},
            {8, comment => comment.UpdatedAt},
            {9, comment => comment.VisibilityGroup},
            {10, comment => comment.VisibilityRole}
        };

        CommentsColumns =
        [
            new SchemaColumn(nameof(IJiraComment.Id), 0, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.IssueKey), 1, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.Body), 2, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.Author), 3, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.AuthorDisplayName), 4, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.UpdateAuthor), 5, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.UpdateAuthorDisplayName), 6, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.CreatedAt), 7, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(IJiraComment.UpdatedAt), 8, typeof(DateTimeOffset?)),
            new SchemaColumn(nameof(IJiraComment.VisibilityGroup), 9, typeof(string)),
            new SchemaColumn(nameof(IJiraComment.VisibilityRole), 10, typeof(string))
        ];
    }
}

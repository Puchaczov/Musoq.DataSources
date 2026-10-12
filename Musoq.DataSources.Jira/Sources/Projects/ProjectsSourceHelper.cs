using Musoq.DataSources.Jira.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Jira.Sources.Projects;

internal static class ProjectsSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> ProjectsNameToIndexMap;

    public static readonly ISchemaColumn[] ProjectsColumns;

    static ProjectsSourceHelper()
    {
        ProjectsNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(IJiraProject.Id), 0 },
            { nameof(IJiraProject.Key), 1 },
            { nameof(IJiraProject.Name), 2 },
            { nameof(IJiraProject.Description), 3 },
            { nameof(IJiraProject.Lead), 4 },
            { nameof(IJiraProject.Url), 5 },
            { nameof(IJiraProject.Category), 6 },
            { nameof(IJiraProject.CategoryDescription), 7 },
            { nameof(IJiraProject.AvatarUrl), 8 }
        };

        ProjectsColumns =
        [
            new SchemaColumn(nameof(IJiraProject.Id), 0, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.Key), 1, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.Name), 2, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.Description), 3, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.Lead), 4, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.Url), 5, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.Category), 6, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.CategoryDescription), 7, typeof(string)),
            new SchemaColumn(nameof(IJiraProject.AvatarUrl), 8, typeof(string))
        ];
    }
}
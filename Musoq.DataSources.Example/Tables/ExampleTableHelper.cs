using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.Example.Entities;

namespace Musoq.DataSources.Example.Tables;

internal static class ExampleTableHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<ExampleEntity, object?>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static ExampleTableHelper()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(ExampleEntity.Id), 0},
            {nameof(ExampleEntity.Name), 1},
            {nameof(ExampleEntity.CreatedDate), 2},
            {nameof(ExampleEntity.Value), 3},
            {nameof(ExampleEntity.IsActive), 4},
            {nameof(ExampleEntity.Category), 5},
            {nameof(ExampleEntity.Description), 6}
        };
        
        IndexToMethodAccessMap = new Dictionary<int, Func<ExampleEntity, object?>>
        {
            {0, entity => entity.Id},
            {1, entity => entity.Name},
            {2, entity => entity.CreatedDate},
            {3, entity => entity.Value},
            {4, entity => entity.IsActive},
            {5, entity => entity.Category},
            {6, entity => entity.Description}
        };
        
        Columns = new[]
        {
            new SchemaColumn(nameof(ExampleEntity.Id), 0, typeof(string)),
            new SchemaColumn(nameof(ExampleEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(ExampleEntity.CreatedDate), 2, typeof(DateTime)),
            new SchemaColumn(nameof(ExampleEntity.Value), 3, typeof(int)),
            new SchemaColumn(nameof(ExampleEntity.IsActive), 4, typeof(bool)),
            new SchemaColumn(nameof(ExampleEntity.Category), 5, typeof(string)),
            new SchemaColumn(nameof(ExampleEntity.Description), 6, typeof(string))
        };
    }
}
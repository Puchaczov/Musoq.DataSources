using System.Collections.Concurrent;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.Example.Entities;
using Musoq.DataSources.Example.Tables;

namespace Musoq.DataSources.Example.Sources;

internal class ExampleRowSource : RowSourceBase<ExampleEntity>
{
    private readonly int _count;
    private readonly string? _filter;

    public ExampleRowSource(RuntimeContext runtimeContext, int count = 10, string? filter = null)
    {
        _count = count;
        _filter = filter;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var random = new Random();
        var categories = new[] { "Technology", "Business", "Education", "Entertainment", "Health" };
        
        var entities = Enumerable.Range(1, _count)
            .Select(i => new ExampleEntity
            {
                Id = $"EX{i:D4}",
                Name = $"Example Item {i}",
                CreatedDate = DateTime.Now.AddDays(-random.Next(0, 365)),
                Value = random.Next(1, 1000),
                IsActive = random.NextDouble() > 0.3, // 70% chance of being active
                Category = categories[random.Next(categories.Length)],
                Description = random.NextDouble() > 0.5 ? $"Description for item {i}" : null
            })
            .Where(entity => string.IsNullOrEmpty(_filter) || 
                           entity.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                           entity.Category.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var resolvers = entities.Select(entity => 
            new EntityResolver<ExampleEntity>(entity, ExampleTableHelper.NameToIndexMap, ExampleTableHelper.IndexToMethodAccessMap))
            .ToList();

        chunkedSource.Add(resolvers);
    }
}
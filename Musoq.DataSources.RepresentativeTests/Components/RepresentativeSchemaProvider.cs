using Musoq.DataSources.Archives;
using Musoq.DataSources.Git;
using Musoq.DataSources.Json;
using Musoq.DataSources.Os;
using Musoq.DataSources.SeparatedValues;
using Musoq.DataSources.System;
using Musoq.DataSources.Time;
using Musoq.Schema;

namespace Musoq.DataSources.RepresentativeTests.Components;

public class RepresentativeSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return schema.ToLowerInvariant() switch
        {
            "#os" or "#disk" => new OsSchema(),
            "#separatedvalues" or "#csv" => new SeparatedValuesSchema(),
            "#time" => new TimeSchema(),
            "#system" => new SystemSchema(),
            "#archives" => new ArchivesSchema(),
            "#json" => new JsonSchema(),
            "#git" => new GitSchema(),
            _ => throw new Exception($"Schema '{schema}' not found")
        };
    }
}

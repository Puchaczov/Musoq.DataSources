using AirtableApiClient;
using Musoq.DataSources.Airtable.Components;
using AirtableBase = Musoq.DataSources.Airtable.Sources.Bases.AirtableBase;

namespace Musoq.DataSources.Airtable;

internal interface IAirtableApi
{
    IEnumerable<IReadOnlyList<AirtableRecord>> GetRecordsChunks(IReadOnlyCollection<string> columns);

    IEnumerable<AirtableField> GetColumns(IEnumerable<string> columns);

    IEnumerable<IReadOnlyList<AirtableBase>> GetBases(IEnumerable<string> columns);

    IEnumerable<IReadOnlyList<AirtableTable>> GetTables(IEnumerable<string> columns);
}
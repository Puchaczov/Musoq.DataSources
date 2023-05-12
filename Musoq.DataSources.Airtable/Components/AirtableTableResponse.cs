using System.Text.Json.Serialization;

namespace Musoq.DataSources.Airtable.Components;

internal class AirtableTablesResponse 
{
    [JsonPropertyName("tables")]
    public AirtableTable[] Tables { get; set; }
}
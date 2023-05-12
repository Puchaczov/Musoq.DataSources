using System.Text.Json.Serialization;

namespace Musoq.DataSources.Airtable.Components;

internal class AirtableResponse
{
    [JsonPropertyName("tables")]
    public AirtableTable[] Tables { get; set; }
}
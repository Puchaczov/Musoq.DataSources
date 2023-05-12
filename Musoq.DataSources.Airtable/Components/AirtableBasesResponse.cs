using System.Text.Json.Serialization;
using Musoq.DataSources.Airtable.Sources.Bases;

namespace Musoq.DataSources.Airtable.Components;

internal class AirtableBasesResponse
{
    [JsonPropertyName("bases")]
    public AirtableBase[] Bases { get; set; }
    
    [JsonPropertyName("offset")]
    public string? Offset { get; set; }
}
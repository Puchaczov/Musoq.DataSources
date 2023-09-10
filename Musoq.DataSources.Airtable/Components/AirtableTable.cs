using System.Text.Json.Serialization;

namespace Musoq.DataSources.Airtable.Components;

internal class AirtableTable
{
    public AirtableTable(string id, string name, string primaryFieldId, string description)
    {
        Id = id;
        Name = name;
        PrimaryFieldId = primaryFieldId;
        Description = description;
        Fields = Array.Empty<AirtableField>();
    }
    
    [JsonPropertyName("fields")]
    public AirtableField[] Fields { get; set; }
    
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("primaryFieldId")]
    public string PrimaryFieldId { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
}
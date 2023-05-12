using System.Text.Json.Serialization;

namespace Musoq.DataSources.Airtable.Components;

public class AirtableTable
{
    public AirtableTable()
    {
        Fields = Array.Empty<AirtableField>();
    }
    
    public AirtableTable(string id, string name, string primaryFieldId, string description)
    {
        Id = id;
        Name = name;
        PrimaryFieldId = primaryFieldId;
        Description = description;
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
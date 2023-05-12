using System.Text.Json.Serialization;

namespace Musoq.DataSources.Airtable.Components;

public class AirtableField
{
    public AirtableField()
    {
        Id = string.Empty;
        Name = string.Empty;
        Type = string.Empty;
        Description = string.Empty;
    }
    
    public AirtableField(string id, string name, string type, string description)
    {
        Id = id;
        Name = name;
        Type = type;
        Description = description;
    }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
}
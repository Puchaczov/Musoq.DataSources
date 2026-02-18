using System.Text.Json.Serialization;

namespace Musoq.DataSources.Airtable.Sources.Bases;

public class AirtableBase
{
    public AirtableBase(string id, string name, string permissionLevel)
    {
        Id = id;
        Name = name;
        PermissionLevel = permissionLevel;
    }

    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("permissionLevel")] public string PermissionLevel { get; set; }
}
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public class PluginRegistryCompatibilityTests
{
    [TestMethod]
    public void Schema12_WithPerVersionRuntimeMetadata_ShouldDeserializeThroughLegacy11Shape()
    {
        const string registryJson = """
        {
          "schemaVersion": "1.2",
          "lastUpdated": "2026-07-20T12:00:00Z",
          "repository": "https://github.com/Puchaczov/Musoq.DataSources",
          "plugins": [
            {
              "name": "Musoq.DataSources.System",
              "shortName": "system",
              "latestVersion": "8.0.0",
              "releaseTag": "8.0.0-Musoq.DataSources.System",
              "releaseDate": "2026-06-20T12:00:00Z",
              "artifacts": {
                "windows-x64": "Musoq.DataSources.System-windows-x64.zip"
              }
            }
          ],
          "versionHistory": {
            "Musoq.DataSources.System": {
              "8.0.0": {
                "releaseTag": "8.0.0-Musoq.DataSources.System",
                "releaseDate": "2026-06-20T12:00:00Z"
              },
              "8.0.1-alpha.2": {
                "releaseTag": "8.0.1-alpha.2-Musoq.DataSources.System",
                "releaseDate": "2026-07-20T12:00:00Z",
                "channel": "alpha",
                "isPrerelease": true,
                "runtimeCompatibility": {
                  "formatVersion": 1,
                  "runtimeFamily": "musoq-runtime-v2",
                  "targetFramework": "net10.0",
                  "hostPackages": {
                    "Musoq.Schema": {
                      "minimumVersionInclusive": "17.0.2-alpha.1",
                      "maximumVersionExclusive": "18.0.0"
                    },
                    "Musoq.Plugins": {
                      "minimumVersionInclusive": "17.0.2-alpha.1",
                      "maximumVersionExclusive": "18.0.0"
                    }
                  }
                },
                "artifacts": {
                  "windows-x64": {
                    "fileName": "Musoq.DataSources.System-windows-x64.zip",
                    "sizeBytes": 123,
                    "md5": "0123456789abcdef0123456789abcdef",
                    "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
                  }
                }
              }
            }
          }
        }
        """;

        var legacyRegistry = JsonSerializer.Deserialize<LegacyRegistry>(registryJson);

        Assert.IsNotNull(legacyRegistry);
        Assert.AreEqual("1.2", legacyRegistry.SchemaVersion);
        Assert.AreEqual(1, legacyRegistry.Plugins.Count);
        Assert.AreEqual("8.0.0", legacyRegistry.Plugins[0].LatestVersion);
        Assert.AreEqual("8.0.0-Musoq.DataSources.System", legacyRegistry.Plugins[0].ReleaseTag);
        Assert.AreEqual(
            "Musoq.DataSources.System-windows-x64.zip",
            legacyRegistry.Plugins[0].Artifacts["windows-x64"]);
        Assert.AreEqual(
            "8.0.1-alpha.2-Musoq.DataSources.System",
            legacyRegistry.VersionHistory["Musoq.DataSources.System"]["8.0.1-alpha.2"].ReleaseTag);
    }

    private sealed class LegacyRegistry
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "";

        [JsonPropertyName("plugins")]
        public List<LegacyPlugin> Plugins { get; init; } = [];

        [JsonPropertyName("versionHistory")]
        public Dictionary<string, Dictionary<string, LegacyVersion>> VersionHistory { get; init; } = [];
    }

    private sealed class LegacyPlugin
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("latestVersion")]
        public string LatestVersion { get; init; } = "";

        [JsonPropertyName("releaseTag")]
        public string ReleaseTag { get; init; } = "";

        [JsonPropertyName("artifacts")]
        public Dictionary<string, string> Artifacts { get; init; } = [];
    }

    private sealed class LegacyVersion
    {
        [JsonPropertyName("releaseTag")]
        public string ReleaseTag { get; init; } = "";

        [JsonPropertyName("releaseDate")]
        public DateTimeOffset ReleaseDate { get; init; }
    }
}

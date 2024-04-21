using Musoq.Schema;

namespace Musoq.DataSources.Ollama;

internal static class OllamaSchemaHelper
{
    public static readonly IDictionary<string, int> NameToIndexMap;
    public static readonly IDictionary<int, Func<OllamaEntity, object>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static OllamaSchemaHelper()
    {
        NameToIndexMap = new Dictionary<string, int>();
        IndexToMethodAccessMap = new Dictionary<int, Func<OllamaEntity, object>>();
        Columns = Array.Empty<ISchemaColumn>();
    }
}
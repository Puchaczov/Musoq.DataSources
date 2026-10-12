using Musoq.Schema;

namespace Musoq.DataSources.Ollama;

internal static class OllamaSchemaHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly ISchemaColumn[] Columns;

    static OllamaSchemaHelper()
    {
        NameToIndexMap = new Dictionary<string, int>();

        Columns = [];
    }
}
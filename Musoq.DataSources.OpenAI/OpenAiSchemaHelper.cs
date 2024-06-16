using Musoq.Schema;

namespace Musoq.DataSources.OpenAI;

internal static class OpenAiSchemaHelper
{
    public static readonly IDictionary<string, int> NameToIndexMap;
    public static readonly IDictionary<int, Func<OpenAiEntity, object>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static OpenAiSchemaHelper()
    {
        NameToIndexMap = new Dictionary<string, int>();
        IndexToMethodAccessMap = new Dictionary<int, Func<OpenAiEntity, object>>();
        Columns = [];
    }
}
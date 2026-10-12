using System.Text;
using System.Text.Json;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Json;

/// <summary>
///     Json helper methods
/// </summary>
public class JsonLibrary : LibraryBase
{
    /// <summary>Returns the JSON text of a concretely typed JSON value.</summary>
    public string JsonText(JsonElement value) => value.GetRawText();

    /// <summary>Returns JSON text, preserving null or missing structured values.</summary>
    [BindableMethod]
    public string JsonText(JsonElement? value) => value.HasValue ? value.Value.GetRawText() : null;

    /// <summary>Returns the number of elements in a concretely typed JSON array.</summary>
    public int Length(JsonElement array) => array.GetArrayLength();

    /// <summary>Returns an array length, preserving null or missing structured values.</summary>
    [BindableMethod]
    public int? Length(JsonElement? array) => array.HasValue ? array.Value.GetArrayLength() : null;

    /// <summary>Flattens a concretely typed JSON array into comma-separated values.</summary>
    public string MakeFlat(JsonElement array)
    {
        var result = new StringBuilder();
        var first = true;
        foreach (var value in array.EnumerateArray())
        {
            if (!first)
                result.Append(", ");
            result.Append(value.ToString());
            first = false;
        }
        return result.ToString();
    }

    /// <summary>Flattens an array, preserving null or missing structured values.</summary>
    [BindableMethod]
    public string MakeFlat(JsonElement? array) => array.HasValue ? MakeFlat(array.Value) : null;

}

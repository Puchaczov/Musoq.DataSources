using System.Dynamic;
using System.Text.Json;
using Musoq.DataSources.Airtable.Components;
using Newtonsoft.Json.Linq;

namespace Musoq.DataSources.Airtable.Helpers;

internal static class TypeMappingHelpers
{
    public static readonly IReadOnlyDictionary<AirtableType, Type> Mapping = new Dictionary<AirtableType, Type>()
    {
        { AirtableType.SingleLineText, typeof(string) },
        { AirtableType.Email, typeof(string) },
        { AirtableType.Url, typeof(string) },
        { AirtableType.MultilineText, typeof(string) },
        { AirtableType.Number, typeof(decimal) },
        { AirtableType.Percent, typeof(decimal) },
        { AirtableType.Currency, typeof(decimal) },
        { AirtableType.SingleSelect, typeof(string) },
        { AirtableType.MultipleSelects, typeof(List<string>) },
        { AirtableType.SingleCollaborator, typeof(ExpandoObject) },
        { AirtableType.MultipleCollaborators, typeof(List<ExpandoObject>) },
        { AirtableType.MultipleRecordLinks, typeof(List<ExpandoObject>) },
        { AirtableType.Date, typeof(string) },
        { AirtableType.DateTime, typeof(string) },
        { AirtableType.PhoneNumber, typeof(string) },
        { AirtableType.Checkbox, typeof(bool) },
        { AirtableType.Formula, typeof(object) }, // depends on the result of the formula
        { AirtableType.CreatedTime, typeof(DateTime) },
        { AirtableType.Rollup, typeof(object) }, // depends on the result of the rollup
        { AirtableType.Count, typeof(int) },
        { AirtableType.Lookup, typeof(object) }, // typically a List of a certain type
        { AirtableType.MultipleAttachments, typeof(List<ExpandoObject>)},
        { AirtableType.MultipleLookupValues, typeof(List<ExpandoObject>) }, // depends on the lookup value type
        { AirtableType.AutoNumber, typeof(int) },
        { AirtableType.Barcode, typeof(string) },
        { AirtableType.Rating, typeof(int) },
        { AirtableType.RichText, typeof(string) },
        { AirtableType.Duration, typeof(string) },
        { AirtableType.LastModifiedTime, typeof(string) },
        { AirtableType.Button, typeof(object) }, // depends on the usage
        { AirtableType.CreatedBy, typeof(string) },
        { AirtableType.LastModifiedBy, typeof(string) },
        { AirtableType.ExternalSyncSource, typeof(object) }, // depends on the external source
    };

    public static void MapFromJsonElement(IDictionary<string, object> fields, string key, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var rawTextObject = element.GetRawText();
                var jObject = JObject.Parse(rawTextObject);
                fields[key] = jObject;
                break;
            case JsonValueKind.Array:
                var rawTextArray = element.GetRawText();
                var jArray = JArray.Parse(rawTextArray);
                fields[key] = jArray;
                break;
            case JsonValueKind.Undefined:
                break;
            case JsonValueKind.String:
                fields[key] = element.GetString();
                break;
            case JsonValueKind.Number:
                fields[key] = element.GetDecimal();
                break;
            case JsonValueKind.True:
                fields[key] = true;
                break;
            case JsonValueKind.False:
                fields[key] = false;
                break;
            case JsonValueKind.Null:
                fields[key] = null;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
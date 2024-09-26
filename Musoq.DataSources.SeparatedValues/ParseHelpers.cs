using System;
using System.Collections.Generic;
using System.Globalization;

namespace Musoq.DataSources.SeparatedValues;

internal static class ParseHelpers
{
    public static object?[] ParseRecords(IReadOnlyDictionary<string, Type> types, string?[] rawRow, IReadOnlyDictionary<int, string> indexToNameMap)
    {
        var parsedRecords = new object?[rawRow.Length];
        
        for (var i = 0; i < rawRow.Length; ++i)
        {
            var headerName = indexToNameMap[i];
            if (types.TryGetValue(headerName, out var type))
            {
                var colValue = rawRow[i];
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        if (bool.TryParse(colValue, out var boolValue))
                            parsedRecords[i] = boolValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Byte:
                        if (byte.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var byteValue))
                            parsedRecords[i] = byteValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Char:
                        if (char.TryParse(colValue, out var charValue))
                            parsedRecords[i] = charValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.DateTime:
                        if (DateTime.TryParse(colValue, CultureInfo.CurrentCulture, DateTimeStyles.None,
                                out var dateTimeValue))
                            parsedRecords[i] = dateTimeValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.DBNull:
                        throw new NotSupportedException($"Type {TypeCode.DBNull} is not supported.");
                    case TypeCode.Decimal:
                        if (decimal.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture,
                                out var decimalValue))
                            parsedRecords[i] = decimalValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Double:
                        if (double.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture,
                                out var doubleValue))
                            parsedRecords[i] = doubleValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Empty:
                        throw new NotSupportedException($"Type {TypeCode.Empty} is not supported.");
                    case TypeCode.Int16:
                        if (short.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var shortValue))
                            parsedRecords[i] = shortValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Int32:
                        if (int.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var intValue))
                            parsedRecords[i] = intValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Int64:
                        if (long.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var longValue))
                            parsedRecords[i] = longValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Object:
                        throw new NotSupportedException($"Type {TypeCode.Object} is not supported.");
                    case TypeCode.SByte:
                        if (sbyte.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var sbyteValue))
                            parsedRecords[i] = sbyteValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.Single:
                        if (float.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var floatValue))
                            parsedRecords[i] = floatValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.String:
                        if (string.IsNullOrEmpty(colValue))
                            parsedRecords[i] = null;
                        else
                            parsedRecords[i] = colValue;
                        break;
                    case TypeCode.UInt16:
                        if (ushort.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture,
                                out var ushortValue))
                            parsedRecords[i] = ushortValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.UInt32:
                        if (uint.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var uintValue))
                            parsedRecords[i] = uintValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    case TypeCode.UInt64:
                        if (ulong.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var ulongValue))
                            parsedRecords[i] = ulongValue;
                        else
                            parsedRecords[i] = null;
                        break;
                    default:
                        throw new NotSupportedException($"Type {type} is not supported.");
                }
            }
            else
            {
                parsedRecords[i] = rawRow[i];
            }
        }

        return parsedRecords;
    }
}
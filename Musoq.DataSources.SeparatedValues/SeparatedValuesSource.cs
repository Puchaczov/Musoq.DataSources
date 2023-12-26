using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;

namespace Musoq.DataSources.SeparatedValues
{
    internal class SeparatedValuesSource : RowSourceBase<object[]>
    {
        private readonly SeparatedValueFile[] _files;
        
        public RuntimeContext? RuntimeContext { get; init; }

        public SeparatedValuesSource(string filePath, string separator, bool hasHeader, int skipLines)
        {
            _files = new[] {
                new SeparatedValueFile()
                {
                    FilePath = filePath,
                    HasHeader = hasHeader,
                    Separator = separator,
                    SkipLines = skipLines
                }
            };
        }

        public SeparatedValuesSource(IReadOnlyTable table, string separator)
        {
            _files = new SeparatedValueFile[table.Count];

            for (int i = 0; i < table.Count; ++i)
            {
                var row = table.Rows[i];
                _files[i] = new SeparatedValueFile()
                {
                    FilePath = (string)row[0],
                    Separator = separator,
                    HasHeader = (bool)row[1],
                    SkipLines = (int)row[2]
                };
            }
        }

        protected override void CollectChunks(BlockingCollection<IReadOnlyList<Musoq.Schema.DataSources.IObjectResolver>> chunkedSource)
        {
            foreach (var csvFile in _files)
            {
                ProcessFile(csvFile, chunkedSource);
            }
        }

        private void ProcessFile(SeparatedValueFile csvFile, BlockingCollection<IReadOnlyList<Musoq.Schema.DataSources.IObjectResolver>> chunkedSource)
        {
            if (RuntimeContext is null)
            {
                throw new InvalidOperationException("Runtime context is not set.");
            }
            
            if (csvFile.FilePath is null)
            {
                throw new InvalidOperationException("File path cannot be null.");
            }
            
            if (csvFile.Separator is null)
            {
                throw new InvalidOperationException("Separator cannot be null.");
            }
            
            var file = new FileInfo(csvFile.FilePath);

            if (!file.Exists)
            {
                chunkedSource.Add(new List<EntityResolver<object[]>>());
                return;
            }

            var nameToIndexMap = new Dictionary<string, int>();
            var indexToMethodAccess = new Dictionary<int, Func<object?[], object?>>();
            var indexToNameMap = new Dictionary<int, string>();
            var endWorkToken = RuntimeContext.EndWorkToken;

            var modifiedCulture = new CultureInfo(CultureInfo.CurrentCulture.Name)
            {
                TextInfo =
                {
                    ListSeparator = csvFile.Separator
                }
            };

            using (var stream = CreateStreamFromFile(file))
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    SkipLines(reader, csvFile);
                    
                    using (var csvReader = new CsvReader(reader,  modifiedCulture))
                    {
                        csvReader.Read();

                        var header = csvReader.Context.Parser.Record;

                        if (header == null)
                            throw new NotSupportedException(
                                "File has no header or no data. Please check if file is not empty.");

                        for (var i = 0; i < header.Length; ++i)
                        {
                            var headerName = csvFile.HasHeader ? SeparatedValuesHelper.MakeHeaderNameValidColumnName(header[i]) : string.Format(SeparatedValuesHelper.AutoColumnName, i + 1);
                            nameToIndexMap.Add(headerName, i);
                            indexToNameMap.Add(i, headerName);
                            var i1 = i;
                            indexToMethodAccess.Add(i, row => row[i1]);
                        }
                    }
                }
            }

            using (var stream = CreateStreamFromFile(file))
            {
                using (var reader = new StreamReader(stream))
                {
                    SkipLines(reader, csvFile);

                    using (var csvReader = new CsvReader(reader, new CsvConfiguration(modifiedCulture) { BadDataFound = _ => {} }))
                    {

                        int i = 1, j = 11;
                        var list = new List<EntityResolver<object?[]>>(100);
                        var rowsToRead = 1000;
                        const int rowsToReadBase = 100;

                        if (csvFile.HasHeader)
                            csvReader.Read(); //skip header.

                        while (csvReader.Read())
                        {
                            var rawRow = csvReader.Context.Parser.Record;
                            
                            if (rawRow == null)
                                continue;
                            
                            list.Add(new EntityResolver<object?[]>(ParseRecords(rawRow, indexToNameMap), nameToIndexMap, indexToMethodAccess));

                            if (i++ < rowsToRead) continue;

                            i = 1;

                            if (j > 1)
                                j -= 1;

                            rowsToRead = rowsToReadBase * j;

                            chunkedSource.Add(list, endWorkToken);
                            list = new List<EntityResolver<object?[]>>(rowsToRead);
                        }

                        chunkedSource.Add(list, endWorkToken);
                    }
                }
            }
        }

        private object?[] ParseRecords(string?[] rawRow, IReadOnlyDictionary<int, string> indexToNameMap)
        {
            if (RuntimeContext is null)
            {
                throw new InvalidOperationException("Runtime context is not set.");
            }
            
            var parsedRecords = new object?[rawRow.Length];
            var types = RuntimeContext
                .AllColumns
                .ToDictionary(
                    col => col.ColumnName,
                    col => col.ColumnType.GetUnderlyingNullable());

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
                            if (DateTime.TryParse(colValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dateTimeValue))
                                parsedRecords[i] = dateTimeValue;
                            else
                                parsedRecords[i] = null;
                            break;
                        case TypeCode.DBNull:
                            throw new NotSupportedException($"Type {TypeCode.DBNull} is not supported.");
                        case TypeCode.Decimal:
                            if (decimal.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var decimalValue))
                                parsedRecords[i] = decimalValue;
                            else
                                parsedRecords[i] = null;
                            break;
                        case TypeCode.Double:
                            if (double.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var doubleValue))
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
                            if (ushort.TryParse(colValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var ushortValue))
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
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    parsedRecords[i] = rawRow[i];
                }
            }

            return parsedRecords;
        }

        private void SkipLines(TextReader reader, SeparatedValueFile csvFile)
        {
            if (csvFile.SkipLines <= 0) return;

            var skippedLines = 0;
            while (skippedLines < csvFile.SkipLines)
            {
                reader.ReadLine();
                skippedLines += 1;
            }
        }

        private Stream CreateStreamFromFile(FileInfo file)
        {
            Stream stream;
            if (SizeConverter.ToMegabytes(file.Length) > Performance.FreeMemoryInMegabytes())
                stream = file.OpenRead();
            else
                stream = new MemoryStream(Encoding.UTF8.GetBytes(file.OpenText().ReadToEnd()));

            return stream;
        }
        
        private class SeparatedValueFile
        {
            public string? FilePath { get; set; }

            public string? Separator { get; set; }

            public bool HasHeader { get; set; }

            public int SkipLines { get; set; }
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;

namespace Musoq.DataSources.SeparatedValues
{
    internal class SeparatedValuesSource : AsyncRowsSourceBase<object[]>
    {
        private readonly SeparatedValueFile[] _files;
        private const int BufferSize = 65536; // 64KB buffer
        private const int ChunkSize = 100000; // Number of rows per chunk
        
        public RuntimeContext? RuntimeContext { get; init; }

        public SeparatedValuesSource(string filePath, string separator, bool hasHeader, int skipLines, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            _files =
            [
                new SeparatedValueFile
                {
                    FilePath = filePath,
                    HasHeader = hasHeader,
                    Separator = separator,
                    SkipLines = skipLines
                }
            ];
        }

        public SeparatedValuesSource(IReadOnlyTable table, string separator, CancellationToken cancellationToken)
            : base(cancellationToken)
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

        protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<Musoq.Schema.DataSources.IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
        {
            await Parallel.ForEachAsync(_files, cancellationToken, async (file, loopToken) => await ProcessFileAsync(file, chunkedSource, loopToken));
        }

        private async Task ProcessFileAsync(SeparatedValueFile csvFile, BlockingCollection<IReadOnlyList<Musoq.Schema.DataSources.IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
        {
            if (RuntimeContext is null)
                throw new InvalidOperationException("Runtime context is not set.");

            if (csvFile.FilePath is null)
                throw new InvalidOperationException("File path cannot be null.");

            if (csvFile.Separator is null)
                throw new InvalidOperationException("Separator cannot be null.");

            var file = new FileInfo(csvFile.FilePath);

            if (!file.Exists)
            {
                chunkedSource.Add(new List<EntityResolver<object[]>>(), cancellationToken);
                return;
            }

            var nameToIndexMap = new Dictionary<string, int>();
            var indexToMethodAccess = new Dictionary<int, Func<object?[], object?>>();
            var indexToNameMap = new Dictionary<int, string>();
            var endWorkToken = RuntimeContext.EndWorkToken;

            var modifiedCulture = new CultureInfo(CultureInfo.CurrentCulture.Name)
            {
                TextInfo = { ListSeparator = csvFile.Separator }
            };

            // Process header
            await ProcessHeaderAsync(file, csvFile, nameToIndexMap, indexToMethodAccess, indexToNameMap, modifiedCulture);

            // Process data
            await ProcessDataAsync(file, csvFile, chunkedSource, nameToIndexMap, indexToMethodAccess, indexToNameMap, modifiedCulture, endWorkToken);
        }

        private static async Task ProcessHeaderAsync(FileInfo file, SeparatedValueFile csvFile, Dictionary<string, int> nameToIndexMap, 
            Dictionary<int, Func<object?[], object?>> indexToMethodAccess, Dictionary<int, string> indexToNameMap, CultureInfo modifiedCulture)
        {
            await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, BufferSize);
            
            await SkipLinesAsync(reader, csvFile.SkipLines);

            using var csvReader = new CsvReader(reader, modifiedCulture);
            await csvReader.ReadAsync();

            var header = csvReader.Context.Parser!.Record;

            if (header == null)
                throw new NotSupportedException("File has no header or no data. Please check if file is not empty.");

            for (var i = 0; i < header.Length; ++i)
            {
                var headerName = csvFile.HasHeader ? SeparatedValuesHelper.MakeHeaderNameValidColumnName(header[i]) : string.Format(SeparatedValuesHelper.AutoColumnName, i + 1);
                nameToIndexMap.Add(headerName, i);
                indexToNameMap.Add(i, headerName);
                var i1 = i;
                indexToMethodAccess.Add(i, row => row[i1]);
            }
        }

        private async Task ProcessDataAsync(FileInfo file, SeparatedValueFile csvFile, 
            BlockingCollection<IReadOnlyList<Musoq.Schema.DataSources.IObjectResolver>> chunkedSource,
            Dictionary<string, int> nameToIndexMap, Dictionary<int, Func<object?[], object?>> indexToMethodAccess, 
            Dictionary<int, string> indexToNameMap, CultureInfo modifiedCulture,  CancellationToken endWorkToken)
        {
            await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, BufferSize);
            
            await SkipLinesAsync(reader, csvFile.SkipLines);

            using var csvReader = new CsvReader(reader, new CsvConfiguration(modifiedCulture) { BadDataFound = _ => { } });

            if (csvFile.HasHeader)
                await csvReader.ReadAsync(); // Skip header

            var chunk = new List<EntityResolver<object?[]>>(ChunkSize);

            while (await csvReader.ReadAsync())
            {
                if (endWorkToken.IsCancellationRequested)
                    break;

                var rawRow = csvReader.Context.Parser!.Record;
                
                if (rawRow == null)
                    continue;
                
                chunk.Add(new EntityResolver<object?[]>(ParseRecords(rawRow, indexToNameMap), nameToIndexMap, indexToMethodAccess));

                if (chunk.Count >= ChunkSize)
                {
                    chunkedSource.Add(chunk, endWorkToken);
                    chunk = new List<EntityResolver<object?[]>>(ChunkSize);
                }
            }

            if (chunk.Count > 0)
            {
                chunkedSource.Add(chunk, endWorkToken);
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

        private static async Task SkipLinesAsync(TextReader reader, int linesToSkip)
        {
            for (var i = 0; i < linesToSkip; i++)
            {
                await reader.ReadLineAsync();
            }
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
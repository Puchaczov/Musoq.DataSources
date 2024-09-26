using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    internal class SeparatedValuesFromFileRowsSource : AsyncRowsSourceBase<object[]>
    {
        private readonly SeparatedValueInfo[] _files;
        private const int BufferSize = 65536; // 64KB buffer
        private const int ChunkSize = 100000; // Number of rows per chunk
        
        public RuntimeContext? RuntimeContext { get; init; }

        public SeparatedValuesFromFileRowsSource(string filePath, string separator, bool hasHeader, int skipLines, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            _files =
            [
                new SeparatedValueInfo
                {
                    FilePath = filePath,
                    HasHeader = hasHeader,
                    Separator = separator,
                    SkipLines = skipLines
                }
            ];
        }

        public SeparatedValuesFromFileRowsSource(IReadOnlyTable table, string separator, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            _files = new SeparatedValueInfo[table.Count];

            for (var i = 0; i < table.Count; ++i)
            {
                var row = table.Rows[i];
                _files[i] = new SeparatedValueInfo
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

        private async Task ProcessFileAsync(SeparatedValueInfo csvFile, BlockingCollection<IReadOnlyList<Musoq.Schema.DataSources.IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
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
            var indexToMethodAccessMap = new Dictionary<int, Func<object?[], object?>>();
            var indexToNameMap = new Dictionary<int, string>();
            var endWorkToken = RuntimeContext.EndWorkToken;

            var modifiedCulture = new CultureInfo(CultureInfo.CurrentCulture.Name)
            {
                TextInfo = { ListSeparator = csvFile.Separator }
            };

            // Process header
            await ProcessHeaderAsync(file, csvFile, nameToIndexMap, indexToMethodAccessMap, indexToNameMap, modifiedCulture);

            // Process data
            await ProcessDataAsync(file, csvFile, chunkedSource, nameToIndexMap, indexToMethodAccessMap, indexToNameMap, modifiedCulture, endWorkToken);
        }

        private static async Task ProcessHeaderAsync(
            FileInfo file, 
            SeparatedValueInfo csvFile, 
            Dictionary<string, int> nameToIndexMap, 
            Dictionary<int, Func<object?[], object?>> indexToMethodAccess, 
            Dictionary<int, string> indexToNameMap, 
            CultureInfo modifiedCulture
        )
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

        private async Task ProcessDataAsync(FileInfo file, SeparatedValueInfo csvFile, 
            BlockingCollection<IReadOnlyList<Musoq.Schema.DataSources.IObjectResolver>> chunkedSource,
            Dictionary<string, int> nameToIndexMap, Dictionary<int, Func<object?[], object?>> indexToMethodAccess, 
            Dictionary<int, string> indexToNameMap, CultureInfo modifiedCulture,  CancellationToken endWorkToken
        )
        {
            if (RuntimeContext is null)
            {
                throw new InvalidOperationException("Runtime context is not set.");
            }
            
            var types = RuntimeContext.AllColumns.ToDictionary(
                col => col.ColumnName,
                col => col.ColumnType.GetUnderlyingNullable());
            
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
                
                if (rawRow is null)
                    continue;
                
                chunk.Add(new EntityResolver<object?[]>(ParseHelpers.ParseRecords(types, rawRow, indexToNameMap), nameToIndexMap, indexToMethodAccess));

                if (chunk.Count < ChunkSize) continue;
                
                chunkedSource.Add(chunk, endWorkToken);
                chunk = new List<EntityResolver<object?[]>>(ChunkSize);
            }

            if (chunk.Count > 0)
            {
                chunkedSource.Add(chunk, endWorkToken);
            }
        }

        private static async Task SkipLinesAsync(TextReader reader, int linesToSkip)
        {
            for (var i = 0; i < linesToSkip; i++)
            {
                await reader.ReadLineAsync();
            }
        }
        
        private class SeparatedValueInfo
        {
            public string? FilePath { get; set; }

            public string? Separator { get; set; }

            public bool HasHeader { get; set; }

            public int SkipLines { get; set; }
        }
    }
}
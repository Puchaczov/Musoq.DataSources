using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.FlatFile
{
    internal class FlatFileSource : RowSourceBase<FlatFileEntity>
    {
        private const string FlatFileSourceName = "flatfile";
        private readonly string _filePath;
        private readonly RuntimeContext _communicator;

        public FlatFileSource(string filePath, RuntimeContext communicator)
        {
            _filePath = filePath;
            _communicator = communicator;
        }

        protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
        {
            _communicator.ReportDataSourceBegin(FlatFileSourceName);
            long totalRowsProcessed = 0;
            
            try
            {
                const int chunkSize = 1000;

                if (!File.Exists(_filePath))
                    return;

                var rowNum = 0;
                var endWorkToken = _communicator.EndWorkToken;

                using var file = File.OpenRead(_filePath);
                using var reader = new StreamReader(file);
                var list = new List<EntityResolver<FlatFileEntity>>();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var entity = new FlatFileEntity
                    {
                        Line = line,
                        LineNumber = ++rowNum
                    };

                    list.Add(new EntityResolver<FlatFileEntity>(entity, FlatFileHelper.FlatNameToIndexMap,
                        FlatFileHelper.FlatIndexToMethodAccessMap));

                    totalRowsProcessed++;

                    if (rowNum <= chunkSize)
                        continue;

                    rowNum = 0;
                    chunkedSource.Add(list, endWorkToken);

                    list = new List<EntityResolver<FlatFileEntity>>(chunkSize);
                }

                chunkedSource.Add(list, endWorkToken);
            }
            finally
            {
                _communicator.ReportDataSourceEnd(FlatFileSourceName, totalRowsProcessed);
            }
        }
    }
}
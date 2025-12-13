using System.Collections.Generic;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using SharpCompress.Readers;

namespace Musoq.DataSources.Archives;

internal class ArchivesRowSource(string path, RuntimeContext runtimeContext) : RowSource
{
    private const string ArchivesSourceName = "archives";
    private readonly Stream _stream = File.OpenRead(path);

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            runtimeContext.ReportDataSourceBegin(ArchivesSourceName);
            long totalRowsProcessed = 0;
            
            try
            {
                using var stream = _stream;
                using var reader = ReaderFactory.Open(stream, new ReaderOptions
                {
                    LeaveStreamOpen = true
                });

                var index = 0;
            
                while (reader.MoveToNextEntry())
                {
                    totalRowsProcessed++;
                    yield return new EntityResolver<EntryWrapper>(
                        new EntryWrapper(reader.Entry, path, index++), 
                        EntryWrapper.NameToIndexMap,
                        EntryWrapper.IndexToMethodAccessMap);
                }
            }
            finally
            {
                runtimeContext.ReportDataSourceEnd(ArchivesSourceName, totalRowsProcessed);
            }
        }
    }
}
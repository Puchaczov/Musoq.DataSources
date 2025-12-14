using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Zip;

internal class ZipSource : RowSource
{
    private const string ZipSourceName = "zip";
    private readonly string _zipPath;
    private readonly RuntimeContext _runtimeContext;

    public ZipSource(string zipPath, RuntimeContext runtimeContext)
    {
        _zipPath = zipPath;
        _runtimeContext = runtimeContext;
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            _runtimeContext.ReportDataSourceBegin(ZipSourceName);
            long totalRowsProcessed = 0;
            
            try
            {
                var endWorkToken = _runtimeContext.EndWorkToken;
                using var file = File.OpenRead(_zipPath);
                using var zip = new ZipArchive(file);
                
                // We know the total count upfront
                _runtimeContext.ReportDataSourceRowsKnown(ZipSourceName, zip.Entries.Count);
                
                foreach (var entry in zip.Entries)
                {
                    endWorkToken.ThrowIfCancellationRequested();
                    if (entry.Name != string.Empty)
                    {
                        totalRowsProcessed++;
                        yield return new EntityResolver<ZipArchiveEntry>(
                            entry,
                            SchemaZipHelper.NameToIndexMap,
                            SchemaZipHelper.IndexToMethodAccessMap);
                    }
                }
            }
            finally
            {
                _runtimeContext.ReportDataSourceEnd(ZipSourceName, totalRowsProcessed);
            }
        }
    }
}
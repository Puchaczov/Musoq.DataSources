using System.Collections.Generic;
using System.IO;
using Musoq.Schema.DataSources;
using SharpCompress.Readers;

namespace Musoq.DataSources.Archives;

internal class ArchivesRowSource(string path) : RowSource
{
    private readonly Stream _stream = File.OpenRead(path);

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            using var stream = _stream;
            using var reader = ReaderFactory.Open(stream, new ReaderOptions
            {
                LeaveStreamOpen = true
            });

            var index = 0;
            
            while (reader.MoveToNextEntry())
            {
                yield return new EntityResolver<EntryWrapper>(
                    new EntryWrapper(reader.Entry, path, index++), 
                    EntryWrapper.NameToIndexMap,
                    EntryWrapper.IndexToMethodAccessMap);
            }
        }
    }
}
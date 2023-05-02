using System.Collections.Generic;
using System.IO;
using Musoq.Schema.DataSources;
using SharpCompress.Readers;

namespace Musoq.DataSources.Archives;

public class ArchivesRowSource : RowSource
{
    private readonly Stream _stream;

    public ArchivesRowSource(string path)
    {
        _stream = File.OpenRead(path);
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            using var stream = _stream;
            using var reader = ReaderFactory.Open(stream);
            
            while (reader.MoveToNextEntry())
            {
                yield return new EntityResolver<EntryWrapper>(
                    new EntryWrapper(reader.Entry, reader), 
                    EntryWrapper.NameToIndexMap,
                    EntryWrapper.IndexToMethodAccessMap);
            }
        }
    }
}
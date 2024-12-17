using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Os.Files;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Tests.Utils
{
    internal class TestFilesSource(string path, bool useSubDirectories, RuntimeContext communicator)
        : FilesSource(path, useSubDirectories, communicator)
    {
        private readonly RuntimeContext _communicator = communicator;

        public IReadOnlyList<EntityResolver<FileEntity>> GetFiles()
        {
            var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            CollectChunksAsync(collection, _communicator.EndWorkToken).Wait();

            var list = new List<EntityResolver<FileEntity>>();

            foreach(var item in collection)
                list.AddRange(item.Select(file => (EntityResolver<FileEntity>)file));

            return list;
        }
    }
}

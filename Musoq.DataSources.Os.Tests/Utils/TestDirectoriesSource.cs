using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.DataSources.Os.Directories;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Tests.Utils
{
    class TestDirectoriesSource : DirectoriesSource
    {
        public TestDirectoriesSource(string path, bool recursive, RuntimeContext communicator) 
            : base(path, recursive, communicator)
        {
        }

        public IReadOnlyList<EntityResolver<DirectoryInfo>> GetDirectories()
        {
            var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            CollectChunks(collection);

            var list = new List<EntityResolver<DirectoryInfo>>();

            foreach (var item in collection)
                list.AddRange(item.Select(dir => (EntityResolver<DirectoryInfo>)dir));

            return list;
        }
    }
}

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Musoq.DataSources.Os.Directories;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Tests.Utils
{
    class TestDirectoriesSource(string path, bool recursive, RuntimeContext context)
        : DirectoriesSource(path, recursive, context)
    {
        public IReadOnlyList<EntityResolver<DirectoryInfo>> GetDirectories()
        {
            var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            CollectChunksAsync(collection, CancellationToken.None).Wait();

            var list = new List<EntityResolver<DirectoryInfo>>();

            foreach (var item in collection)
                list.AddRange(item.Select(dir => (EntityResolver<DirectoryInfo>)dir));

            return list;
        }
    }
}

﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Os.Files;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Tests.Utils
{
    class TestFilesSource : FilesSource
    {
        public TestFilesSource(string path, bool useSubDirectories, RuntimeContext communicator) 
            : base(path, useSubDirectories, communicator)
        {
        }

        public IReadOnlyList<EntityResolver<ExtendedFileInfo>> GetFiles()
        {
            var collection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
            CollectChunks(collection);

            var list = new List<EntityResolver<ExtendedFileInfo>>();

            foreach(var item in collection)
                list.AddRange(item.Select(file => (EntityResolver<ExtendedFileInfo>)file));

            return list;
        }
    }
}

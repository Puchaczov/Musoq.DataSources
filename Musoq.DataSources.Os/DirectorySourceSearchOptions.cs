﻿namespace Musoq.DataSources.Os
{
    public class DirectorySourceSearchOptions
    {
        public DirectorySourceSearchOptions(string path, bool useSubDirectories)
        {
            Path = path;
            WithSubDirectories = useSubDirectories;
        }

        public string Path { get; }

        public bool WithSubDirectories { get; }
    }
}
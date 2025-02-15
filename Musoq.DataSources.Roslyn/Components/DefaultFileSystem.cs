using System.IO;

namespace Musoq.DataSources.Roslyn.Components
{
    internal sealed class DefaultFileSystem : IFileSystem
    {
        public bool Exists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
    }
}

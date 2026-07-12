using System.IO;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class FileAttributeEntity(FileAttributes attribute)
{
    public string Name => attribute.ToString();
    public int Value => (int)attribute;
}

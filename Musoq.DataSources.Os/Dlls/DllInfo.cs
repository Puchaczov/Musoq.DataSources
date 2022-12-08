using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Musoq.DataSources.Os.Dlls
{
    public class DllInfo
    {
        public FileInfo FileInfo { get; set; }

        public Assembly Assembly { get; set; }

        public FileVersionInfo Version { get; set; }
    }
}

using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Os;

internal static class OsNativePatterns
{
    internal static readonly PatternColumnBindings<string> Files = new(
        ("Name", Path.GetFileName), ("FileName", Path.GetFileName),
        ("Extension", Path.GetExtension), ("FullPath", static path => path),
        ("DirectoryPath", Path.GetDirectoryName),
        ("DirectoryName", static path => Path.GetDirectoryName(Path.GetDirectoryName(path))));
    internal static readonly PatternColumnBindings<string> Directories = new(
        ("Name", static path => Path.GetFileName(Path.TrimEndingDirectorySeparator(path))),
        ("FullName", static path => path), ("Extension", Path.GetExtension),
        ("Root", Path.GetPathRoot));
    internal static readonly PatternColumnBindings<string> Metadata = new(("FullName", static path => path));
    internal static IReadOnlyCollection<string>? Columns(string source) => source switch
    {
        "file" or "files" => Files.Names,
        "directories" => Directories.Names,
        "metadata" => Metadata.Names,
        _ => null
    };
}

using System.IO;
using Musoq.DataSources.Os.Files;

namespace Musoq.DataSources.Os.Compare.Directories;

internal class CompareDirectoriesResult(
    DirectoryInfo sourceRoot,
    FileEntity? sourceFile,
    DirectoryInfo destinationRoot,
    FileEntity? destinationFile,
    State state)
{
    public DirectoryInfo SourceRoot { get; } = sourceRoot;

    public FileEntity? SourceFile { get; } = sourceFile;

    public string? SourceFileRelative => SourceFile?.FullPath.Replace(SourceRoot.FullName, string.Empty);

    public DirectoryInfo DestinationRoot { get; } = destinationRoot;

    public FileEntity? DestinationFile { get; } = destinationFile;

    public string? DestinationFileRelative => DestinationFile?.FullPath.Replace(DestinationRoot.FullName, string.Empty);

    public State State { get; } = state;
}
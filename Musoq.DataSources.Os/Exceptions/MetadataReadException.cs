using System;
using System.IO;

namespace Musoq.DataSources.Os.Exceptions;

/// <summary>
///     Exception thrown when metadata could not be read from file.
/// </summary>
/// <param name="fileInfo">The file info.</param>
/// <param name="innerException">The inner exception.</param>
public class MetadataReadException(FileInfo fileInfo, Exception innerException)
    : AggregateException($"Could not read metadata from file {fileInfo.FullName}.", innerException);
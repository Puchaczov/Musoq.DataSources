#nullable enable

using System;
using System.IO;
using System.Threading;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Bytes;

internal static class SearchBinaryPolicy
{
    public const char BinaryMarker = '\0';

    public static bool IsBinaryFile(
        string path,
        SearchEncodingMode encodingMode,
        SearchCharBuffer buffer,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(buffer);

        TextReader reader;
        try
        {
            reader = SearchTextReader.Open(path, encodingMode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ISearchDiagnosticException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(path),
                exception);
        }

        return IsBinaryReader(path, reader, buffer, cancellationToken);
    }

    internal static bool IsBinaryBytes(
        byte[] bytes,
        int length,
        SearchEncodingMode encodingMode,
        SearchCharBuffer buffer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(buffer);
        using var reader = SearchTextReader.Open(bytes, length, encodingMode);
        return IsBinaryReader("<memory>", reader, buffer, cancellationToken);
    }

    private static bool IsBinaryReader(
        string path,
        TextReader reader,
        SearchCharBuffer buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            using (reader)
            {
                var characters = buffer.Array;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var charsRead = reader.Read(characters, 0, buffer.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (charsRead == 0)
                        return false;

                    if (characters.AsSpan(0, charsRead).Contains(BinaryMarker))
                        return true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ISearchDiagnosticException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SearchSourceReadException(
                SearchDiagnosticCatalog.SourceReadFailed(path),
                exception);
        }
    }
}

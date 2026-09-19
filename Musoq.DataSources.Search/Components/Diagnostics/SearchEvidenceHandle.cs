#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Traversal;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Diagnostics;

/// <summary>
///     Retains only an immutable source identity and re-reads bounded evidence on demand.
/// </summary>
internal sealed class SearchEvidenceHandle
{
    private readonly SearchSourceVersion _sourceVersion;
    private readonly SearchEncodingMode _encodingMode;
    private readonly byte[]? _snapshotBytes;

    private SearchEvidenceHandle(
        SearchSourceVersion sourceVersion,
        SearchEncodingMode encodingMode,
        byte[]? snapshotBytes = null)
    {
        _sourceVersion = sourceVersion;
        _encodingMode = encodingMode;
        _snapshotBytes = snapshotBytes;
    }

    public static SearchEvidenceHandle Capture(
        string path,
        SearchEncodingMode encodingMode,
        CancellationToken cancellationToken = default)
    {
        return new(
            SearchSourceVersion.Capture(path, cancellationToken),
            encodingMode);
    }

    public static SearchEvidenceHandle Capture(
        byte[] bytes,
        int length,
        SearchSourceObservation observation,
        SearchEncodingMode encodingMode)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (length < 0 || length > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        var snapshot = bytes.AsSpan(0, length).ToArray();
        return new(
            SearchSourceVersion.FromSnapshot(observation, snapshot),
            encodingMode,
            snapshot);
    }

    public IReadOnlyList<SearchContextLine> Expand(
        long matchingLine,
        SearchContextOptions options,
        CancellationToken cancellationToken = default)
    {
        if (matchingLine <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchingLine));
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsEnabled)
            return Array.Empty<SearchContextLine>();

        EnsureCurrent(cancellationToken);
        IReadOnlyList<SearchContextLine> context;
        try
        {
            context = _snapshotBytes is null
                ? SearchEvidenceReader.Read(
                    _sourceVersion.CanonicalPath,
                    _encodingMode,
                    matchingLine,
                    options,
                    cancellationToken)
                : SearchEvidenceReader.Read(
                    _snapshotBytes,
                    _snapshotBytes.Length,
                    _encodingMode,
                    matchingLine,
                    options,
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SearchEvidenceStaleException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            DecoderFallbackException)
        {
            throw new SearchSourceReadException(
                SearchDiagnosticCatalog.SourceReadFailed(_sourceVersion.CanonicalPath),
                exception);
        }

        EnsureCurrent(cancellationToken);
        return context;
    }

    private void EnsureCurrent(CancellationToken cancellationToken)
    {
        if (!_sourceVersion.MatchesCurrent(_sourceVersion.CanonicalPath, cancellationToken))
        {
            throw new SearchEvidenceStaleException(_sourceVersion.CanonicalPath);
        }
    }
}

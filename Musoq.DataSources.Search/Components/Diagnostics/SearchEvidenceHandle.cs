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

    private SearchEvidenceHandle(
        SearchSourceVersion sourceVersion,
        SearchEncodingMode encodingMode)
    {
        _sourceVersion = sourceVersion;
        _encodingMode = encodingMode;
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
            context = SearchEvidenceReader.Read(
                _sourceVersion.CanonicalPath,
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

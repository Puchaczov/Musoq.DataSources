#nullable enable

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Text;

/// <summary>
///     Bounded context settings for the internal Search execution seam.
/// </summary>
internal sealed record SearchContextOptions
{
    public const int MaxContextLines = 128;
    public const int MaxContextBytes = 1_048_576;
    public const int DefaultContextBytes = 64 * 1024;

    public static SearchContextOptions Disabled { get; } = new();

    public SearchContextOptions(
        int beforeLines = 0,
        int afterLines = 0,
        int maxBytes = DefaultContextBytes)
    {
        if (beforeLines < 0)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("beforeContextLines"));
        if (afterLines < 0)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("afterContextLines"));
        if (maxBytes < 0)
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("contextBytes"));

        if (beforeLines > MaxContextLines ||
            afterLines > MaxContextLines ||
            beforeLines > MaxContextLines - afterLines)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit("contextLines", MaxContextLines));
        }

        var lineCount = beforeLines + afterLines;

        if (maxBytes > MaxContextBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit("contextBytes", MaxContextBytes));
        }

        BeforeLines = beforeLines;
        AfterLines = afterLines;
        MaxBytes = maxBytes;
    }

    public int BeforeLines { get; }

    public int AfterLines { get; }

    public int MaxBytes { get; }

    public bool IsEnabled => BeforeLines > 0 || AfterLines > 0;

    // A fixed per-line share keeps every emitted context window within the
    // request budget and bounds the line text held by the scanner. The
    // resulting value is intentionally conservative when a window is short.
    public int MaxBytesPerLine =>
        !IsEnabled
            ? 0
            : MaxBytes / checked(BeforeLines + AfterLines);
}

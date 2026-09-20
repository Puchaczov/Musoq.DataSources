#nullable enable

namespace Musoq.DataSources.Search.Components.Contracts;

/// <summary>
///     Safety limits shared by the typed pattern contracts and the internal
///     many-pattern request. These limits are independent of any transport
///     format.
/// </summary>
internal static class SearchPatternLimits
{
    public const int MaxPatternCount = 1024;

    public const int MaxPatternIdLength = 128;

    public const int MaxRegexPatternCount = 32;
}

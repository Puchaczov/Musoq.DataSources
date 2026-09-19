#nullable enable

namespace Musoq.DataSources.Search.Testing;

public readonly record struct SearchByteReferenceOccurrence(long Offset, byte[] Bytes);

public static class SearchByteOracle
{
    public static IReadOnlyList<SearchByteReferenceOccurrence> Scan(
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> pattern,
        ReadOnlySpan<byte> masks)
    {
        if (pattern.Length == 0 || pattern.Length != masks.Length)
            throw new ArgumentException("The byte pattern and mask must have equal non-zero lengths.");

        var matches = new List<SearchByteReferenceOccurrence>();
        var nextAllowed = 0;
        for (var offset = 0; offset <= payload.Length - pattern.Length; offset++)
        {
            if (offset < nextAllowed)
                continue;

            var match = true;
            for (var index = 0; index < pattern.Length; index++)
            {
                if ((payload[offset + index] & masks[index]) !=
                    (pattern[index] & masks[index]))
                {
                    match = false;
                    break;
                }
            }

            if (!match)
                continue;

            matches.Add(new SearchByteReferenceOccurrence(
                offset,
                payload.Slice(offset, pattern.Length).ToArray()));
            nextAllowed = checked(offset + pattern.Length);
        }

        return matches;
    }
}

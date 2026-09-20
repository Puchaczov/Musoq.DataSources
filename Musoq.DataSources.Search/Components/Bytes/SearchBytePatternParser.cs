#nullable enable

using System;
using System.Globalization;
using System.Text;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Bytes;

/// <summary>
///     Compiles the typed hexadecimal byte-pattern contract. JSON is
///     intentionally not parsed here: the SQL and host APIs provide the
///     pattern, mask, and window as typed arguments.
/// </summary>
internal static class SearchBytePatternParser
{
    public const int MaxPatternCharacters = SearchRegexBackend.MaxPatternLength;

    internal static SearchBytePattern ParseHex(
        string? bytes,
        string? mask,
        long windowBeforeBytes = 0,
        long windowAfterBytes = 0)
    {
        if (bytes is null)
        {
            throw Invalid(
                "'patternHex' must be a non-null hexadecimal byte sequence",
                SearchDiagnosticPhase.Argument);
        }

        if (bytes.Length == 0 || string.IsNullOrWhiteSpace(bytes))
        {
            throw Invalid(
                "'patternHex' must contain at least one byte value",
                SearchDiagnosticPhase.Argument);
        }

        if (bytes.Length > MaxPatternCharacters)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "pattern characters",
                    MaxPatternCharacters),
                budgetCode: "pattern-size");
        }

        return Compile(
            bytes,
            bytesOffset: 0,
            mask,
            maskOffset: 0,
            windowBeforeBytes,
            windowAfterBytes);
    }

    private static SearchBytePattern Compile(
        string bytesText,
        long bytesOffset,
        string? maskText,
        long maskOffset,
        long windowBeforeBytes,
        long windowAfterBytes)
    {
        var normalizedBytes = NormalizeHexText(bytesText, "bytes", bytesOffset);
        var patternBytes = new byte[normalizedBytes.Length / 2];
        var masks = new byte[patternBytes.Length];
        var containsWildcard = false;

        for (var index = 0; index < patternBytes.Length; index++)
        {
            var sourceIndex = index * 2;
            var high = ParseNibble(
                normalizedBytes[sourceIndex],
                allowWildcard: true,
                "bytes",
                sourceIndex,
                bytesOffset,
                out var highMask);
            var low = ParseNibble(
                normalizedBytes[sourceIndex + 1],
                allowWildcard: true,
                "bytes",
                sourceIndex + 1,
                bytesOffset,
                out var lowMask);

            patternBytes[index] = (byte)((high << 4) | low);
            masks[index] = (byte)((highMask << 4) | lowMask);
            containsWildcard |= highMask != 0xF || lowMask != 0xF;
        }

        if (maskText is not null)
        {
            if (containsWildcard)
            {
                throw Invalid(
                    "'bytes' wildcard nibbles and an explicit 'mask' cannot be combined",
                    SearchDiagnosticPhase.Argument,
                    maskOffset,
                    1,
                    argumentName: "options.maskHex");
            }

            var normalizedMask = NormalizeHexText(maskText, "mask", maskOffset);
            if (normalizedMask.Length != normalizedBytes.Length)
            {
                throw Invalid(
                    $"'mask' must contain exactly {patternBytes.Length} byte values to match 'bytes'",
                    SearchDiagnosticPhase.Argument,
                    maskOffset,
                    Math.Max(1, normalizedMask.Length),
                    argumentName: "options.maskHex");
            }

            for (var index = 0; index < masks.Length; index++)
            {
                var sourceIndex = index * 2;
                var high = ParseNibble(
                    normalizedMask[sourceIndex],
                    allowWildcard: false,
                    "mask",
                    sourceIndex,
                    maskOffset,
                    out _);
                var low = ParseNibble(
                    normalizedMask[sourceIndex + 1],
                    allowWildcard: false,
                    "mask",
                    sourceIndex + 1,
                    maskOffset,
                    out _);
                masks[index] = (byte)((high << 4) | low);
            }
        }

        return new SearchBytePattern(
            patternBytes,
            masks,
            SearchByteWindowOptions.Create(
                windowBeforeBytes,
                windowAfterBytes,
                patternBytes.Length));
    }

    private static string NormalizeHexText(
        string value,
        string field,
        long fieldOffset)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!IsAsciiWhitespace(character))
                builder.Append(character);
        }

        if (builder.Length == 0)
        {
            throw Invalid(
                $"'{field}' must contain at least one byte value",
                SearchDiagnosticPhase.Argument,
                fieldOffset,
                1,
                argumentName: field == "mask" ? "options.maskHex" : "patternHex");
        }

        if (builder.ToString().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(
                $"'{field}' must be an explicit byte string; the 0x prefix is not accepted",
                SearchDiagnosticPhase.Argument,
                fieldOffset,
                2,
                argumentName: field == "mask" ? "options.maskHex" : "patternHex");
        }

        if ((builder.Length & 1) != 0)
        {
            throw Invalid(
                $"'{field}' must contain an even number of hex nibbles, with two nibbles per byte",
                SearchDiagnosticPhase.Argument,
                fieldOffset,
                Math.Max(1, builder.Length),
                argumentName: field == "mask" ? "options.maskHex" : "patternHex");
        }

        return builder.ToString();
    }

    private static bool IsAsciiWhitespace(char character)
    {
        return character is ' ' or '\t' or '\r' or '\n';
    }

    private static int ParseNibble(
        char character,
        bool allowWildcard,
        string field,
        int nibbleIndex,
        long fieldOffset,
        out int mask)
    {
        if (allowWildcard && character == '?')
        {
            mask = 0;
            return 0;
        }

        mask = 0xF;
        if (character is >= '0' and <= '9')
            return character - '0';
        if (character is >= 'A' and <= 'F')
            return character - 'A' + 10;
        if (character is >= 'a' and <= 'f')
            return character - 'a' + 10;

        throw Invalid(
            $"'{field}' contains invalid hex nibble '{SearchDiagnosticText.Display(character.ToString())}' at nibble {nibbleIndex.ToString(CultureInfo.InvariantCulture)}",
            SearchDiagnosticPhase.Argument,
            fieldOffset,
            1,
            argumentName: field == "mask" ? "options.maskHex" : "patternHex");
    }

    private static SearchPatternException Invalid(
        string reason,
        SearchDiagnosticPhase phase,
        long? offset = null,
        long? length = null,
        string argumentName = "patternHex",
        Exception? innerException = null)
    {
        return new SearchPatternException(
            SearchDiagnosticCatalog.InvalidBytePattern(
                reason,
                phase,
                offset,
                length,
                argumentName),
            innerException);
    }
}

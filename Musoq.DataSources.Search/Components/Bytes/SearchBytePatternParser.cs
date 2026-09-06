#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Bytes;

internal static class SearchBytePatternParser
{
    public const int CurrentVersion = 1;

    public const int MaxPatternCharacters = SearchRegexBackend.MaxPatternLength;

    private const int MaxJsonDepth = 8;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static SearchBytePattern Parse(string? patternJson)
    {
        if (patternJson is null)
        {
            throw Invalid(
                "pattern must be a non-null versioned JSON object",
                SearchDiagnosticPhase.Argument);
        }

        if (patternJson.Length == 0 || string.IsNullOrWhiteSpace(patternJson))
        {
            throw Invalid(
                "pattern must contain one non-empty JSON object",
                SearchDiagnosticPhase.Argument);
        }

        if (patternJson.Length > MaxPatternCharacters)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "pattern characters",
                    MaxPatternCharacters),
                budgetCode: "pattern-size");
        }

        byte[] utf8;
        try
        {
            utf8 = StrictUtf8.GetBytes(patternJson);
        }
        catch (EncoderFallbackException exception)
        {
            throw Invalid(
                "pattern contains an invalid UTF-16 scalar",
                SearchDiagnosticPhase.Syntax,
                innerException: exception);
        }

        try
        {
            return ParseJson(utf8);
        }
        catch (SearchPatternException)
        {
            throw;
        }
        catch (SearchResourceLimitException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid(
                "pattern is not valid JSON",
                SearchDiagnosticPhase.Syntax,
                GetJsonErrorOffset(utf8, exception),
                1,
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw Invalid(
                "pattern contains a value with an invalid JSON type",
                SearchDiagnosticPhase.Syntax,
                innerException: exception);
        }
    }

    private static SearchBytePattern ParseJson(ReadOnlySpan<byte> utf8)
    {
        var reader = new Utf8JsonReader(
            utf8,
            isFinalBlock: true,
            state: new JsonReaderState(
                new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaxJsonDepth
                }));
        var names = new HashSet<string>(StringComparer.Ordinal);
        int? version = null;
        string? bytes = null;
        string? mask = null;
        long windowBeforeBytes = 0;
        long windowAfterBytes = 0;
        long versionOffset = 0;
        long bytesOffset = 0;
        long maskOffset = 0;
        var hasEndObject = false;

        if (!reader.Read())
            throw Invalid("pattern must contain one JSON object", SearchDiagnosticPhase.Syntax);
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw Invalid(
                "pattern must contain one JSON object, not a JSON scalar or array",
                SearchDiagnosticPhase.Argument,
                reader.TokenStartIndex,
                1);
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                hasEndObject = true;
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw Invalid(
                    "pattern objects may contain only named properties",
                    SearchDiagnosticPhase.Syntax,
                    reader.TokenStartIndex,
                    1);
            }

            var propertyName = reader.GetString() ?? string.Empty;
            var propertyOffset = reader.TokenStartIndex;
            if (!names.Add(propertyName))
            {
                throw Invalid(
                    $"duplicate JSON property '{Display(propertyName)}'",
                    SearchDiagnosticPhase.Argument,
                    propertyOffset,
                    Math.Max(1, propertyName.Length));
            }

            if (!reader.Read())
            {
                throw Invalid(
                    $"property '{Display(propertyName)}' has no value",
                    SearchDiagnosticPhase.Syntax,
                    reader.BytesConsumed,
                    1);
            }

            switch (propertyName)
            {
                case "version":
                    versionOffset = propertyOffset;
                    if (reader.TokenType != JsonTokenType.Number ||
                        !reader.TryGetInt32(out var parsedVersion))
                    {
                        throw Invalid(
                            "'version' must be an integer",
                            SearchDiagnosticPhase.Argument,
                            propertyOffset,
                            1);
                    }

                    version = parsedVersion;
                    break;
                case "bytes":
                    bytesOffset = propertyOffset;
                    bytes = ReadString(
                        ref reader,
                        "'bytes'",
                        propertyOffset);
                    break;
                case "mask":
                    maskOffset = propertyOffset;
                    mask = ReadString(
                        ref reader,
                        "'mask'",
                        propertyOffset);
                    break;
                case "window":
                    (windowBeforeBytes, windowAfterBytes) = ReadWindow(
                        ref reader,
                        propertyOffset);
                    break;
                case "endianness":
                    throw Invalid(
                        "'endianness' is not supported; byte patterns are byte-order neutral, so encode bytes in scan order and no conversion is applied",
                        SearchDiagnosticPhase.Argument,
                        propertyOffset,
                        1);
                default:
                    throw Invalid(
                        $"unknown property '{Display(propertyName)}'; only 'version', 'bytes', optional 'mask' and optional 'window' are supported",
                        SearchDiagnosticPhase.Argument,
                        propertyOffset,
                        Math.Max(1, propertyName.Length));
            }
        }

        if (!hasEndObject)
        {
            throw Invalid(
                "pattern object ended before the JSON value was complete",
                SearchDiagnosticPhase.Syntax,
                reader.BytesConsumed,
                1);
        }

        if (reader.Read())
        {
            throw Invalid(
                "pattern must contain exactly one JSON object",
                SearchDiagnosticPhase.Syntax,
                reader.TokenStartIndex,
                1);
        }

        if (version is null)
        {
            throw Invalid(
                "'version' is required",
                SearchDiagnosticPhase.Argument,
                0,
                1);
        }

        if (version.Value != CurrentVersion)
        {
            throw Invalid(
                $"'version' must be {CurrentVersion}",
                SearchDiagnosticPhase.Argument,
                versionOffset,
                1);
        }

        if (bytes is null)
        {
            throw Invalid(
                "'bytes' is required",
                SearchDiagnosticPhase.Argument,
                0,
                1);
        }

        return Compile(
            bytes,
            bytesOffset,
            mask,
            maskOffset,
            windowBeforeBytes,
            windowAfterBytes);
    }

    private static (long BeforeBytes, long AfterBytes) ReadWindow(
        ref Utf8JsonReader reader,
        long propertyOffset)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw Invalid(
                "'window' must be an object with non-negative 'beforeBytes' and 'afterBytes' integers",
                SearchDiagnosticPhase.Argument,
                propertyOffset,
                1);
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        long beforeBytes = 0;
        long afterBytes = 0;
        var hasEndObject = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                hasEndObject = true;
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw Invalid(
                    "'window' may contain only named properties",
                    SearchDiagnosticPhase.Syntax,
                    reader.TokenStartIndex,
                    1);
            }

            var propertyName = reader.GetString() ?? string.Empty;
            var nestedOffset = reader.TokenStartIndex;
            if (!names.Add(propertyName))
            {
                throw Invalid(
                    $"duplicate JSON property 'window.{Display(propertyName)}'",
                    SearchDiagnosticPhase.Argument,
                    nestedOffset,
                    Math.Max(1, propertyName.Length));
            }

            if (!reader.Read())
            {
                throw Invalid(
                    $"property 'window.{Display(propertyName)}' has no value",
                    SearchDiagnosticPhase.Syntax,
                    reader.BytesConsumed,
                    1);
            }

            if (propertyName is not "beforeBytes" and not "afterBytes")
            {
                throw Invalid(
                    $"unknown window property '{Display(propertyName)}'; only 'beforeBytes' and 'afterBytes' are supported",
                    SearchDiagnosticPhase.Argument,
                    nestedOffset,
                    Math.Max(1, propertyName.Length));
            }

            if (reader.TokenType != JsonTokenType.Number ||
                !reader.TryGetInt64(out var parsedValue) ||
                parsedValue < 0)
            {
                throw Invalid(
                    $"'window.{Display(propertyName)}' must be a non-negative integer",
                    SearchDiagnosticPhase.Argument,
                    nestedOffset,
                    1);
            }

            if (propertyName == "beforeBytes")
                beforeBytes = parsedValue;
            else
                afterBytes = parsedValue;
        }

        if (!hasEndObject)
        {
            throw Invalid(
                "'window' ended before its JSON object was complete",
                SearchDiagnosticPhase.Syntax,
                reader.BytesConsumed,
                1);
        }

        return (beforeBytes, afterBytes);
    }

    private static string ReadString(
        ref Utf8JsonReader reader,
        string property,
        long propertyOffset)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw Invalid(
                $"{property} must be a string",
                SearchDiagnosticPhase.Argument,
                propertyOffset,
                1);
        }

        return reader.GetString() ??
               throw Invalid(
                   $"{property} must not be null",
                   SearchDiagnosticPhase.Argument,
                   propertyOffset,
                   1);
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
                    1);
            }

            var normalizedMask = NormalizeHexText(maskText, "mask", maskOffset);
            if (normalizedMask.Length != normalizedBytes.Length)
            {
                throw Invalid(
                    $"'mask' must contain exactly {patternBytes.Length} byte values to match 'bytes'",
                    SearchDiagnosticPhase.Argument,
                    maskOffset,
                    Math.Max(1, normalizedMask.Length));
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
                1);
        }

        if (builder.ToString().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(
                $"'{field}' must be an explicit byte string; the 0x prefix is not accepted",
                SearchDiagnosticPhase.Argument,
                fieldOffset,
                2);
        }

        if ((builder.Length & 1) != 0)
        {
            throw Invalid(
                $"'{field}' must contain an even number of hex nibbles, with two nibbles per byte",
                SearchDiagnosticPhase.Argument,
                fieldOffset,
                Math.Max(1, builder.Length));
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
            1);
    }

    private static SearchPatternException Invalid(
        string reason,
        SearchDiagnosticPhase phase,
        long? offset = null,
        long? length = null,
        Exception? innerException = null)
    {
        return new SearchPatternException(
            SearchDiagnosticCatalog.InvalidBytePattern(reason, phase, offset, length),
            innerException);
    }

    private static long GetJsonErrorOffset(
        ReadOnlySpan<byte> json,
        JsonException exception)
    {
        var line = exception.LineNumber.GetValueOrDefault();
        var position = exception.BytePositionInLine.GetValueOrDefault();
        var offset = 0L;

        for (var currentLine = 0L; currentLine < line; currentLine++)
        {
            var newline = json[(int)Math.Min(offset, json.Length)..].IndexOf((byte)'\n');
            if (newline < 0)
                return Math.Min(json.Length, offset + position);

            offset += newline + 1L;
        }

        return Math.Min(json.Length, offset + position);
    }

    private static string Display(string value)
    {
        return SearchDiagnosticText.Display(value);
    }
}

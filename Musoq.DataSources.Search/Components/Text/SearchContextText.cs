#nullable enable

using System;
using System.Text;

namespace Musoq.DataSources.Search.Components.Text;

internal static class SearchContextText
{
    private static readonly Encoding Utf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);

    public static string? Truncate(string? lineText, int maxBytes)
    {
        if (lineText is null)
            return null;

        if (maxBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));

        if (Utf8.GetByteCount(lineText) <= maxBytes)
            return lineText;
        if (maxBytes == 0)
            return null;

        var bytes = 0;
        var charLength = 0;
        while (charLength < lineText.Length)
        {
            var nextCharLength =
                char.IsHighSurrogate(lineText[charLength]) &&
                charLength + 1 < lineText.Length &&
                char.IsLowSurrogate(lineText[charLength + 1])
                    ? 2
                    : 1;
            var nextBytes = Utf8.GetByteCount(
                lineText.AsSpan(charLength, nextCharLength));
            if (bytes > maxBytes - nextBytes)
                break;

            bytes += nextBytes;
            charLength += nextCharLength;
        }

        return charLength == 0 ? null : lineText[..charLength];
    }
}

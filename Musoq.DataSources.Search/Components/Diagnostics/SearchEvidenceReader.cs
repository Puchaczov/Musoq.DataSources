#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Traversal;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Diagnostics;

internal static class SearchEvidenceReader
{
    public static IReadOnlyList<SearchContextLine> Read(
        string path,
        SearchEncodingMode encodingMode,
        long matchingLine,
        SearchContextOptions options,
        CancellationToken cancellationToken)
    {
        using var reader = SearchTextReader.Open(path, encodingMode);
        return Read(reader, matchingLine, options, cancellationToken, path);
    }

    public static IReadOnlyList<SearchContextLine> Read(
        byte[] bytes,
        int length,
        SearchEncodingMode encodingMode,
        long matchingLine,
        SearchContextOptions options,
        CancellationToken cancellationToken)
    {
        using var reader = SearchTextReader.Open(bytes, length, encodingMode);
        return Read(reader, matchingLine, options, cancellationToken, "<snapshot>");
    }

    private static IReadOnlyList<SearchContextLine> Read(
        TextReader reader,
        long matchingLine,
        SearchContextOptions options,
        CancellationToken cancellationToken,
        string stalePath)
    {
        using var buffer = SearchCharBuffer.Rent();
        var beforeLines = new Queue<ContextLineData>();
        var result = new List<SearchContextLine>(
            checked(options.BeforeLines + options.AfterLines));
        var lineText = options.MaxBytesPerLine > 0
            ? new StringBuilder()
            : null;
        var lineNumber = 1L;
        var hasTextOnLine = false;
        var foundMatchingLine = false;
        var stop = false;

        while (!stop)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var charsRead = reader.Read(buffer.Array, 0, buffer.Length);
            cancellationToken.ThrowIfCancellationRequested();
            if (charsRead == 0)
                break;

            for (var index = 0; index < charsRead; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                var character = buffer.Array[index];
                hasTextOnLine = true;
                if (character != '\n')
                {
                    AppendLineCharacter(lineText, character, options.MaxBytesPerLine);
                    continue;
                }

                AppendLineCharacter(lineText, character, options.MaxBytesPerLine);
                stop = CompleteLine(
                    lineNumber,
                    matchingLine,
                    lineText,
                    hasTextOnLine,
                    options,
                    beforeLines,
                    result,
                    ref foundMatchingLine);
                lineText?.Clear();
                hasTextOnLine = false;
                lineNumber = checked(lineNumber + 1);

                if (stop)
                    break;
            }
        }

        if (!stop && hasTextOnLine)
        {
            _ = CompleteLine(
                lineNumber,
                matchingLine,
                lineText,
                hasTextOnLine,
                options,
                beforeLines,
                result,
                ref foundMatchingLine);
        }

        if (!foundMatchingLine)
            throw new SearchEvidenceStaleException(stalePath);

        return result.AsReadOnly();
    }

    private static bool CompleteLine(
        long lineNumber,
        long matchingLine,
        StringBuilder? lineText,
        bool hasTextOnLine,
        SearchContextOptions options,
        Queue<ContextLineData> beforeLines,
        List<SearchContextLine> result,
        ref bool foundMatchingLine)
    {
        if (!foundMatchingLine)
        {
            if (lineNumber == matchingLine)
            {
                foundMatchingLine = true;
                foreach (var beforeLine in beforeLines)
                {
                    result.Add(new SearchContextLine(
                        checked((int)(beforeLine.LineNumber - matchingLine)),
                        beforeLine.LineNumber,
                        beforeLine.LineText));
                }

                return options.AfterLines == 0;
            }

            if (lineNumber < matchingLine && options.BeforeLines > 0)
            {
                beforeLines.Enqueue(new ContextLineData(
                    lineNumber,
                    GetLineText(lineText, options.MaxBytesPerLine, hasTextOnLine)));
                while (beforeLines.Count > options.BeforeLines)
                    beforeLines.Dequeue();
            }

            return false;
        }

        if (lineNumber <= matchingLine)
            return false;

        var relativeLine = checked(lineNumber - matchingLine);
        if (relativeLine > options.AfterLines)
            return true;

        result.Add(new SearchContextLine(
            checked((int)relativeLine),
            lineNumber,
            GetLineText(lineText, options.MaxBytesPerLine, hasTextOnLine)));
        return result.Count >= options.BeforeLines + options.AfterLines;
    }

    private static string? GetLineText(
        StringBuilder? lineText,
        int maxBytesPerLine,
        bool hasTextOnLine)
    {
        if (!hasTextOnLine || lineText is null)
            return null;

        return SearchContextText.Truncate(lineText.ToString(), maxBytesPerLine);
    }

    private static void AppendLineCharacter(
        StringBuilder? lineText,
        char character,
        int maxBytesPerLine)
    {
        if (lineText is null || lineText.Length >= maxBytesPerLine)
            return;

        lineText.Append(character);
    }

    private readonly record struct ContextLineData(long LineNumber, string? LineText);
}

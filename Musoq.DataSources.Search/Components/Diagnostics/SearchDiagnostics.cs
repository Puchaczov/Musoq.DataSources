#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Musoq.DataSources.Search.Components.Bytes;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Diagnostics;

internal static class SearchDiagnosticText
{
    public const int DefaultDisplayLength = 96;

    public static string Display(
        string value,
        int maxLength = DefaultDisplayLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var character in value)
        {
            var escaped = NeedsEscape(character)
                ? "\\u" + ((int)character).ToString("X4", CultureInfo.InvariantCulture)
                : character.ToString();
            if (builder.Length + escaped.Length > maxLength)
            {
                if (builder.Length == 0)
                    builder.Append('…');
                else if (builder.Length == maxLength)
                    builder[maxLength - 1] = '…';
                else
                    builder.Append('…');

                break;
            }

            builder.Append(escaped);
        }

        return builder.ToString();
    }

    public static string Bound(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        return value.Length <= maxLength
            ? value
            : value[..(maxLength - 1)] + "…";
    }

    private static bool NeedsEscape(char character)
    {
        return char.IsControl(character) ||
               char.GetUnicodeCategory(character) is
                   UnicodeCategory.Format or
                   UnicodeCategory.LineSeparator or
                   UnicodeCategory.ParagraphSeparator;
    }
}

internal enum SearchDiagnosticPhase
{
    Syntax,
    Argument,
    SourceAccess,
    Resource,
    Output
}

internal sealed record SearchDiagnosticLocation
{
    private const int MaxValueLength = 256;

    private SearchDiagnosticLocation(
        string? argumentName,
        string? path,
        long? offset,
        long? length)
    {
        ArgumentName = Normalize(argumentName);
        Path = Normalize(path);
        DisplayArgumentName = argumentName is null
            ? null
            : SearchDiagnosticText.Display(argumentName, MaxValueLength);
        DisplayPath = path is null
            ? null
            : SearchDiagnosticText.Display(path, MaxValueLength);
        Offset = offset;
        Length = length;
    }

    public string? ArgumentName { get; }

    public string? Path { get; }

    /// <summary>
    ///     Gets the bounded presentation-safe argument name.
    /// </summary>
    public string? DisplayArgumentName { get; }

    /// <summary>
    ///     Gets the bounded presentation-safe path. <see cref="Path"/> retains
    ///     the machine-facing value.
    /// </summary>
    public string? DisplayPath { get; }

    public long? Offset { get; }

    public long? Length { get; }

    public static SearchDiagnosticLocation ForArgument(string argumentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentName);
        return new SearchDiagnosticLocation(argumentName, null, null, null);
    }

    public static SearchDiagnosticLocation ForPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new SearchDiagnosticLocation(null, path, null, null);
    }

    public static SearchDiagnosticLocation ForArgumentSpan(
        string argumentName,
        long offset,
        long length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentName);
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        return new SearchDiagnosticLocation(argumentName, null, offset, length);
    }

    private static string? Normalize(string? value)
    {
        if (value is null)
            return null;

        return SearchDiagnosticText.Bound(value, MaxValueLength);
    }
}

internal sealed record SearchDiagnostic
{
    internal const int MaxTextLength = 512;

    public SearchDiagnostic(
        string code,
        SearchDiagnosticPhase phase,
        string message,
        string explanation,
        string suggestedFix,
        SearchDiagnosticLocation? location = null)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            !code.StartsWith("SEARCH-", StringComparison.Ordinal))
        {
            throw new ArgumentException("Search diagnostic codes must use the SEARCH- prefix.", nameof(code));
        }

        Code = code;
        Phase = phase;
        Message = RequireBounded(message, nameof(message));
        Explanation = RequireBounded(explanation, nameof(explanation));
        SuggestedFix = RequireBounded(suggestedFix, nameof(suggestedFix));
        Location = location;
    }

    public string Code { get; }

    public SearchDiagnosticPhase Phase { get; }

    public string Message { get; }

    public string Explanation { get; }

    public string SuggestedFix { get; }

    public SearchDiagnosticLocation? Location { get; }

    private static string RequireBounded(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaxTextLength)
        {
            throw new ArgumentException(
                $"Search diagnostic text cannot exceed {MaxTextLength} characters.",
                parameterName);
        }

        if (value.Contains('\r') || value.Contains('\n'))
            throw new ArgumentException("Search diagnostic text cannot contain line breaks.", parameterName);

        return value;
    }
}

internal static class SearchDiagnosticCodes
{
    public const string InvalidRegex = "SEARCH-SYNTAX-001";
    public const string InvalidRecordFraming = "SEARCH-SYNTAX-002";
    public const string InvalidBytePattern = "SEARCH-SYNTAX-003";
    public const string InvalidArgument = "SEARCH-ARGUMENT-001";
    public const string MissingRoot = "SEARCH-SOURCE-001";
    public const string SourceOpenFailed = "SEARCH-SOURCE-002";
    public const string SourceReadFailed = "SEARCH-SOURCE-003";
    public const string UnsupportedEncoding = "SEARCH-SOURCE-004";
    public const string SourceChanged = "SEARCH-SOURCE-005";
    public const string ResourceLimit = "SEARCH-RESOURCE-001";
    public const string OutputFailed = "SEARCH-OUTPUT-001";

    public static IReadOnlyList<string> All { get; } =
    [
        InvalidRegex,
        InvalidRecordFraming,
        InvalidBytePattern,
        InvalidArgument,
        MissingRoot,
        SourceOpenFailed,
        SourceReadFailed,
        UnsupportedEncoding,
        SourceChanged,
        ResourceLimit,
        OutputFailed
    ];
}

internal static class SearchDiagnosticCatalog
{
    public static SearchDiagnostic InvalidArgument(string argumentName)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.InvalidArgument,
            SearchDiagnosticPhase.Argument,
            "The Search request contains an invalid argument.",
            "A required Search source argument is missing, null, or has an unsupported type or value.",
            "Provide a non-null string value for each declared Search source argument.",
            SearchDiagnosticLocation.ForArgument(argumentName));
    }

    public static SearchDiagnostic InvalidArgumentCount(int actualCount)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.InvalidArgument,
            SearchDiagnosticPhase.Argument,
            "The Search source received the wrong number of arguments.",
            $"The matches source requires exactly two arguments, but received {actualCount}.",
            "Pass exactly a root string and a literal string to search.matches.",
            SearchDiagnosticLocation.ForArgument("arguments"));
    }

    public static SearchDiagnostic InvalidManyInput(
        string reason,
        SearchDiagnosticPhase phase,
        long? offset = null,
        long? length = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var location = offset is null
            ? SearchDiagnosticLocation.ForArgument("patterns")
            : SearchDiagnosticLocation.ForArgumentSpan(
                "patterns",
                offset.Value,
                length ?? 1);

        return new SearchDiagnostic(
            SearchDiagnosticCodes.InvalidArgument,
            phase,
            "The Search many typed input is invalid.",
            $"The Search many typed input is invalid: {SearchDiagnosticText.Display(reason, 384)}.",
            "Correct the typed pattern collection and use only the documented bounded fields and values.",
            location);
    }

    public static SearchDiagnostic InvalidRegex()
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.InvalidRegex,
            SearchDiagnosticPhase.Syntax,
            "The Search regex pattern is invalid for the portable non-backtracking dialect.",
            "The pattern cannot be parsed by Search's portable non-backtracking regex dialect.",
            "Correct the pattern for this dialect or use literal mode when metacharacters are not required.",
            SearchDiagnosticLocation.ForArgument("pattern"));
    }

    public static SearchDiagnostic InvalidRecordFraming(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new SearchDiagnostic(
            SearchDiagnosticCodes.InvalidRecordFraming,
            SearchDiagnosticPhase.Syntax,
            "The Search input has malformed record framing.",
            $"The bounded multiline record framing is malformed: {SearchDiagnosticText.Display(reason, 384)}.",
            "Correct the record delimiters or allow an unterminated EOF record before retrying.",
            SearchDiagnosticLocation.ForArgument("recordFraming"));
    }

    public static SearchDiagnostic InvalidBytePattern(
        string reason,
        SearchDiagnosticPhase phase = SearchDiagnosticPhase.Argument,
        long? offset = null,
        long? length = null,
        string argumentName = "patternHex")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var location = offset is null
            ? SearchDiagnosticLocation.ForArgument(argumentName)
            : SearchDiagnosticLocation.ForArgumentSpan(
                argumentName,
                offset.Value,
                length ?? 1);

        return new SearchDiagnostic(
            SearchDiagnosticCodes.InvalidBytePattern,
            phase,
            "The Search byte pattern is invalid.",
            $"The typed hexadecimal byte pattern is invalid: {SearchDiagnosticText.Display(reason, 384)}.",
            "Use an explicit even-length hexadecimal pattern, and provide an optional same-length hexadecimal mask through typed byte options; do not use an SQL numeric literal.",
            location);
    }

    public static SearchDiagnostic UnsupportedRegexConstruct(string construct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(construct);
        return new SearchDiagnostic(
            SearchDiagnosticCodes.InvalidRegex,
            SearchDiagnosticPhase.Syntax,
            "The Search regex uses an unsupported construct.",
            $"The portable non-backtracking dialect does not support {SearchDiagnosticText.Display(construct)}.",
            $"Rewrite the pattern without {SearchDiagnosticText.Display(construct)}, or use literal mode; Search will not switch dialects silently.",
            SearchDiagnosticLocation.ForArgument("pattern"));
    }

    public static SearchDiagnostic MissingRoot(string path)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.MissingRoot,
            SearchDiagnosticPhase.SourceAccess,
            "The Search root could not be found.",
            "Search cannot establish a complete scope because the requested file or directory does not exist.",
            "Check the root path and make sure it exists before executing the query.",
            SearchDiagnosticLocation.ForPath(path));
    }

    public static SearchDiagnostic SourceOpenFailed(string path)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.SourceOpenFailed,
            SearchDiagnosticPhase.SourceAccess,
            "Search could not open an input file.",
            "The requested scope may be incomplete because an input file could not be opened.",
            "Check the file permissions, path, and availability, then retry the search.",
            SearchDiagnosticLocation.ForPath(path));
    }

    public static SearchDiagnostic SourceReadFailed(string path)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.SourceReadFailed,
            SearchDiagnosticPhase.SourceAccess,
            "Search could not read an input file.",
            "The result cannot be treated as an exhaustive search because reading an input failed.",
            "Check the file and encoding, then retry the search from the beginning.",
            SearchDiagnosticLocation.ForPath(path));
    }

    public static SearchDiagnostic UnsupportedEncoding()
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.UnsupportedEncoding,
            SearchDiagnosticPhase.SourceAccess,
            "The requested Search encoding is unsupported.",
            "Search only accepts encodings explicitly declared by its text contract.",
            "Use a declared UTF-8 or UTF-16 encoding, or select byte search for arbitrary binary input.",
            SearchDiagnosticLocation.ForArgument("encoding"));
    }

    public static SearchDiagnostic EncodingMismatch()
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.UnsupportedEncoding,
            SearchDiagnosticPhase.SourceAccess,
            "The requested Search encoding does not match the input.",
            "The declared encoding does not agree with the input byte-order mark.",
            "Use auto or declare the matching UTF-8 or UTF-16 encoding mode.",
            SearchDiagnosticLocation.ForArgument("encoding"));
    }

    public static SearchDiagnostic SourceChanged(string path)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.SourceChanged,
            SearchDiagnosticPhase.SourceAccess,
            "Search detected a source change during the read.",
            "The source changed, was replaced, deleted or became unreadable while Search was processing it; the observed rows are not an atomic snapshot.",
            "Retry against a stable or immutable input, or handle the incomplete result explicitly.",
            SearchDiagnosticLocation.ForPath(path));
    }

    public static SearchDiagnostic ResourceLimit(string argumentName, long limit)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.ResourceLimit,
            SearchDiagnosticPhase.Resource,
            "The Search request exceeds a safety limit.",
            $"The '{argumentName}' request value exceeds the declared limit of {limit}.",
            "Reduce the request to the declared limit and split larger work into bounded searches.",
            SearchDiagnosticLocation.ForArgument(argumentName));
    }

    public static SearchDiagnostic RegexRecordTooLong(int limit)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.ResourceLimit,
            SearchDiagnosticPhase.Resource,
            "The Search regex record exceeds the safety limit.",
            $"A physical record is longer than the declared regex limit of {limit} UTF-16 characters.",
            "Split the input into shorter physical records or use literal mode for the large record.",
            SearchDiagnosticLocation.ForArgument("record"));
    }

    public static SearchDiagnostic RegexMultilineRecordTooLong(int limit)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.ResourceLimit,
            SearchDiagnosticPhase.Resource,
            "The Search multiline regex record exceeds the safety limit.",
            $"A bounded multiline record is longer than the declared regex limit of {limit} original bytes.",
            "Reduce the record, choose physical-line scope, or split the input into bounded searches.",
            SearchDiagnosticLocation.ForArgument("recordBytes"));
    }

    public static SearchDiagnostic RegexMatchTimedOut(int milliseconds)
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.ResourceLimit,
            SearchDiagnosticPhase.Resource,
            "The Search regex match exceeded its time limit.",
            $"The portable non-backtracking regex did not finish one bounded record within {milliseconds} milliseconds.",
            "Simplify the pattern or split the input into shorter records, then retry.",
            SearchDiagnosticLocation.ForArgument("pattern"));
    }

    public static SearchDiagnostic OutputFailed()
    {
        return new SearchDiagnostic(
            SearchDiagnosticCodes.OutputFailed,
            SearchDiagnosticPhase.Output,
            "Search could not publish its result rows.",
            "The scan did not complete with a trustworthy output stream.",
            "Retry the query and inspect the host result consumer if the failure persists.");
    }
}

internal interface ISearchDiagnosticException
{
    SearchDiagnostic Diagnostic { get; }
}

internal sealed class SearchRequestException : ArgumentException, ISearchDiagnosticException
{
    public SearchRequestException(SearchDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, diagnostic.Location?.ArgumentName, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public SearchDiagnostic Diagnostic { get; }
}

internal sealed class SearchPatternException : ArgumentException, ISearchDiagnosticException
{
    public SearchPatternException(SearchDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, diagnostic.Location?.ArgumentName, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public SearchDiagnostic Diagnostic { get; }
}

internal sealed class SearchRecordFramingException : IOException, ISearchDiagnosticException
{
    public SearchRecordFramingException(SearchDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public SearchDiagnostic Diagnostic { get; }
}

internal sealed class SearchEncodingException : ArgumentException, ISearchDiagnosticException
{
    public SearchEncodingException(SearchDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, diagnostic.Location?.ArgumentName, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public SearchDiagnostic Diagnostic { get; }
}

internal sealed class SearchResourceLimitException : ArgumentException, ISearchDiagnosticException
{
    public SearchResourceLimitException(
        SearchDiagnostic diagnostic,
        Exception? innerException = null,
        string? budgetCode = null)
        : base(diagnostic.Message, diagnostic.Location?.ArgumentName, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
        BudgetCode = budgetCode;
    }

    public SearchDiagnostic Diagnostic { get; }

    public string? BudgetCode { get; }
}

internal sealed class SearchSourceAccessException : IOException, ISearchDiagnosticException
{
    public SearchSourceAccessException(SearchDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public SearchDiagnostic Diagnostic { get; }
}

internal sealed class SearchSourceReadException : IOException, ISearchDiagnosticException
{
    public SearchSourceReadException(SearchDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public SearchDiagnostic Diagnostic { get; }
}

internal sealed class SearchSourceChangedException : IOException, ISearchDiagnosticException
{
    public SearchSourceChangedException(string path)
        : base(SearchDiagnosticCatalog.SourceChanged(path).Message)
    {
        Diagnostic = SearchDiagnosticCatalog.SourceChanged(path);
        Path = path;
    }

    public SearchDiagnostic Diagnostic { get; }

    public string Path { get; }
}

internal sealed class SearchOutputException : InvalidOperationException, ISearchDiagnosticException
{
    public SearchOutputException(SearchDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, innerException)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    public SearchDiagnostic Diagnostic { get; }
}

internal static class SearchDiagnosticValidation
{
    public static void ValidateRegex(string? pattern)
    {
        SearchRegexBackend.Validate(pattern);
    }

    public static void ValidateEncoding(string? encoding)
    {
        _ = SearchEncodingPolicy.Parse(encoding);
    }

    public static SearchBytePattern ValidateBytePattern(string? pattern)
    {
        return SearchBytePatternParser.ParseHex(pattern, mask: null);
    }
}

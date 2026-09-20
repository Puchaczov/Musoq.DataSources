#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Musoq.DataSources.Search.Components.Bytes;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Contracts;

/// <summary>
///     Converts the public structural-input contract into the existing
///     immutable Search execution request types. Keeping this conversion in
///     one place prevents typed constructors from developing different
///     defaults or validation rules.
/// </summary>
internal static class SearchTypedInputNormalizer
{
    public static SearchRequest CreateSingle(
        string root,
        string pattern,
        SearchTextInput? text,
        SearchRecordsInput? records,
        SearchContextInput? context,
        SearchScopeInput? scope,
        SearchLimitsInput? limits)
    {
        ValidateRequiredText(root, "root");
        ValidateRequiredText(pattern, "pattern");
        var effectiveText = NormalizeText(text);
        var effectiveLimits = CreateLimits(limits);
        var maxRecordBytesExplicit = limits is not null &&
                                     limits.Value.MaxRecordBytes != long.MaxValue;
        var effectiveScope = CreateScope(scope);
        var effectiveContext = CreateContext(context, effectiveLimits);
        var mode = ParsePatternMode(effectiveText.Mode, "text.mode");

        if (mode == SearchPatternMode.Literal)
        {
            if (records is not null)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidArgument("records"));
            }

            return SearchRequest.Create(
                root,
                pattern,
                effectiveScope,
                effectiveText.Encoding,
                effectiveText.CaseMode,
                effectiveText.WholeWord,
                effectiveContext,
                effectiveLimits,
                maxRecordBytesExplicit: maxRecordBytesExplicit);
        }

        var effectiveRecords = records ?? new SearchRecordsInput();
        var recordMode = ParseRecordMode(effectiveRecords.Mode);
        var maxRecordBytes = ValidateRecordBytes(
            effectiveRecords.MaxBytes,
            "records.maxBytes");
        var framing = recordMode == SearchRecordMode.BoundedMultiline
            ? CreateFraming(effectiveRecords)
            : null;

        if (recordMode == SearchRecordMode.PhysicalLine &&
            (effectiveRecords.StartDelimiter is not null ||
             effectiveRecords.EndDelimiter is not null ||
             effectiveRecords.AllowUnterminatedEof))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("records"));
        }

        if (effectiveLimits.MaxRecordBytes != SearchResourceLimits.Unlimited)
        {
            maxRecordBytes = Math.Min(
                maxRecordBytes,
                checked((int)effectiveLimits.MaxRecordBytes));
        }

        return SearchRequest.CreateRegex(
            root,
            pattern,
            effectiveScope,
            effectiveText.Encoding,
            effectiveText.CaseMode,
            effectiveText.WholeWord,
            recordMode,
            maxRecordBytes,
            effectiveContext,
            framing,
            effectiveLimits);
    }

    public static SearchManyRequest CreateMany(
        string root,
        IReadOnlyList<SearchPatternInput> inputPatterns,
        SearchManyOptionsInput? options)
    {
        ArgumentNullException.ThrowIfNull(inputPatterns);
        if (inputPatterns.Count == 0 ||
            inputPatterns.Count > SearchPatternLimits.MaxPatternCount)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "pattern-count",
                    SearchPatternLimits.MaxPatternCount));
        }

        ValidateRequiredText(root, "root");
        var effectiveOptions = options ?? new SearchManyOptionsInput();
        var effectiveText = NormalizeManyText(effectiveOptions.Text);
        var effectiveLimits = CreateLimits(effectiveOptions.Limits);
        var maxRecordBytesExplicit = effectiveOptions.Limits is not null &&
                                     effectiveOptions.Limits.Value.MaxRecordBytes != long.MaxValue;
        var effectiveScope = CreateScope(effectiveOptions.Scope);
        var patterns = new SearchManyPattern[inputPatterns.Count];
        var patternIds = new HashSet<string>(StringComparer.Ordinal);
        var regexCount = 0;

        for (var index = 0; index < inputPatterns.Count; index++)
        {
            var input = inputPatterns[index];
            ValidatePatternId(input.Id, index);
            if (!patternIds.Add(input.Id))
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidManyInput(
                        $"'patterns[{index}].id' must be unique",
                        SearchDiagnosticPhase.Argument));
            }
            var mode = ParsePatternMode(input.Mode, $"patterns[{index}].mode");
            if (input.Pattern is null)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidArgument($"patterns[{index}].pattern"));
            }

            if (mode == SearchPatternMode.Regex)
                regexCount++;

            patterns[index] = new SearchManyPattern(input.Id, input.Pattern, mode);
        }

        if (regexCount > SearchPatternLimits.MaxRegexPatternCount)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "regex-pattern-count",
                    SearchPatternLimits.MaxRegexPatternCount));
        }

        return new SearchManyRequest(
            patterns,
            new SearchManyOptions(
                SearchCasePolicy.Parse(effectiveText.CaseMode),
                effectiveText.WholeWord,
                SearchEncodingPolicy.Parse(effectiveText.Encoding),
                selection: SearchSelectionMode.LeftmostFirstNonOverlapping,
                take: null,
                partialPolicy: SearchPartialPolicy.Reject,
                validation: SearchValidationMode.FullInput,
                effectiveScope,
                effectiveLimits,
                maxRecordBytesExplicit,
                CreateContext(effectiveOptions.Context, effectiveLimits)));
    }

    public static SearchBytePattern CreateBytes(
        string patternHex,
        SearchBytesOptionsInput? options)
    {
        ValidateRequiredText(patternHex, "patternHex");
        var effectiveOptions = options ?? new SearchBytesOptionsInput();
        var window = effectiveOptions.Window ?? new SearchBytesWindowInput();
        var limits = CreateLimits(effectiveOptions.Limits);
        limits.ValidatePattern(patternHex);
        return SearchBytePatternParser.ParseHex(
            patternHex,
            effectiveOptions.MaskHex,
            window.BeforeBytes,
            window.AfterBytes);
    }

    public static ScopePolicy CreateScope(SearchScopeInput? input)
    {
        if (input is null)
            return ScopePolicy.Default;

        var value = input.Value;
        var repositoryIgnores = ParseRepositoryIgnores(value.RepositoryIgnores);
        var globalIgnores = ParseGlobalIgnores(value.GlobalIgnores);
        var inaccessible = value.InaccessibleEntries is null
            ? InaccessibleEntryPolicy.Fail
            : ParseInaccessibleEntries(value.InaccessibleEntries);

        if (globalIgnores == GlobalIgnorePolicy.Disabled &&
            value.GlobalIgnoreRules is { Count: > 0 })
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("scope.globalIgnoreRules"));
        }

        if (globalIgnores == GlobalIgnorePolicy.Configured &&
            (value.GlobalIgnoreRules is null || value.GlobalIgnoreRules.Count == 0))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("scope.globalIgnoreRules"));
        }

        return new ScopePolicy(
            value.Recursive,
            value.Include,
            value.Exclude,
            repositoryIgnores,
            globalIgnores,
            value.HiddenEntries
                ? HiddenEntryPolicy.Include
                : HiddenEntryPolicy.Exclude,
            value.FollowLinks
                ? LinkTraversalPolicy.Follow
                : LinkTraversalPolicy.DoNotFollow,
            inaccessible,
            value.GlobalIgnoreRules,
            CreateMetadata(value.Metadata));
    }

    public static SearchResourceLimits CreateLimits(SearchLimitsInput? input)
    {
        if (input is null)
            return SearchResourceLimits.Default;

        var value = input.Value;
        var maxRecordBytes = value.MaxRecordBytes == long.MaxValue
            ? SearchRegexScanner.MaxMultilineRecordBytes
            : value.MaxRecordBytes;
        return new SearchResourceLimits(
            value.MaxTotalBytes,
            value.MaxFileBytes,
            value.MaxFiles,
            value.MaxPatternCount,
            value.MaxPatternLength,
            value.MaxPatternBytes,
            value.MaxPatternCompilationMilliseconds,
            maxRecordBytes,
            value.MaxContextBytes,
            value.MaxInFlightOutputBytes,
            value.MaxMatchCount);
    }

    public static SearchContextOptions CreateContext(
        SearchContextInput? input,
        SearchResourceLimits limits)
    {
        if (input is null)
            return SearchContextOptions.Disabled;

        var value = input.Value;
        if (value.MaxBytes > limits.MaxContextBytes)
        {
            throw limits.Exhausted(
                "context-bytes",
                limits.MaxContextBytes,
                "context-bytes");
        }

        return new SearchContextOptions(
            value.BeforeLines,
            value.AfterLines,
            value.MaxBytes);
    }

    private static SearchTextInput NormalizeText(SearchTextInput? input)
    {
        if (input is null)
        {
            return new SearchTextInput(
                "literal",
                "sensitive",
                "auto",
                false);
        }

        var value = input.Value;
        return new SearchTextInput(
            value.Mode ?? "literal",
            value.CaseMode ?? "sensitive",
            value.Encoding ?? "auto",
            value.WholeWord);
    }

    private static SearchManyTextInput NormalizeManyText(SearchManyTextInput? input)
    {
        if (input is null)
        {
            return new SearchManyTextInput(
                "sensitive",
                "auto",
                false);
        }

        var value = input.Value;
        return new SearchManyTextInput(
            value.CaseMode ?? "sensitive",
            value.Encoding ?? "auto",
            value.WholeWord);
    }

    private static void ValidateRequiredText(string? value, string argumentName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(argumentName));
        }
    }

    private static SearchMetadataPolicy CreateMetadata(SearchMetadataInput? input)
    {
        if (input is null)
            return SearchMetadataPolicy.Default;

        var value = input.Value;
        return new SearchMetadataPolicy(
            value.NameIncludes,
            value.NameExcludes,
            value.ExtensionIncludes,
            value.ExtensionExcludes,
            value.MinimumSizeBytes,
            value.MaximumSizeBytes,
            ParseUtc(value.ModifiedAfterOrEqualUtc, "scope.metadata.modifiedAfterOrEqualUtc"),
            ParseUtc(value.ModifiedBeforeOrEqualUtc, "scope.metadata.modifiedBeforeOrEqualUtc"));
    }

    private static DateTimeOffset? ParseUtc(string? value, string name)
    {
        if (value is null)
            return null;

        if (!value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) &&
            value.LastIndexOfAny(['+', '-']) <= value.IndexOf('T'))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(name));
        }

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(name));
        }

        return parsed.ToUniversalTime();
    }

    private static SearchRecordFraming CreateFraming(SearchRecordsInput input)
    {
        if (string.IsNullOrEmpty(input.StartDelimiter) ||
            string.IsNullOrEmpty(input.EndDelimiter))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("records"));
        }

        return new SearchRecordFraming(
            input.StartDelimiter,
            input.EndDelimiter,
            input.AllowUnterminatedEof);
    }

    private static int ValidateRecordBytes(long value, string name)
    {
        if (value <= 0 || value > SearchRegexScanner.MaxMultilineRecordBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    name,
                    SearchRegexScanner.MaxMultilineRecordBytes));
        }

        return checked((int)value);
    }

    private static SearchPatternMode ParsePatternMode(string? value, string argumentName)
    {
        if (value is not null && value.Equals("literal", StringComparison.OrdinalIgnoreCase))
            return SearchPatternMode.Literal;
        if (value is not null && value.Equals("regex", StringComparison.OrdinalIgnoreCase))
            return SearchPatternMode.Regex;

        throw new SearchRequestException(
            SearchDiagnosticCatalog.InvalidArgument(argumentName));
    }

    private static SearchRecordMode ParseRecordMode(string? value)
    {
        if (value is not null && value.Equals("physical-line", StringComparison.OrdinalIgnoreCase))
            return SearchRecordMode.PhysicalLine;
        if (value is not null && value.Equals("bounded-multiline", StringComparison.OrdinalIgnoreCase))
            return SearchRecordMode.BoundedMultiline;

        throw new SearchRequestException(
            SearchDiagnosticCatalog.InvalidArgument("records.mode"));
    }

    private static RepositoryIgnorePolicy ParseRepositoryIgnores(string? value)
    {
        return (value ?? "respect").ToLowerInvariant() switch
        {
            "respect" => RepositoryIgnorePolicy.Respect,
            "disabled" => RepositoryIgnorePolicy.Disabled,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("scope.repositoryIgnores"))
        };
    }

    private static GlobalIgnorePolicy ParseGlobalIgnores(string? value)
    {
        return (value ?? "disabled").ToLowerInvariant() switch
        {
            "disabled" => GlobalIgnorePolicy.Disabled,
            "configured" => GlobalIgnorePolicy.Configured,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("scope.globalIgnores"))
        };
    }

    private static InaccessibleEntryPolicy ParseInaccessibleEntries(string? value)
    {
        return (value ?? "fail").ToLowerInvariant() switch
        {
            "fail" => InaccessibleEntryPolicy.Fail,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("scope.inaccessibleEntries"))
        };
    }

    private static void ValidatePatternId(string? value, int index)
    {
        if (string.IsNullOrEmpty(value) || value.Length > SearchPatternLimits.MaxPatternIdLength)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyInput(
                    $"'patterns[{index}].id' must be an ASCII identifier",
                    SearchDiagnosticPhase.Argument));
        }

        if (!IsAsciiIdentifierStart(value[0]))
            throw InvalidPatternId(index);

        for (var characterIndex = 1; characterIndex < value.Length; characterIndex++)
        {
            if (!IsAsciiIdentifierPart(value[characterIndex]))
                throw InvalidPatternId(index);
        }
    }

    private static SearchRequestException InvalidPatternId(int index)
    {
        return new SearchRequestException(
            SearchDiagnosticCatalog.InvalidManyInput(
                $"'patterns[{index}].id' must be an ASCII identifier",
                SearchDiagnosticPhase.Argument));
    }

    private static bool IsAsciiIdentifierStart(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static bool IsAsciiIdentifierPart(char value)
    {
        return IsAsciiIdentifierStart(value) || value is '.' or '_' or '-';
    }
}

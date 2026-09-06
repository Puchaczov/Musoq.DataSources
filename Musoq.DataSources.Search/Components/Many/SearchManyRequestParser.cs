#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Many;

internal static class SearchManyRequestParser
{
    public const int MaxPatternCount = 1_024;

    public const int MaxPatternIdLength = 128;

    public const int MaxPatternTextLength = SearchRegexBackend.MaxPatternLength;

    public const int MaxScopeListCount = 128;

    public const int MaxScopeEntryLength = 4_096;

    // The scalar transport remains deliberately bounded before JSON parsing.
    public const int MaxRequestCharacters = 8 * 1024 * 1024;

    private const int MaxJsonDepth = 32;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static SearchManyRequest Parse(string? requestJson)
    {
        if (requestJson is null)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    "requestJson must be a non-null scalar string",
                    SearchDiagnosticPhase.Argument));
        }

        if (requestJson.Length == 0 || string.IsNullOrWhiteSpace(requestJson))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    "requestJson must contain one non-empty JSON value",
                    SearchDiagnosticPhase.Argument));
        }

        if (requestJson.Length > MaxRequestCharacters)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ManyRequestResourceLimit(
                    "requestJson characters",
                    MaxRequestCharacters));
        }

        byte[] utf8;
        try
        {
            utf8 = StrictUtf8.GetBytes(requestJson);
        }
        catch (EncoderFallbackException exception)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    "requestJson contains an invalid UTF-16 scalar",
                    SearchDiagnosticPhase.Syntax),
                exception);
        }

        try
        {
            ValidateDuplicateProperties(utf8);
            return new RequestReader(utf8).Parse();
        }
        catch (SearchRequestException)
        {
            throw;
        }
        catch (SearchResourceLimitException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    "requestJson is not valid JSON",
                    SearchDiagnosticPhase.Syntax,
                    GetJsonErrorOffset(utf8, exception),
                    1),
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    "requestJson contains a value with an invalid JSON type",
                    SearchDiagnosticPhase.Syntax),
                exception);
        }
    }

    private static long GetJsonErrorOffset(ReadOnlySpan<byte> json, JsonException exception)
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

    private static void ValidateDuplicateProperties(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(
            json,
            isFinalBlock: true,
            state: new JsonReaderState(
                new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaxJsonDepth
                }));
        var objectProperties = new Stack<HashSet<string>>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    objectProperties.Pop();
                    break;
                case JsonTokenType.PropertyName:
                {
                    var name = reader.GetString() ?? string.Empty;
                    if (!objectProperties.Peek().Add(name))
                    {
                        throw new SearchRequestException(
                            SearchDiagnosticCatalog.InvalidManyRequest(
                                $"duplicate JSON property '{Display(name)}'",
                                SearchDiagnosticPhase.Argument,
                                reader.TokenStartIndex));
                    }

                    break;
                }
            }
        }
    }

    private static string Display(string value)
    {
        return SearchDiagnosticText.Display(value);
    }

    private static bool IsPatternId(string value)
    {
        if (value.Length is < 1 or > MaxPatternIdLength)
            return false;

        if (!IsAsciiIdentifierStart(value[0]))
            return false;

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAsciiIdentifierPart(character))
                return false;
        }

        return true;
    }

    private static bool IsAsciiIdentifierStart(char character)
    {
        return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static bool IsAsciiIdentifierPart(char character)
    {
        return IsAsciiIdentifierStart(character) || character is '.' or '_' or '-';
    }

    private static SearchPatternMode ParsePatternMode(
        string? value,
        long offset,
        string propertyPath)
    {
        return value switch
        {
            "literal" => SearchPatternMode.Literal,
            "regex" => SearchPatternMode.Regex,
            null => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'{propertyPath}' is required",
                    SearchDiagnosticPhase.Argument,
                    offset)),
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'{propertyPath}' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static SearchCaseMode ParseCase(
        string? value,
        long offset)
    {
        return value switch
        {
            null => SearchCaseMode.Sensitive,
            "sensitive" => SearchCaseMode.Sensitive,
            "insensitive" => SearchCaseMode.Insensitive,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.case' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static SearchEncodingMode ParseEncoding(
        string? value,
        long offset)
    {
        return value switch
        {
            null => SearchEncodingMode.Auto,
            "auto" => SearchEncodingMode.Auto,
            "utf8" => SearchEncodingMode.Utf8,
            "utf8-bom" => SearchEncodingMode.Utf8Bom,
            "utf16-le-bom" => SearchEncodingMode.Utf16LittleEndianBom,
            "utf16-be-bom" => SearchEncodingMode.Utf16BigEndianBom,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.encoding' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static SearchSelectionMode ParseSelection(
        string? value,
        long offset)
    {
        return value switch
        {
            null => SearchSelectionMode.LeftmostFirstNonOverlapping,
            "leftmost-first-non-overlapping" => SearchSelectionMode.LeftmostFirstNonOverlapping,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.selection' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static SearchPartialPolicy ParsePartialPolicy(
        string? value,
        long offset)
    {
        return value switch
        {
            null => SearchPartialPolicy.Reject,
            "reject" => SearchPartialPolicy.Reject,
            "allow" => SearchPartialPolicy.Allow,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.partialPolicy' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static SearchValidationMode ParseValidation(
        string? value,
        long offset)
    {
        return value switch
        {
            null => SearchValidationMode.FullInput,
            "full-input" => SearchValidationMode.FullInput,
            "observed-prefix" => SearchValidationMode.ObservedPrefix,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.validation' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static RepositoryIgnorePolicy ParseRepositoryIgnores(
        string? value,
        long offset)
    {
        return value switch
        {
            null => RepositoryIgnorePolicy.Respect,
            "respect" => RepositoryIgnorePolicy.Respect,
            "disabled" => RepositoryIgnorePolicy.Disabled,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.scope.repositoryIgnores' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static GlobalIgnorePolicy ParseGlobalIgnores(
        string? value,
        long offset)
    {
        return value switch
        {
            null => GlobalIgnorePolicy.Disabled,
            "disabled" => GlobalIgnorePolicy.Disabled,
            "configured" => GlobalIgnorePolicy.Configured,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.scope.globalIgnores' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static HiddenEntryPolicy ParseHiddenEntries(
        string? value,
        long offset)
    {
        return value switch
        {
            null => HiddenEntryPolicy.Exclude,
            "exclude" => HiddenEntryPolicy.Exclude,
            "include" => HiddenEntryPolicy.Include,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.scope.hiddenEntries' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static LinkTraversalPolicy ParseFollowLinks(
        string? value,
        long offset)
    {
        return value switch
        {
            null => LinkTraversalPolicy.DoNotFollow,
            "do not follow" => LinkTraversalPolicy.DoNotFollow,
            "follow" => LinkTraversalPolicy.Follow,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.scope.followLinks' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private static InaccessibleEntryPolicy ParseInaccessibleEntries(
        string? value,
        long offset)
    {
        return value switch
        {
            null => InaccessibleEntryPolicy.Fail,
            "fail" => InaccessibleEntryPolicy.Fail,
            "skip" => InaccessibleEntryPolicy.Skip,
            _ => throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    $"'options.scope.inaccessibleEntries' has unsupported value '{Display(value)}'",
                    SearchDiagnosticPhase.Argument,
                    offset))
        };
    }

    private sealed class RawRequest
    {
        public int? Version { get; init; }

        public long VersionOffset { get; init; }

        public List<RawPattern>? Patterns { get; init; }

        public long PatternsOffset { get; init; }

        public RawOptions? Options { get; init; }
    }

    private sealed class RawPattern
    {
        public string? Id { get; init; }

        public long IdOffset { get; init; }

        public string? Pattern { get; init; }

        public long PatternOffset { get; init; }

        public string? Mode { get; init; }

        public long ModeOffset { get; init; }

    }

    private sealed class RawOptions
    {
        public string? Case { get; init; }

        public long CaseOffset { get; init; }

        public bool? WholeWord { get; init; }

        public string? Encoding { get; init; }

        public long EncodingOffset { get; init; }

        public string? Selection { get; init; }

        public long SelectionOffset { get; init; }

        public long? Take { get; init; }

        public long TakeOffset { get; init; }

        public string? PartialPolicy { get; init; }

        public long PartialPolicyOffset { get; init; }

        public string? Validation { get; init; }

        public long ValidationOffset { get; init; }

        public RawScope? Scope { get; init; }
    }

    private sealed class RawScope
    {
        public bool? Recursive { get; init; }

        public IReadOnlyList<string> Include { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Exclude { get; init; } = Array.Empty<string>();

        public string? RepositoryIgnores { get; init; }

        public long RepositoryIgnoresOffset { get; init; }

        public string? GlobalIgnores { get; init; }

        public long GlobalIgnoresOffset { get; init; }

        public string? HiddenEntries { get; init; }

        public long HiddenEntriesOffset { get; init; }

        public string? FollowLinks { get; init; }

        public long FollowLinksOffset { get; init; }

        public string? InaccessibleEntries { get; init; }

        public long InaccessibleEntriesOffset { get; init; }
    }

    private ref struct RequestReader
    {
        private Utf8JsonReader _reader;

        public RequestReader(ReadOnlySpan<byte> json)
        {
            _reader = new Utf8JsonReader(
                json,
                isFinalBlock: true,
                state: new JsonReaderState(
                    new JsonReaderOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = MaxJsonDepth
                    }));
        }

        public SearchManyRequest Parse()
        {
            ReadNext("requestJson");
            if (_reader.TokenType != JsonTokenType.StartObject)
            {
                Reject(
                    "requestJson must be one JSON object",
                    _reader.TokenStartIndex,
                    SearchDiagnosticPhase.Argument);
            }

            var raw = ParseRequestObject();
            if (_reader.Read())
            {
                Reject(
                    "requestJson must contain exactly one JSON value",
                    _reader.TokenStartIndex,
                    SearchDiagnosticPhase.Syntax);
            }

            return Build(raw);
        }

        private RawRequest ParseRequestObject()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            int? version = null;
            var versionOffset = 0L;
            List<RawPattern>? patterns = null;
            var patternsOffset = 0L;
            RawOptions? options = null;

            while (ReadProperty(names, "requestJson", out var name, out var propertyOffset))
            {
                switch (name)
                {
                    case "version":
                        versionOffset = propertyOffset;
                        ReadNext("version");
                        version = ReadInt32("version", propertyOffset);
                        break;
                    case "patterns":
                        patternsOffset = propertyOffset;
                        patterns = ParsePatterns();
                        break;
                    case "options":
                        options = ParseOptions(propertyOffset);
                        break;
                    default:
                        RejectUnknownProperty(name, propertyOffset, "request");
                        break;
                }
            }

            return new RawRequest
            {
                Version = version,
                VersionOffset = versionOffset,
                Patterns = patterns,
                PatternsOffset = patternsOffset,
                Options = options
            };
        }

        private List<RawPattern> ParsePatterns()
        {
            ReadNext("patterns");
            if (_reader.TokenType != JsonTokenType.StartArray)
            {
                Reject(
                    "'patterns' must be an array",
                    _reader.TokenStartIndex,
                    SearchDiagnosticPhase.Argument);
            }

            var patterns = new List<RawPattern>();
            while (true)
            {
                ReadNext("patterns");
                if (_reader.TokenType == JsonTokenType.EndArray)
                    return patterns;

                if (patterns.Count >= MaxPatternCount)
                {
                    Resource(
                        "patterns",
                        MaxPatternCount,
                        _reader.TokenStartIndex);
                }

                if (_reader.TokenType != JsonTokenType.StartObject)
                {
                    Reject(
                        $"'patterns[{patterns.Count}]' must be an object",
                        _reader.TokenStartIndex,
                        SearchDiagnosticPhase.Argument);
                }

                patterns.Add(ParsePattern(patterns.Count));
            }
        }

        private RawPattern ParsePattern(int index)
        {
            var objectOffset = _reader.TokenStartIndex;
            var names = new HashSet<string>(StringComparer.Ordinal);
            string? id = null;
            var idOffset = objectOffset;
            string? pattern = null;
            var patternOffset = objectOffset;
            string? mode = null;
            var modeOffset = objectOffset;

            while (ReadProperty(names, $"patterns[{index}]", out var name, out var propertyOffset))
            {
                switch (name)
                {
                    case "id":
                        idOffset = propertyOffset;
                        ReadNext($"patterns[{index}].id");
                        id = ReadString($"patterns[{index}].id", propertyOffset);
                        break;
                    case "pattern":
                        patternOffset = propertyOffset;
                        ReadNext($"patterns[{index}].pattern");
                        pattern = ReadString($"patterns[{index}].pattern", propertyOffset);
                        if (pattern.Length > MaxPatternTextLength)
                        {
                            Resource(
                                $"patterns[{index}].pattern",
                                MaxPatternTextLength,
                                propertyOffset);
                        }

                        break;
                    case "mode":
                        modeOffset = propertyOffset;
                        ReadNext($"patterns[{index}].mode");
                        mode = ReadString($"patterns[{index}].mode", propertyOffset);
                        break;
                    default:
                        RejectUnknownProperty(name, propertyOffset, $"patterns[{index}]");
                        break;
                }
            }

            if (id is null)
                Reject($"'patterns[{index}].id' is required", objectOffset, SearchDiagnosticPhase.Argument);
            if (pattern is null)
                Reject($"'patterns[{index}].pattern' is required", objectOffset, SearchDiagnosticPhase.Argument);
            if (mode is null)
                Reject($"'patterns[{index}].mode' is required", objectOffset, SearchDiagnosticPhase.Argument);

            return new RawPattern
            {
                Id = id,
                IdOffset = idOffset,
                Pattern = pattern,
                PatternOffset = patternOffset,
                Mode = mode,
                ModeOffset = modeOffset,
            };
        }

        private RawOptions ParseOptions(long propertyOffset)
        {
            ReadNext("options");
            if (_reader.TokenType != JsonTokenType.StartObject)
            {
                Reject(
                    "'options' must be an object",
                    _reader.TokenStartIndex,
                    SearchDiagnosticPhase.Argument);
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            string? caseMode = null;
            var caseOffset = propertyOffset;
            bool? wholeWord = null;
            string? encoding = null;
            var encodingOffset = propertyOffset;
            string? selection = null;
            var selectionOffset = propertyOffset;
            long? take = null;
            var takeOffset = propertyOffset;
            string? partialPolicy = null;
            var partialPolicyOffset = propertyOffset;
            string? validation = null;
            var validationOffset = propertyOffset;
            RawScope? scope = null;

            while (ReadProperty(names, "options", out var name, out var currentPropertyOffset))
            {
                switch (name)
                {
                    case "case":
                        caseOffset = currentPropertyOffset;
                        ReadNext("options.case");
                        caseMode = ReadString("options.case", currentPropertyOffset);
                        break;
                    case "wholeWord":
                        ReadNext("options.wholeWord");
                        wholeWord = ReadBoolean("options.wholeWord", currentPropertyOffset);
                        break;
                    case "encoding":
                        encodingOffset = currentPropertyOffset;
                        ReadNext("options.encoding");
                        encoding = ReadString("options.encoding", currentPropertyOffset);
                        break;
                    case "selection":
                        selectionOffset = currentPropertyOffset;
                        ReadNext("options.selection");
                        selection = ReadString("options.selection", currentPropertyOffset);
                        break;
                    case "take":
                        takeOffset = currentPropertyOffset;
                        ReadNext("options.take");
                        take = ReadInt64("options.take", currentPropertyOffset);
                        break;
                    case "partialPolicy":
                        partialPolicyOffset = currentPropertyOffset;
                        ReadNext("options.partialPolicy");
                        partialPolicy = ReadString("options.partialPolicy", currentPropertyOffset);
                        break;
                    case "validation":
                        validationOffset = currentPropertyOffset;
                        ReadNext("options.validation");
                        validation = ReadString("options.validation", currentPropertyOffset);
                        break;
                    case "scope":
                        scope = ParseScope(currentPropertyOffset);
                        break;
                    default:
                        RejectUnknownProperty(name, currentPropertyOffset, "options");
                        break;
                }
            }

            return new RawOptions
            {
                Case = caseMode,
                CaseOffset = caseOffset,
                WholeWord = wholeWord,
                Encoding = encoding,
                EncodingOffset = encodingOffset,
                Selection = selection,
                SelectionOffset = selectionOffset,
                Take = take,
                TakeOffset = takeOffset,
                PartialPolicy = partialPolicy,
                PartialPolicyOffset = partialPolicyOffset,
                Validation = validation,
                ValidationOffset = validationOffset,
                Scope = scope
            };
        }

        private RawScope ParseScope(long propertyOffset)
        {
            ReadNext("options.scope");
            if (_reader.TokenType != JsonTokenType.StartObject)
            {
                Reject(
                    "'options.scope' must be an object",
                    _reader.TokenStartIndex,
                    SearchDiagnosticPhase.Argument);
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            bool? recursive = null;
            IReadOnlyList<string> include = Array.Empty<string>();
            IReadOnlyList<string> exclude = Array.Empty<string>();
            string? repositoryIgnores = null;
            var repositoryIgnoresOffset = propertyOffset;
            string? globalIgnores = null;
            var globalIgnoresOffset = propertyOffset;
            string? hiddenEntries = null;
            var hiddenEntriesOffset = propertyOffset;
            string? followLinks = null;
            var followLinksOffset = propertyOffset;
            string? inaccessibleEntries = null;
            var inaccessibleEntriesOffset = propertyOffset;

            while (ReadProperty(names, "options.scope", out var name, out var currentPropertyOffset))
            {
                switch (name)
                {
                    case "recursive":
                        ReadNext("options.scope.recursive");
                        recursive = ReadBoolean("options.scope.recursive", currentPropertyOffset);
                        break;
                    case "include":
                        include = ReadStringArray("options.scope.include", currentPropertyOffset);
                        break;
                    case "exclude":
                        exclude = ReadStringArray("options.scope.exclude", currentPropertyOffset);
                        break;
                    case "repositoryIgnores":
                        repositoryIgnoresOffset = currentPropertyOffset;
                        ReadNext("options.scope.repositoryIgnores");
                        repositoryIgnores = ReadString("options.scope.repositoryIgnores", currentPropertyOffset);
                        break;
                    case "globalIgnores":
                        globalIgnoresOffset = currentPropertyOffset;
                        ReadNext("options.scope.globalIgnores");
                        globalIgnores = ReadString("options.scope.globalIgnores", currentPropertyOffset);
                        break;
                    case "hiddenEntries":
                        hiddenEntriesOffset = currentPropertyOffset;
                        ReadNext("options.scope.hiddenEntries");
                        hiddenEntries = ReadString("options.scope.hiddenEntries", currentPropertyOffset);
                        break;
                    case "followLinks":
                        followLinksOffset = currentPropertyOffset;
                        ReadNext("options.scope.followLinks");
                        followLinks = ReadString("options.scope.followLinks", currentPropertyOffset);
                        break;
                    case "inaccessibleEntries":
                        inaccessibleEntriesOffset = currentPropertyOffset;
                        ReadNext("options.scope.inaccessibleEntries");
                        inaccessibleEntries = ReadString("options.scope.inaccessibleEntries", currentPropertyOffset);
                        break;
                    default:
                        RejectUnknownProperty(name, currentPropertyOffset, "options.scope");
                        break;
                }
            }

            return new RawScope
            {
                Recursive = recursive,
                Include = include,
                Exclude = exclude,
                RepositoryIgnores = repositoryIgnores,
                RepositoryIgnoresOffset = repositoryIgnoresOffset,
                GlobalIgnores = globalIgnores,
                GlobalIgnoresOffset = globalIgnoresOffset,
                HiddenEntries = hiddenEntries,
                HiddenEntriesOffset = hiddenEntriesOffset,
                FollowLinks = followLinks,
                FollowLinksOffset = followLinksOffset,
                InaccessibleEntries = inaccessibleEntries,
                InaccessibleEntriesOffset = inaccessibleEntriesOffset
            };
        }

        private IReadOnlyList<string> ReadStringArray(string path, long propertyOffset)
        {
            ReadNext(path);
            if (_reader.TokenType != JsonTokenType.StartArray)
            {
                Reject(
                    $"'{path}' must be an array",
                    _reader.TokenStartIndex,
                    SearchDiagnosticPhase.Argument);
            }

            var values = new List<string>();
            while (true)
            {
                ReadNext(path);
                if (_reader.TokenType == JsonTokenType.EndArray)
                    return values.AsReadOnly();

                if (values.Count >= MaxScopeListCount)
                {
                    Resource(path, MaxScopeListCount, _reader.TokenStartIndex);
                }

                var value = ReadString(path, _reader.TokenStartIndex);
                if (value.Length > MaxScopeEntryLength)
                {
                    Resource(path, MaxScopeEntryLength, propertyOffset);
                }

                values.Add(value);
            }
        }

        private int ReadInt32(string path, long propertyOffset)
        {
            var value = 0;
            if (_reader.TokenType != JsonTokenType.Number ||
                !_reader.TryGetInt32(out value))
            {
                Reject($"'{path}' must be an integer", propertyOffset, SearchDiagnosticPhase.Argument);
            }

            return value;
        }

        private long ReadInt64(string path, long propertyOffset)
        {
            var value = 0L;
            if (_reader.TokenType != JsonTokenType.Number ||
                !_reader.TryGetInt64(out value))
            {
                Reject($"'{path}' must be an integer", propertyOffset, SearchDiagnosticPhase.Argument);
            }

            return value;
        }

        private bool ReadBoolean(string path, long propertyOffset)
        {
            if (_reader.TokenType is not JsonTokenType.True and not JsonTokenType.False)
            {
                Reject($"'{path}' must be a boolean", propertyOffset, SearchDiagnosticPhase.Argument);
            }

            return _reader.TokenType == JsonTokenType.True;
        }

        private string ReadString(string path, long propertyOffset)
        {
            if (_reader.TokenType != JsonTokenType.String)
            {
                Reject($"'{path}' must be a string", propertyOffset, SearchDiagnosticPhase.Argument);
            }

            return _reader.GetString() ??
                   throw new SearchRequestException(
                       SearchDiagnosticCatalog.InvalidManyRequest(
                           $"'{path}' must not be null",
                           SearchDiagnosticPhase.Argument,
                           propertyOffset));
        }

        private bool ReadProperty(
            HashSet<string> names,
            string objectPath,
            out string name,
            out long propertyOffset)
        {
            ReadNext(objectPath);
            if (_reader.TokenType == JsonTokenType.EndObject)
            {
                name = string.Empty;
                propertyOffset = _reader.TokenStartIndex;
                return false;
            }

            if (_reader.TokenType != JsonTokenType.PropertyName)
            {
                Reject(
                    $"'{objectPath}' must contain JSON properties",
                    _reader.TokenStartIndex,
                    SearchDiagnosticPhase.Syntax);
            }

            name = _reader.GetString() ?? string.Empty;
            propertyOffset = _reader.TokenStartIndex;
            if (!names.Add(name))
            {
                Reject(
                    $"duplicate JSON property '{Display(name)}' in '{objectPath}'",
                    propertyOffset,
                    SearchDiagnosticPhase.Argument);
            }

            return true;
        }

        private void RejectUnknownProperty(string name, long propertyOffset, string objectPath)
        {
            Reject(
                $"unknown property '{Display(name)}' in '{objectPath}'",
                propertyOffset,
                SearchDiagnosticPhase.Argument);
        }

        private void ReadNext(string path)
        {
            if (!_reader.Read())
            {
                Reject(
                    $"'{path}' ended before the JSON value was complete",
                    _reader.BytesConsumed,
                    SearchDiagnosticPhase.Syntax);
            }
        }

        private void Reject(
            string reason,
            long offset,
            SearchDiagnosticPhase phase,
            long length = 1)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyRequest(
                    reason,
                    phase,
                    Math.Max(0, offset),
                    Math.Max(1, length)));
        }

        private void Resource(string field, int limit, long offset)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ManyRequestResourceLimit(
                    field,
                    limit,
                    Math.Max(0, offset)));
        }

        private SearchManyRequest Build(RawRequest raw)
        {
            if (raw.Version is null)
                Reject("'version' is required", 0, SearchDiagnosticPhase.Argument);
            if (raw.Version != SearchManyRequest.CurrentVersion)
            {
                Reject(
                    $"'version' must be {SearchManyRequest.CurrentVersion}",
                    raw.VersionOffset,
                    SearchDiagnosticPhase.Argument);
            }

            var rawPatterns = raw.Patterns ??
                              throw new SearchRequestException(
                                  SearchDiagnosticCatalog.InvalidManyRequest(
                                      "'patterns' is required",
                                      SearchDiagnosticPhase.Argument,
                                      raw.PatternsOffset));
            if (rawPatterns.Count == 0)
                Reject("'patterns' must contain at least one pattern", raw.PatternsOffset, SearchDiagnosticPhase.Argument);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var patterns = new List<SearchManyPattern>(rawPatterns.Count);
            foreach (var rawPattern in rawPatterns)
            {
                if (!IsPatternId(rawPattern.Id!))
                {
                    Reject(
                        $"'patterns[].id' '{Display(rawPattern.Id!)}' is not a valid ASCII identifier",
                        rawPattern.IdOffset,
                        SearchDiagnosticPhase.Argument);
                }

                if (!ids.Add(rawPattern.Id!))
                {
                    Reject(
                        $"duplicate pattern ID '{Display(rawPattern.Id!)}'",
                        rawPattern.IdOffset,
                        SearchDiagnosticPhase.Argument);
                }

                if (rawPattern.Pattern!.Length == 0)
                {
                    Reject(
                        "'patterns[].pattern' must not be empty",
                        rawPattern.PatternOffset,
                        SearchDiagnosticPhase.Argument);
                }

                var mode = ParsePatternMode(
                    rawPattern.Mode,
                    rawPattern.ModeOffset,
                    "patterns[].mode");
                patterns.Add(new SearchManyPattern(rawPattern.Id!, rawPattern.Pattern!, mode));
            }

            var options = BuildOptions(raw.Options);
            return new SearchManyRequest(patterns, options);
        }

        private SearchManyOptions BuildOptions(RawOptions? raw)
        {
            if (raw is null)
                return SearchManyOptions.Default;

            if (raw.Take is < 1 or > 1_000_000)
            {
                Reject(
                    "'options.take' must be an integer from 1 through 1000000",
                    raw.TakeOffset,
                    SearchDiagnosticPhase.Argument);
            }

            var scope = BuildScope(raw.Scope);
            return new SearchManyOptions(
                ParseCase(raw.Case, raw.CaseOffset),
                raw.WholeWord ?? false,
                ParseEncoding(raw.Encoding, raw.EncodingOffset),
                ParseSelection(raw.Selection, raw.SelectionOffset),
                raw.Take is null ? null : checked((int)raw.Take.Value),
                ParsePartialPolicy(raw.PartialPolicy, raw.PartialPolicyOffset),
                ParseValidation(raw.Validation, raw.ValidationOffset),
                scope);
        }

        private ScopePolicy BuildScope(RawScope? raw)
        {
            if (raw is null)
                return ScopePolicy.Default;

            return new ScopePolicy(
                raw.Recursive ?? true,
                raw.Include,
                raw.Exclude,
                ParseRepositoryIgnores(raw.RepositoryIgnores, raw.RepositoryIgnoresOffset),
                ParseGlobalIgnores(raw.GlobalIgnores, raw.GlobalIgnoresOffset),
                ParseHiddenEntries(raw.HiddenEntries, raw.HiddenEntriesOffset),
                ParseFollowLinks(raw.FollowLinks, raw.FollowLinksOffset),
                ParseInaccessibleEntries(raw.InaccessibleEntries, raw.InaccessibleEntriesOffset));
        }
    }
}

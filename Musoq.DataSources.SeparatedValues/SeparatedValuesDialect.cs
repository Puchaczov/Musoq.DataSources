#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Musoq.DataSources.SeparatedValues;

/// <summary>
/// The normalized, immutable grammar used by one separated-values scan.
/// </summary>
internal sealed class SeparatedValuesDialect
{
    public const string QuoteSettingName = "separatedvalues.quote_char";
    public const string EscapeSettingName = "separatedvalues.escape_mode";
    public const string WhitespaceSettingName = "separatedvalues.whitespace_mode";
    public const string BlankRecordSettingName = "separatedvalues.blank_record_mode";
    public const string CommentPrefixSettingName = "separatedvalues.comment_prefix";
    public const string NullTokensSettingName = "separatedvalues.null_tokens";
    public const string CultureSettingName = "separatedvalues.value_culture";
    public const string RecordEndingsSettingName = "separatedvalues.record_endings";
    public const string MaximumRecordBytesSettingName = "separatedvalues.max_record_bytes";
    public const string MaximumBufferedBytesSettingName = "separatedvalues.max_buffered_bytes";
    public const int DefaultMaximumRecordBytes = 256 * 1024 * 1024;
    public const int DefaultMaximumBufferedBytes = 256 * 1024 * 1024;

    private SeparatedValuesDialect(
        byte separator,
        byte? quote,
        SeparatedValuesEscapeMode escapeMode,
        SeparatedValuesWhitespaceMode whitespaceMode,
        SeparatedValuesBlankRecordMode blankRecordMode,
        ImmutableArray<byte> commentPrefix,
        ImmutableArray<ImmutableArray<byte>> nullTokens,
        string cultureName,
        SeparatedValuesRecordEndingMode recordEndingMode,
        int maximumRecordBytes,
        int maximumBufferedBytes)
    {
        ValidateSeparator(separator, quote);
        if (maximumRecordBytes <= 0 || maximumRecordBytes > 1024 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maximumRecordBytes));
        if (maximumBufferedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBufferedBytes));
        if (commentPrefix.Length > 32)
            throw new ArgumentException("A comment prefix may contain at most 32 UTF-8 bytes.", nameof(commentPrefix));

        Separator = separator;
        Quote = quote;
        EscapeMode = escapeMode;
        WhitespaceMode = whitespaceMode;
        BlankRecordMode = blankRecordMode;
        CommentPrefix = commentPrefix;
        NullTokens = nullTokens;
        CultureName = string.IsNullOrWhiteSpace(cultureName) ? "invariant" : cultureName;
        RecordEndingMode = recordEndingMode;
        MaximumRecordBytes = maximumRecordBytes;
        MaximumBufferedBytes = maximumBufferedBytes;
        Fingerprint = CreateFingerprint();
    }

    public byte Separator { get; }
    public byte? Quote { get; }
    public SeparatedValuesEscapeMode EscapeMode { get; }
    public SeparatedValuesWhitespaceMode WhitespaceMode { get; }
    public SeparatedValuesBlankRecordMode BlankRecordMode { get; }
    public ImmutableArray<byte> CommentPrefix { get; }
    public ImmutableArray<ImmutableArray<byte>> NullTokens { get; }
    public string CultureName { get; }
    public SeparatedValuesRecordEndingMode RecordEndingMode { get; }
    public int MaximumRecordBytes { get; }
    public int MaximumBufferedBytes { get; }
    public string Fingerprint { get; }

    public bool IsStrict =>
        Quote == (byte)'"' &&
        EscapeMode == SeparatedValuesEscapeMode.Double &&
        WhitespaceMode == SeparatedValuesWhitespaceMode.Preserve &&
        BlankRecordMode == SeparatedValuesBlankRecordMode.Skip &&
        CommentPrefix.IsEmpty &&
        NullTokens.IsEmpty &&
        string.Equals(CultureName, "invariant", StringComparison.OrdinalIgnoreCase) &&
        RecordEndingMode == SeparatedValuesRecordEndingMode.LfCrLf &&
        MaximumRecordBytes == DefaultMaximumRecordBytes &&
        MaximumBufferedBytes == DefaultMaximumBufferedBytes;

    /// <summary>
    /// Indicates that the block pipeline can resolve record boundaries without
    /// features that require line-level coordination (comments, blank-record
    /// emission, or a non-default record/buffer safety limit).
    /// </summary>
    /// <remarks>
    /// Field-level options such as trimming, null tokens, and a custom quote do
    /// not change where an LF/CRLF record boundary is; they are therefore safe
    /// to process after the boundary pass. Backslash escaping is kept on the
    /// sequential path because an escape can straddle two blocks. The strict
    /// framing kernel has a narrower contract and is selected separately.
    /// </remarks>
    public bool IsParallelFramingCompatible =>
        RecordEndingMode == SeparatedValuesRecordEndingMode.LfCrLf &&
        EscapeMode != SeparatedValuesEscapeMode.Backslash &&
        BlankRecordMode == SeparatedValuesBlankRecordMode.Skip &&
        CommentPrefix.IsEmpty &&
        MaximumRecordBytes == DefaultMaximumRecordBytes &&
        MaximumBufferedBytes == DefaultMaximumBufferedBytes;

    public static SeparatedValuesDialect Strict(byte separator)
    {
        return new SeparatedValuesDialect(
            separator,
            (byte)'"',
            SeparatedValuesEscapeMode.Double,
            SeparatedValuesWhitespaceMode.Preserve,
            SeparatedValuesBlankRecordMode.Skip,
            [],
            [],
            "invariant",
            SeparatedValuesRecordEndingMode.LfCrLf,
            DefaultMaximumRecordBytes,
            DefaultMaximumBufferedBytes);
    }

    public static SeparatedValuesDialect Create(
        byte separator,
        byte? quote = (byte)'"',
        SeparatedValuesEscapeMode escapeMode = SeparatedValuesEscapeMode.Double,
        SeparatedValuesWhitespaceMode whitespaceMode = SeparatedValuesWhitespaceMode.Preserve,
        SeparatedValuesBlankRecordMode blankRecordMode = SeparatedValuesBlankRecordMode.Skip,
        IEnumerable<byte>? commentPrefix = null,
        IEnumerable<IEnumerable<byte>>? nullTokens = null,
        string? cultureName = null,
        SeparatedValuesRecordEndingMode recordEndingMode = SeparatedValuesRecordEndingMode.LfCrLf,
        int maximumRecordBytes = DefaultMaximumRecordBytes,
        int maximumBufferedBytes = DefaultMaximumBufferedBytes)
    {
        var prefix = commentPrefix?.ToImmutableArray() ?? [];
        var tokens = nullTokens is null
            ? ImmutableArray<ImmutableArray<byte>>.Empty
            : nullTokens.Select(token => token.ToImmutableArray()).ToImmutableArray();
        if (prefix.Length > 32)
            throw new ArgumentException("A comment prefix may contain at most 32 UTF-8 bytes.", nameof(commentPrefix));
        if (tokens.Length > 32)
            throw new ArgumentException("Null tokens may contain at most 32 values.", nameof(nullTokens));
        foreach (var token in tokens)
        {
            if (token.Length == 0 || token.Length > 256)
                throw new ArgumentException("Null tokens must contain between 1 and 256 UTF-8 bytes.", nameof(nullTokens));
        }

        return new SeparatedValuesDialect(
            separator,
            quote,
            escapeMode,
            whitespaceMode,
            blankRecordMode,
            prefix,
            tokens,
            cultureName ?? "invariant",
            recordEndingMode,
            maximumRecordBytes,
            maximumBufferedBytes);
    }

    public static SeparatedValuesDialect FromRuntimeSettings(
        byte separator,
        IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var quote = ParseQuote(settings);
        var escape = ParseEnum(
            settings,
            EscapeSettingName,
            "double",
            ("double", SeparatedValuesEscapeMode.Double),
            ("backslash", SeparatedValuesEscapeMode.Backslash),
            ("none", SeparatedValuesEscapeMode.None));
        var whitespace = ParseEnum(
            settings,
            WhitespaceSettingName,
            "preserve",
            ("preserve", SeparatedValuesWhitespaceMode.Preserve),
            ("trim", SeparatedValuesWhitespaceMode.Trim));
        var blank = ParseEnum(
            settings,
            BlankRecordSettingName,
            "skip",
            ("skip", SeparatedValuesBlankRecordMode.Skip),
            ("emit", SeparatedValuesBlankRecordMode.Emit));
        var endings = ParseEnum(
            settings,
            RecordEndingsSettingName,
            "lf_crlf",
            ("lf_crlf", SeparatedValuesRecordEndingMode.LfCrLf),
            ("any", SeparatedValuesRecordEndingMode.Any));
        var culture = settings.TryGetValue(CultureSettingName, out var cultureText) &&
                      !string.IsNullOrWhiteSpace(cultureText)
            ? cultureText.Trim()
            : "invariant";
        if (string.Equals(culture, "invariant", StringComparison.OrdinalIgnoreCase))
            culture = "invariant";
        else
            _ = CultureInfo.GetCultureInfo(culture);

        var maximumRecordBytes = ParseLimit(
            settings,
            MaximumRecordBytesSettingName,
            DefaultMaximumRecordBytes,
            1024 * 1024 * 1024);
        var maximumBufferedBytes = ParseLimit(
            settings,
            MaximumBufferedBytesSettingName,
            DefaultMaximumBufferedBytes,
            SeparatedValuesStructuralMemoryBudget.CapacityBytes);
        return Create(
            separator,
            quote,
            escape,
            whitespace,
            blank,
            ParseUtf8Bytes(settings, CommentPrefixSettingName, 32),
            ParseNullTokens(settings),
            culture,
            endings,
            maximumRecordBytes,
            maximumBufferedBytes);
    }

    public bool IsNullToken(ReadOnlySpan<byte> value, bool quoted)
    {
        if (quoted || NullTokens.IsEmpty)
            return false;

        foreach (var token in NullTokens)
        {
            if (value.SequenceEqual(token.AsSpan()))
                return true;
        }

        return false;
    }

    private static void ValidateSeparator(byte separator, byte? quote)
    {
        if (separator > 0x7f ||
            separator is 13 or 10 ||
            (quote.HasValue && (quote.Value > 0x7f || quote.Value is 13 or 10)) ||
            quote == separator)
            throw new ArgumentException("The separated-values delimiter is not supported.", nameof(separator));
    }

    private static byte? ParseQuote(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(QuoteSettingName, out var text) || string.IsNullOrWhiteSpace(text))
            return (byte)'"';
        text = text.Trim();
        if (string.Equals(text, "none", StringComparison.OrdinalIgnoreCase))
            return null;
        if (text.Length != 1 || text[0] > 0x7f)
            throw new ArgumentException($"Runtime setting '{QuoteSettingName}' must be 'none' or one ASCII character.");
        return (byte)text[0];
    }

    private static T ParseEnum<T>(
        IReadOnlyDictionary<string, string> settings,
        string name,
        string defaultValue,
        params (string Text, T Value)[] values)
    {
        var text = settings.TryGetValue(name, out var configured) && !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : defaultValue;
        foreach (var value in values)
        {
            if (string.Equals(text, value.Text, StringComparison.OrdinalIgnoreCase))
                return value.Value;
        }

        throw new ArgumentException($"Runtime setting '{name}' has unsupported value '{text}'.");
    }

    private static int ParseLimit(
        IReadOnlyDictionary<string, string> settings,
        string name,
        int defaultValue,
        int maximum)
    {
        if (!settings.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text))
            return defaultValue;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value <= 0 ||
            value > maximum)
            throw new ArgumentException(
                $"Runtime setting '{name}' must be an integer between 1 and {maximum:N0}.");
        return value;
    }

    private static byte[] ParseUtf8Bytes(
        IReadOnlyDictionary<string, string> settings,
        string name,
        int maximum)
    {
        if (!settings.TryGetValue(name, out var text) || string.IsNullOrEmpty(text))
            return [];
        var bytes = new UTF8Encoding(false, true).GetBytes(text);
        if (bytes.Length > maximum)
            throw new ArgumentException($"Runtime setting '{name}' may contain at most {maximum} UTF-8 bytes.");
        return bytes;
    }

    private static byte[][] ParseNullTokens(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(NullTokensSettingName, out var text) || string.IsNullOrWhiteSpace(text))
            return [];
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new FormatException();
            var values = document.RootElement.EnumerateArray().ToArray();
            if (values.Length > 32)
                throw new ArgumentException($"Runtime setting '{NullTokensSettingName}' may contain at most 32 tokens.");
            var result = new byte[values.Length][];
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index].ValueKind != JsonValueKind.String)
                    throw new FormatException();
                result[index] = new UTF8Encoding(false, true).GetBytes(values[index].GetString()!);
                if (result[index].Length == 0 || result[index].Length > 256)
                    throw new ArgumentException(
                        $"Runtime setting '{NullTokensSettingName}' tokens must contain 1..256 UTF-8 bytes.");
            }

            return result;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                $"Runtime setting '{NullTokensSettingName}' must be a JSON array of strings.",
                exception);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"Runtime setting '{NullTokensSettingName}' must be a JSON array of strings.",
                exception);
        }
    }

    private string CreateFingerprint()
    {
        var prefix = CommentPrefix.IsEmpty ? string.Empty : Convert.ToHexString(CommentPrefix.AsSpan());
        var tokens = NullTokens.IsEmpty
            ? string.Empty
            : string.Join(',', NullTokens.Select(token => Convert.ToHexString(token.AsSpan())));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"separator={Separator};quote={(Quote.HasValue ? Quote.Value.ToString(CultureInfo.InvariantCulture) : "none")};" +
            $"escape={EscapeMode};whitespace={WhitespaceMode};blank={BlankRecordMode};comment={prefix};" +
            $"null={tokens};culture={CultureName};endings={RecordEndingMode};record={MaximumRecordBytes};buffer={MaximumBufferedBytes}");
    }
}

internal enum SeparatedValuesEscapeMode : byte
{
    Double,
    Backslash,
    None
}

internal enum SeparatedValuesWhitespaceMode : byte
{
    Preserve,
    Trim
}

internal enum SeparatedValuesBlankRecordMode : byte
{
    Skip,
    Emit
}

internal enum SeparatedValuesRecordEndingMode : byte
{
    LfCrLf,
    Any
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesReadModifiers
{
    public const string SourceCodec = $"{ColumnReadModifiers.SourcePrefix}codec";

    private static readonly Encoding DefaultFileEncoding = Encoding.UTF8;
    private static readonly Encoding StrictUtf8Encoding = new UTF8Encoding(false, true);

    static SeparatedValuesReadModifiers()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding ResolveFileEncodingOrDefault(IEnumerable<ISchemaColumn> columns)
    {
        return TryResolveFileEncoding(columns, out var encoding, out _)
            ? encoding
            : DefaultFileEncoding;
    }

    public static Encoding ResolveFileEncodingOrThrow(IEnumerable<ISchemaColumn> columns)
    {
        var columnList = columns.ToArray();
        var error = Describe(columnList)
            .FirstOrDefault(static diagnostic => diagnostic.Severity == SourceContractDiagnosticSeverity.Error);

        if (error != null)
            throw new InvalidOperationException(error.Message);

        if (TryResolveFileEncoding(columnList, out var encoding, out var diagnostic))
            return encoding;

        throw new InvalidOperationException(diagnostic?.Message ?? "Separated values read modifiers are invalid.");
    }

    public static Encoding ResolveColumnEncodingOrThrow(ISchemaColumn column)
    {
        var encodingName = column.ReadModifiers.GetValueOrDefault(ColumnReadModifiers.Encoding);

        if (TryResolveEncoding(encodingName, out var encoding, out _))
            return encoding;

        throw new InvalidOperationException($"Encoding '{encodingName}' is not supported by #separatedvalues.");
    }

    public static IReadOnlyList<SourceContractDiagnostic> Describe(IEnumerable<ISchemaColumn> columns)
    {
        var diagnostics = new List<SourceContractDiagnostic>();
        var columnList = columns.ToArray();

        AddUnsupportedModifierDiagnostics(columnList, diagnostics);
        AddEncodingDiagnostics(columnList, diagnostics);
        AddCultureDiagnostics(columnList, diagnostics);
        AddSourceCodecDiagnostics(columnList, diagnostics);
        AddFormatDiagnostics(columnList, diagnostics);

        return diagnostics;
    }

    public static IReadOnlyList<SourceContractDiagnostic> Plan(IEnumerable<SourceColumnRef> columns)
    {
        var diagnostics = new List<SourceContractDiagnostic>();
        var columnList = columns.ToArray();

        AddUnsupportedModifierDiagnostics(columnList, diagnostics);
        AddEncodingDiagnostics(columnList, diagnostics);
        AddCultureDiagnostics(columnList, diagnostics);
        AddSourceCodecDiagnostics(columnList, diagnostics);

        return diagnostics;
    }

    private static bool HasSourceCodec(IReadOnlyDictionary<string, string> modifiers)
    {
        return modifiers.ContainsKey(SourceCodec);
    }

    private static bool IsSupportedSourceCodec(string codec)
    {
        return string.Equals(codec, "base64", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(codec, "hex", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryDecodeSourceCodec(
        string text,
        string codec,
        out byte[] bytes)
    {
        try
        {
            if (string.Equals(codec, "base64", StringComparison.OrdinalIgnoreCase))
            {
                bytes = Convert.FromBase64String(text);
                return true;
            }

            if (string.Equals(codec, "hex", StringComparison.OrdinalIgnoreCase))
            {
                bytes = Convert.FromHexString(text);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        bytes = [];
        return false;
    }

    public static CultureInfo ResolveCulture(IReadOnlyDictionary<string, string> modifiers)
    {
        return modifiers.TryGetValue(ColumnReadModifiers.Culture, out var culture)
            ? CultureInfo.GetCultureInfo(culture)
            : CultureInfo.CurrentCulture;
    }

    private static bool IsSupportedModifier(string modifier)
    {
        return modifier is
            ColumnReadModifiers.Encoding or
            ColumnReadModifiers.Culture or
            ColumnReadModifiers.Format or
            ColumnReadModifiers.Trim or
            SourceCodec;
    }

    private static bool TryResolveFileEncoding(
        IEnumerable<ISchemaColumn> columns,
        out Encoding encoding,
        out SourceContractDiagnostic? diagnostic)
    {
        var normalColumns = columns
            .Where(static column => !HasSourceCodec(column.ReadModifiers))
            .ToArray();
        string? requestedEncoding = null;
        Encoding? resolvedEncoding = null;

        foreach (var column in normalColumns)
        {
            if (!column.ReadModifiers.TryGetValue(ColumnReadModifiers.Encoding, out var encodingName))
                continue;

            if (!TryResolveEncoding(encodingName, out var currentEncoding, out var exception))
            {
                encoding = DefaultFileEncoding;
                diagnostic = UnsupportedEncoding(column.ColumnName, encodingName, exception);
                return false;
            }

            if (resolvedEncoding == null)
            {
                requestedEncoding = encodingName;
                resolvedEncoding = currentEncoding;
                continue;
            }

            if (string.Equals(resolvedEncoding.WebName, currentEncoding.WebName, StringComparison.OrdinalIgnoreCase))
                continue;

            encoding = DefaultFileEncoding;
            diagnostic = SourceContractDiagnostic.Error(
                $"Separated values file encoding is file-wide, but columns requested both '{requestedEncoding}' and '{encodingName}'.",
                "SeparatedValuesInconsistentEncoding") with
            {
                ColumnName = column.ColumnName,
                ModifierKey = ColumnReadModifiers.Encoding
            };
            return false;
        }

        encoding = resolvedEncoding ?? DefaultFileEncoding;
        diagnostic = null;
        return true;
    }

    private static void AddUnsupportedModifierDiagnostics(
        IEnumerable<ISchemaColumn> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var column in columns)
            AddUnsupportedModifierDiagnostics(column.ColumnName, column.ReadModifiers, diagnostics);
    }

    private static void AddUnsupportedModifierDiagnostics(
        IEnumerable<SourceColumnRef> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var column in columns)
            AddUnsupportedModifierDiagnostics(column.Name, column.ReadModifiers, diagnostics);
    }

    private static void AddUnsupportedModifierDiagnostics(
        string columnName,
        IReadOnlyDictionary<string, string> modifiers,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var modifier in modifiers)
        {
            if (IsSupportedModifier(modifier.Key))
                continue;

            diagnostics.Add(SourceContractDiagnostic.Warning(
                $"Separated values source does not support modifier '{modifier.Key}' on column '{columnName}'.",
                "SeparatedValuesUnsupportedModifier") with
            {
                ColumnName = columnName,
                ModifierKey = modifier.Key
            });
        }
    }

    private static void AddEncodingDiagnostics(
        IEnumerable<ISchemaColumn> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        var columnList = columns.ToArray();

        foreach (var column in columnList)
            AddUnsupportedEncodingDiagnostic(column.ColumnName, column.ReadModifiers, diagnostics);

        if (!TryResolveFileEncoding(columnList, out _, out var diagnostic) && diagnostic != null)
            AddIfMissing(diagnostics, diagnostic);
    }

    private static void AddEncodingDiagnostics(
        IEnumerable<SourceColumnRef> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        var columnList = columns.ToArray();

        foreach (var column in columnList)
            AddUnsupportedEncodingDiagnostic(column.Name, column.ReadModifiers, diagnostics);

        AddPlanFileEncodingConflictDiagnostic(columnList, diagnostics);
    }

    private static void AddUnsupportedEncodingDiagnostic(
        string columnName,
        IReadOnlyDictionary<string, string> modifiers,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        if (!modifiers.TryGetValue(ColumnReadModifiers.Encoding, out var encoding))
            return;

        if (TryResolveEncoding(encoding, out _, out _))
            return;

        diagnostics.Add(UnsupportedEncoding(columnName, encoding, null));
    }

    private static void AddPlanFileEncodingConflictDiagnostic(
        IEnumerable<SourceColumnRef> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        string? requestedEncoding = null;
        Encoding? resolvedEncoding = null;

        foreach (var column in columns.Where(static column => !HasSourceCodec(column.ReadModifiers)))
        {
            if (!column.ReadModifiers.TryGetValue(ColumnReadModifiers.Encoding, out var encodingName) ||
                !TryResolveEncoding(encodingName, out var currentEncoding, out _))
                continue;

            if (resolvedEncoding == null)
            {
                requestedEncoding = encodingName;
                resolvedEncoding = currentEncoding;
                continue;
            }

            if (string.Equals(resolvedEncoding.WebName, currentEncoding.WebName, StringComparison.OrdinalIgnoreCase))
                continue;

            diagnostics.Add(SourceContractDiagnostic.Error(
                $"Separated values file encoding is file-wide, but columns requested both '{requestedEncoding}' and '{encodingName}'.",
                "SeparatedValuesInconsistentEncoding") with
            {
                ColumnName = column.Name,
                ModifierKey = ColumnReadModifiers.Encoding
            });
            return;
        }
    }

    private static void AddSourceCodecDiagnostics(
        IEnumerable<ISchemaColumn> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var column in columns)
            AddSourceCodecDiagnostic(column.ColumnName, column.ReadModifiers, diagnostics);
    }

    private static void AddCultureDiagnostics(
        IEnumerable<ISchemaColumn> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var column in columns)
            AddCultureDiagnostic(column.ColumnName, column.ReadModifiers, diagnostics);
    }

    private static void AddCultureDiagnostics(
        IEnumerable<SourceColumnRef> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var column in columns)
            AddCultureDiagnostic(column.Name, column.ReadModifiers, diagnostics);
    }

    private static void AddCultureDiagnostic(
        string columnName,
        IReadOnlyDictionary<string, string> modifiers,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        if (!modifiers.TryGetValue(ColumnReadModifiers.Culture, out var culture))
            return;

        try
        {
            _ = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException exception)
        {
            diagnostics.Add(SourceContractDiagnostic.Error(
                $"Culture '{culture}' is not supported by #separatedvalues: {exception.Message}",
                "SeparatedValuesUnsupportedCulture") with
            {
                ColumnName = columnName,
                ModifierKey = ColumnReadModifiers.Culture
            });
        }
    }

    private static void AddSourceCodecDiagnostics(
        IEnumerable<SourceColumnRef> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var column in columns)
            AddSourceCodecDiagnostic(column.Name, column.ReadModifiers, diagnostics);
    }

    private static void AddSourceCodecDiagnostic(
        string columnName,
        IReadOnlyDictionary<string, string> modifiers,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        if (!modifiers.TryGetValue(SourceCodec, out var codec) || IsSupportedSourceCodec(codec))
            return;

        diagnostics.Add(SourceContractDiagnostic.Error(
            $"Separated values source codec '{codec}' is not supported on column '{columnName}'.",
            "SeparatedValuesUnsupportedSourceCodec") with
        {
            ColumnName = columnName,
            ModifierKey = SourceCodec
        });
    }

    private static void AddFormatDiagnostics(
        IEnumerable<ISchemaColumn> columns,
        ICollection<SourceContractDiagnostic> diagnostics)
    {
        foreach (var column in columns)
        {
            if (!column.ReadModifiers.ContainsKey(ColumnReadModifiers.Format))
                continue;

            var targetType = Nullable.GetUnderlyingType(column.ColumnType) ?? column.ColumnType;
            if (targetType == typeof(DateTime) ||
                targetType == typeof(DateTimeOffset) ||
                targetType == typeof(TimeSpan))
                continue;

            diagnostics.Add(SourceContractDiagnostic.Error(
                $"Separated values source supports format modifier only for date/time columns, but column '{column.ColumnName}' is '{targetType.Name}'.",
                "SeparatedValuesUnsupportedFormat") with
            {
                ColumnName = column.ColumnName,
                ModifierKey = ColumnReadModifiers.Format
            });
        }
    }

    private static bool TryResolveEncoding(
        string? encodingName,
        out Encoding encoding,
        out Exception? exception)
    {
        try
        {
            encoding = ResolveEncoding(encodingName);
            exception = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            encoding = DefaultFileEncoding;
            exception = ex;
            return false;
        }
        catch (NotSupportedException ex)
        {
            encoding = DefaultFileEncoding;
            exception = ex;
            return false;
        }
    }

    private static Encoding ResolveEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName) ||
            string.Equals(encodingName, "utf-8", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(encodingName, "utf8", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(encodingName)
                ? DefaultFileEncoding
                : StrictUtf8Encoding;

        if (string.Equals(encodingName, "utf-16le", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(encodingName, "utf16le", StringComparison.OrdinalIgnoreCase))
            return new UnicodeEncoding(false, false, true);

        if (string.Equals(encodingName, "utf-16be", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(encodingName, "utf16be", StringComparison.OrdinalIgnoreCase))
            return new UnicodeEncoding(true, false, true);

        if (string.Equals(encodingName, "latin1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(encodingName, "iso-8859-1", StringComparison.OrdinalIgnoreCase))
            return Encoding.Latin1;

        return Encoding.GetEncoding(
            encodingName,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private static SourceContractDiagnostic UnsupportedEncoding(
        string columnName,
        string encoding,
        Exception? exception)
    {
        var message = exception == null
            ? $"Encoding '{encoding}' is not supported by #separatedvalues."
            : $"Encoding '{encoding}' is not supported by #separatedvalues: {exception.Message}";

        return SourceContractDiagnostic.Error(
            message,
            "SeparatedValuesUnsupportedEncoding") with
        {
            ColumnName = columnName,
            ModifierKey = ColumnReadModifiers.Encoding
        };
    }

    private static void AddIfMissing(
        ICollection<SourceContractDiagnostic> diagnostics,
        SourceContractDiagnostic diagnostic)
    {
        if (diagnostics.Any(existing =>
                existing.Code == diagnostic.Code &&
                existing.ColumnName == diagnostic.ColumnName &&
                existing.ModifierKey == diagnostic.ModifierKey))
            return;

        diagnostics.Add(diagnostic);
    }
}

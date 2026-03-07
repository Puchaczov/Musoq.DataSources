using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a compiler diagnostic (error, warning, info).
/// </summary>
public class DiagnosticEntity
{
    private readonly Diagnostic _diagnostic;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagnosticEntity" /> class.
    /// </summary>
    /// <param name="diagnostic">The Roslyn diagnostic.</param>
    public DiagnosticEntity(Diagnostic diagnostic)
    {
        _diagnostic = diagnostic;
    }

    /// <summary>
    ///     Gets the diagnostic ID (e.g., CS0001, CS8602).
    /// </summary>
    public string Id => _diagnostic.Id;

    /// <summary>
    ///     Gets the diagnostic message.
    /// </summary>
    public string Message => _diagnostic.GetMessage();

    /// <summary>
    ///     Gets the severity of the diagnostic (Error, Warning, Info, Hidden).
    /// </summary>
    public string Severity => _diagnostic.Severity.ToString();

    /// <summary>
    ///     Gets a value indicating whether this diagnostic is an error.
    /// </summary>
    public bool IsError => _diagnostic.Severity == DiagnosticSeverity.Error;

    /// <summary>
    ///     Gets a value indicating whether this diagnostic is a warning.
    /// </summary>
    public bool IsWarning => _diagnostic.Severity == DiagnosticSeverity.Warning;

    /// <summary>
    ///     Gets the category of the diagnostic.
    /// </summary>
    public string Category => _diagnostic.Descriptor.Category;

    /// <summary>
    ///     Gets the start line number of the diagnostic location (1-based).
    /// </summary>
    public int StartLine
    {
        get
        {
            if (_diagnostic.Location == Location.None) return 0;
            var lineSpan = _diagnostic.Location.GetLineSpan();
            return lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    ///     Gets the end line number of the diagnostic location (1-based).
    /// </summary>
    public int EndLine
    {
        get
        {
            if (_diagnostic.Location == Location.None) return 0;
            var lineSpan = _diagnostic.Location.GetLineSpan();
            return lineSpan.EndLinePosition.Line + 1;
        }
    }

    /// <summary>
    ///     Gets the file path of the diagnostic location.
    /// </summary>
    public string? FilePath => _diagnostic.Location.SourceTree?.FilePath;

    /// <summary>
    ///     Returns a string representation of the diagnostic.
    /// </summary>
    public override string ToString() => $"{Severity} {Id}: {Message}";
}

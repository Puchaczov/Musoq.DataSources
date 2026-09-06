#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Traversal;

internal sealed class SearchScopeCounters
{
    private long _filesConsidered;
    private long _metadataReads;
    private long _metadataRejected;
    private long _candidatesYielded;
    private long _contentOpenAttempts;
    private long _binaryFilesSkipped;

    public long FilesConsidered => Interlocked.Read(ref _filesConsidered);

    public long MetadataReads => Interlocked.Read(ref _metadataReads);

    public long MetadataRejected => Interlocked.Read(ref _metadataRejected);

    public long CandidatesYielded => Interlocked.Read(ref _candidatesYielded);

    public long ContentOpenAttempts => Interlocked.Read(ref _contentOpenAttempts);

    public long BinaryFilesSkipped => Interlocked.Read(ref _binaryFilesSkipped);

    internal void IncrementFilesConsidered() => Interlocked.Increment(ref _filesConsidered);

    internal void IncrementMetadataReads() => Interlocked.Increment(ref _metadataReads);

    internal void IncrementMetadataRejected() => Interlocked.Increment(ref _metadataRejected);

    internal void IncrementCandidatesYielded() => Interlocked.Increment(ref _candidatesYielded);

    internal void IncrementContentOpenAttempts() => Interlocked.Increment(ref _contentOpenAttempts);

    internal void IncrementBinaryFilesSkipped() => Interlocked.Increment(ref _binaryFilesSkipped);
}

internal static class SearchScopeTraversal
{
    internal static IEnumerable<string> Enumerate(
        string rootPath,
        ScopePolicy scope,
        CancellationToken cancellationToken,
        SearchScopeCounters? counters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(scope);

        var matcher = new SearchScopeMatcher(rootPath, scope, cancellationToken, counters);
        if (!matcher.PrepareRoot())
            yield break;

        var explicitFileRoot = File.Exists(rootPath);
        foreach (var file in SearchFileTraversal.Enumerate(
                     rootPath,
                     cancellationToken,
                     matcher.Decide))
        {
            if (explicitFileRoot)
            {
                counters?.IncrementFilesConsidered();
                counters?.IncrementCandidatesYielded();
            }

            yield return file;
        }
    }
}

internal sealed class SearchScopeMatcher
{
    private const string ConfiguredGlobalSource = "<configured global ignore>";
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly string _rootPath;
    private string _physicalRootPath;
    private readonly ScopePolicy _scope;
    private readonly CancellationToken _cancellationToken;
    private readonly GlobPattern[] _include;
    private readonly GlobPattern[] _exclude;
    private readonly GlobPattern[] _nameIncludes;
    private readonly GlobPattern[] _nameExcludes;
    private readonly SearchMetadataPolicy _metadata;
    private readonly SearchScopeCounters? _counters;
    private readonly List<SearchIgnoreRule> _ignoreRules = [];
    private readonly HashSet<string> _loadedDirectories = new(PathComparer);
    private readonly HashSet<string> _visitedPhysicalDirectories = new(PathComparer);
    private long _ruleOrder;

    public SearchScopeMatcher(
        string rootPath,
        ScopePolicy scope,
        CancellationToken cancellationToken,
        SearchScopeCounters? counters = null)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _physicalRootPath = _rootPath;
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _cancellationToken = cancellationToken;
        _metadata = scope.Metadata;
        _counters = counters;
        _include = Compile(scope.Include, "include");
        _exclude = Compile(scope.Exclude, "exclude");
        _nameIncludes = Compile(_metadata.NameIncludes, "nameIncludes");
        _nameExcludes = Compile(_metadata.NameExcludes, "nameExcludes");

        if (scope.GlobalIgnores == GlobalIgnorePolicy.Configured)
        {
            for (var index = 0; index < scope.GlobalIgnoreRules.Count; index++)
            {
                AddRule(
                    scope.GlobalIgnoreRules[index],
                    _rootPath,
                    ConfiguredGlobalSource,
                    precedence: 0,
                    lineNumber: index + 1);
            }
        }
    }

    public bool PrepareRoot()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // Leave missing-root reporting to SearchFileTraversal so it retains
        // the established SEARCH-SOURCE-001 diagnostic.
        if (!File.Exists(_rootPath) && !Directory.Exists(_rootPath))
            return true;

        var rootEntry = SearchFileTraversal.ClassifyPath(_rootPath);
        if (rootEntry.IsSpecial)
            return false;

        if (!rootEntry.IsReparsePoint)
        {
            _physicalRootPath = _rootPath;
            if (rootEntry.IsDirectory)
                _visitedPhysicalDirectories.Add(_physicalRootPath);

            return true;
        }

        if (_scope.FollowLinks == LinkTraversalPolicy.DoNotFollow)
            return false;

        var resolution = SearchFileTraversal.ResolveLink(rootEntry, _cancellationToken);
        if (resolution.Status != SearchLinkResolutionStatus.Resolved ||
            resolution.PhysicalPath is null)
        {
            return false;
        }

        _physicalRootPath = resolution.PhysicalPath;
        if (rootEntry.IsDirectory && resolution.IsDirectory)
            _visitedPhysicalDirectories.Add(_physicalRootPath);

        return rootEntry.IsDirectory == resolution.IsDirectory ||
            rootEntry.IsFile == resolution.IsFile;
    }

    public SearchTraversalAction Decide(SearchTraversalEntry entry)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetFullPath(entry.Path);
        var relativePath = GetRelativePath(fullPath);
        if (entry.IsSpecial)
            return SearchTraversalAction.Skip;

        if (relativePath.Length == 0)
        {
            if (entry.IsReparsePoint && _scope.FollowLinks == LinkTraversalPolicy.DoNotFollow)
            {
                return SearchTraversalAction.Skip;
            }

            return entry.IsDirectory
                ? SearchTraversalAction.Descend
                : SearchTraversalAction.Yield;
        }

        var parentDirectory = Path.GetDirectoryName(fullPath) ?? _rootPath;
        SearchLinkResolution resolution = default;
        if (entry.IsReparsePoint && !TryResolveLink(entry, out resolution))
            return SearchTraversalAction.Skip;

        if (entry.IsDirectory)
        {
            if (!_scope.Recursive)
                return SearchTraversalAction.Skip;

            if (MatchesDirectoryOnlyExclude(relativePath))
                return SearchTraversalAction.Skip;

            if (entry.IsReparsePoint)
            {
                if (!resolution.IsDirectory ||
                    resolution.PhysicalPath is null ||
                    !_visitedPhysicalDirectories.Add(resolution.PhysicalPath))
                {
                    return SearchTraversalAction.Skip;
                }
            }

            EnsureRules(parentDirectory);
            if (IsIgnored(fullPath, isDirectory: true))
                return SearchTraversalAction.Skip;

            return SearchTraversalAction.Descend;
        }

        if (!entry.IsFile)
            return SearchTraversalAction.Skip;

        _counters?.IncrementFilesConsidered();
        if (!MatchesInclude(relativePath) || MatchesExclude(relativePath))
            return SearchTraversalAction.Skip;

        EnsureRules(parentDirectory);
        if (IsIgnored(fullPath, isDirectory: false))
            return SearchTraversalAction.Skip;

        if (!MatchesMetadata(fullPath))
        {
            _counters?.IncrementMetadataRejected();
            return SearchTraversalAction.Skip;
        }

        _counters?.IncrementCandidatesYielded();
        return SearchTraversalAction.Yield;
    }

    private bool TryResolveLink(
        SearchTraversalEntry entry,
        out SearchLinkResolution resolution)
    {
        resolution = default;
        if (!entry.IsReparsePoint)
            return true;

        if (_scope.FollowLinks == LinkTraversalPolicy.DoNotFollow)
            return false;

        resolution = SearchFileTraversal.ResolveLink(entry, _cancellationToken);
        if (resolution.Status != SearchLinkResolutionStatus.Resolved ||
            resolution.PhysicalPath is null)
        {
            return false;
        }

        if (!SearchPathContainment.IsContained(_physicalRootPath, resolution.PhysicalPath))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(entry.Path),
                new InvalidOperationException(
                    "The resolved link target is outside the physical search root."));
        }

        if (entry.IsDirectory)
            return resolution.IsDirectory;

        return entry.IsFile && resolution.IsFile;
    }

    private GlobPattern[] Compile(IReadOnlyList<string> patterns, string parameterName)
    {
        var compiled = new GlobPattern[patterns.Count];
        try
        {
            for (var index = 0; index < patterns.Count; index++)
                compiled[index] = new GlobPattern(patterns[index]);
        }
        catch (ArgumentException exception)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(parameterName),
                exception);
        }

        return compiled;
    }

    private bool MatchesInclude(string relativePath)
    {
        if (_include.Length == 0)
            return true;

        foreach (var pattern in _include)
        {
            if (pattern.Matches(relativePath, isDirectory: false))
                return true;
        }

        return false;
    }

    private bool MatchesExclude(string relativePath)
    {
        foreach (var pattern in _exclude)
        {
            if (pattern.Matches(relativePath, isDirectory: false))
                return true;
        }

        return false;
    }

    private bool MatchesDirectoryOnlyExclude(string relativePath)
    {
        foreach (var pattern in _exclude)
        {
            if (pattern.DirectoryOnly && pattern.Matches(relativePath, isDirectory: true))
                return true;
        }

        return false;
    }

    private bool MatchesMetadata(string fullPath)
    {
        if (_nameIncludes.Length == 0 &&
            _nameExcludes.Length == 0 &&
            _metadata.ExtensionIncludes.Count == 0 &&
            _metadata.ExtensionExcludes.Count == 0 &&
            _metadata.MinimumSizeBytes is null &&
            _metadata.MaximumSizeBytes is null &&
            _metadata.ModifiedAfterOrEqualUtc is null &&
            _metadata.ModifiedBeforeOrEqualUtc is null)
        {
            return true;
        }

        var name = Path.GetFileName(fullPath);
        if (_nameIncludes.Length > 0 && !MatchesAny(_nameIncludes, name))
            return false;

        if (MatchesAny(_nameExcludes, name))
            return false;

        var extension = Path.GetExtension(name);
        if (_metadata.ExtensionIncludes.Count > 0 &&
            !ContainsExtension(_metadata.ExtensionIncludes, extension))
        {
            return false;
        }

        if (ContainsExtension(_metadata.ExtensionExcludes, extension))
            return false;

        if (_metadata.MinimumSizeBytes is null &&
            _metadata.MaximumSizeBytes is null &&
            _metadata.ModifiedAfterOrEqualUtc is null &&
            _metadata.ModifiedBeforeOrEqualUtc is null)
        {
            return true;
        }

        _counters?.IncrementMetadataReads();
        try
        {
            _ = File.GetAttributes(fullPath);
            long? length = null;
            if (_metadata.MinimumSizeBytes is not null || _metadata.MaximumSizeBytes is not null)
                length = new FileInfo(fullPath).Length;

            DateTimeOffset? modified = null;
            if (_metadata.ModifiedAfterOrEqualUtc is not null ||
                _metadata.ModifiedBeforeOrEqualUtc is not null)
            {
                var lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
                modified = new DateTimeOffset(
                    DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc));
            }

            if (length is not null &&
                ((_metadata.MinimumSizeBytes is not null && length < _metadata.MinimumSizeBytes) ||
                 (_metadata.MaximumSizeBytes is not null && length > _metadata.MaximumSizeBytes)))
            {
                return false;
            }

            return modified is null ||
                ((_metadata.ModifiedAfterOrEqualUtc is null ||
                  modified >= _metadata.ModifiedAfterOrEqualUtc) &&
                 (_metadata.ModifiedBeforeOrEqualUtc is null ||
                  modified <= _metadata.ModifiedBeforeOrEqualUtc));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(fullPath),
                exception);
        }
    }

    private static bool MatchesAny(IReadOnlyList<GlobPattern> patterns, string name)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.Matches(name, isDirectory: false))
                return true;
        }

        return false;
    }

    private static bool ContainsExtension(
        IReadOnlyList<string> extensions,
        string extension)
    {
        foreach (var candidate in extensions)
        {
            if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsIgnored(string fullPath, bool isDirectory)
    {
        SearchIgnoreRule? selected = null;
        foreach (var rule in _ignoreRules)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!rule.Matches(fullPath, isDirectory))
                continue;

            if (selected is null || SearchIgnoreRule.Compare(rule, selected) > 0)
                selected = rule;
        }

        return selected is not null && !selected.Negated;
    }

    private void EnsureRules(string directoryPath)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var normalizedDirectory = GetFullPath(directoryPath);
        if (!IsContained(normalizedDirectory))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(normalizedDirectory),
                new InvalidOperationException("The ignore source is outside the search root."));
        }

        if (!_loadedDirectories.Add(normalizedDirectory))
            return;

        try
        {
            var parent = Directory.GetParent(normalizedDirectory)?.FullName;
            if (parent is not null &&
                !PathComparer.Equals(parent, _rootPath) &&
                IsContained(parent))
                EnsureRules(parent);

            if (_scope.RepositoryIgnores == RepositoryIgnorePolicy.Respect)
            {
                LoadRules(normalizedDirectory, ".gitignore", precedence: 1);
                LoadRules(normalizedDirectory, ".ignore", precedence: 2);
                LoadRules(normalizedDirectory, ".rgignore", precedence: 3);
            }
        }
        catch
        {
            _loadedDirectories.Remove(normalizedDirectory);
            throw;
        }
    }

    private void LoadRules(string directoryPath, string fileName, int precedence)
    {
        var sourcePath = Path.Combine(directoryPath, fileName);
        try
        {
            using var reader = new StreamReader(sourcePath);
            var lineNumber = 0;
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var line = reader.ReadLine();
                if (line is null)
                    break;

                lineNumber++;
                AddRule(line, directoryPath, sourcePath, precedence, lineNumber);
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(sourcePath),
                exception);
        }
    }

    private void AddRule(
        string ruleText,
        string baseDirectory,
        string sourcePath,
        int precedence,
        int lineNumber)
    {
        if (!SearchIgnoreRule.TryParse(
                ruleText,
                baseDirectory,
                sourcePath,
                precedence,
                GetDepth(baseDirectory),
                lineNumber,
                _ruleOrder++,
                out var rule))
        {
            return;
        }

        _ignoreRules.Add(rule);
    }

    private int GetDepth(string path)
    {
        var relative = GetRelativePath(path);
        if (relative.Length == 0)
            return 0;

        var depth = 0;
        foreach (var part in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is not ".")
                depth++;
        }

        return depth;
    }

    private string GetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException exception)
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(path),
                exception);
        }
    }

    private string GetRelativePath(string path)
    {
        var relative = Path.GetRelativePath(_rootPath, path);
        relative = relative.Replace('\\', '/');
        if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
            relative = relative.Replace(Path.AltDirectorySeparatorChar, '/');

        if (relative is ".")
            return string.Empty;

        if (Path.IsPathRooted(relative) ||
            relative is ".." ||
            relative.StartsWith("../", StringComparison.Ordinal))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(path),
                new InvalidOperationException("The candidate path is outside the search root."));
        }

        return relative;
    }

    private bool IsContained(string path)
    {
        return SearchPathContainment.IsContained(_rootPath, path);
    }

    private static bool IsAccessFailure(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException;
    }
}

internal static class SearchPathContainment
{
    internal static bool IsContained(string rootPath, string candidatePath)
    {
        var root = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        var relative = Path.GetRelativePath(root, candidate).Replace('\\', '/');
        return !Path.IsPathRooted(relative) &&
            relative is not ".." &&
            !relative.StartsWith("../", StringComparison.Ordinal);
    }
}

internal sealed class GlobPattern
{
    private static readonly RegexOptions CommonRegexOptions =
        RegexOptions.CultureInvariant | RegexOptions.Compiled;
    private static readonly bool IgnoreCase = OperatingSystem.IsWindows();
    private readonly Regex _pathRegex;
    private readonly Regex _segmentRegex;
    private readonly bool _hasSeparator;
    private readonly bool _anchored;
    private readonly bool _matchAnySegment;

    public GlobPattern(string pattern, bool matchAnySegment = false)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0)
            throw new ArgumentException("A glob pattern cannot be empty.", nameof(pattern));

        var tokens = Parse(pattern, out _anchored, out var directoryOnly);
        DirectoryOnly = directoryOnly;
        _hasSeparator = ContainsSeparator(tokens);
        _matchAnySegment = matchAnySegment;
        _pathRegex = Compile(tokens, pathPattern: true);
        _segmentRegex = Compile(tokens, pathPattern: false);
    }

    public bool DirectoryOnly { get; }

    public bool Matches(string relativePath, bool isDirectory)
    {
        if (DirectoryOnly && !isDirectory)
            return false;

        if (relativePath.Length == 0)
            return false;

        if (_hasSeparator || _anchored)
            return _pathRegex.IsMatch(relativePath);

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!_matchAnySegment)
            return segments.Length > 0 && _segmentRegex.IsMatch(segments[^1]);

        foreach (var segment in segments)
        {
            if (_segmentRegex.IsMatch(segment))
                return true;
        }

        return false;
    }

    private static List<GlobToken> Parse(
        string pattern,
        out bool anchored,
        out bool directoryOnly)
    {
        var text = TrimTrailingUnescapedSpaces(pattern);
        directoryOnly = EndsWithUnescaped(text, '/');
        if (directoryOnly)
            text = text[..^1];

        anchored = StartsWithUnescaped(text, '/');
        if (anchored)
            text = text[1..];

        var tokens = new List<GlobToken>(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '\\' && index + 1 < text.Length)
            {
                tokens.Add(new GlobToken(text[++index], Escaped: true, Separator: false));
                continue;
            }

            tokens.Add(new GlobToken(
                current,
                Escaped: false,
                Separator: current == '/'));
        }

        return tokens;
    }

    private Regex Compile(IReadOnlyList<GlobToken> tokens, bool pathPattern)
    {
        var expression = new StringBuilder(@"\A");
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Separator)
            {
                expression.Append('/');
                continue;
            }

            if (!token.Escaped && token.Value == '*')
            {
                var isDouble = index + 1 < tokens.Count &&
                    !tokens[index + 1].Escaped &&
                    tokens[index + 1].Value == '*';
                if (isDouble)
                {
                    if (pathPattern && index + 2 < tokens.Count && tokens[index + 2].Separator)
                    {
                        expression.Append("(?:.*/)?");
                        index += 2;
                    }
                    else
                    {
                        expression.Append(pathPattern ? ".*" : "[^/]*");
                        index++;
                    }

                    continue;
                }

                expression.Append("[^/]*");
                continue;
            }

            if (!token.Escaped && token.Value == '?')
            {
                expression.Append("[^/]");
                continue;
            }

            expression.Append(Regex.Escape(token.Value.ToString()));
        }

        expression.Append(@"\z");
        var options = CommonRegexOptions;
        if (IgnoreCase)
            options |= RegexOptions.IgnoreCase;

        return new Regex(expression.ToString(), options);
    }

    private static bool ContainsSeparator(IReadOnlyList<GlobToken> tokens)
    {
        foreach (var token in tokens)
        {
            if (token.Separator)
                return true;
        }

        return false;
    }

    private static string TrimTrailingUnescapedSpaces(string text)
    {
        var end = text.Length;
        while (end > 0 && text[end - 1] == ' ' && !IsEscaped(text, end - 1))
            end--;

        return end == text.Length ? text : text[..end];
    }

    private static bool StartsWithUnescaped(string text, char value)
    {
        return text.Length > 0 && text[0] == value;
    }

    private static bool EndsWithUnescaped(string text, char value)
    {
        return text.Length > 0 && text[^1] == value && !IsEscaped(text, text.Length - 1);
    }

    private static bool IsEscaped(string text, int index)
    {
        var slashCount = 0;
        for (var position = index - 1; position >= 0 && text[position] == '\\'; position--)
            slashCount++;

        return slashCount % 2 == 1;
    }

    private readonly record struct GlobToken(char Value, bool Escaped, bool Separator);
}

internal sealed class SearchIgnoreRule
{
    private readonly GlobPattern _pattern;
    private readonly string _baseDirectory;

    private SearchIgnoreRule(
        GlobPattern pattern,
        string baseDirectory,
        string sourcePath,
        int precedence,
        int directoryDepth,
        int lineNumber,
        long order,
        bool negated)
    {
        _pattern = pattern;
        _baseDirectory = baseDirectory;
        SourcePath = sourcePath;
        Precedence = precedence;
        DirectoryDepth = directoryDepth;
        LineNumber = lineNumber;
        Order = order;
        Negated = negated;
    }

    public string SourcePath { get; }

    public int Precedence { get; }

    public int DirectoryDepth { get; }

    public int LineNumber { get; }

    public long Order { get; }

    public bool Negated { get; }

    public bool Matches(string fullPath, bool isDirectory)
    {
        var relative = Path.GetRelativePath(_baseDirectory, fullPath).Replace('\\', '/');
        if (Path.IsPathRooted(relative) ||
            relative is ".." ||
            relative.StartsWith("../", StringComparison.Ordinal))
        {
            return false;
        }

        return _pattern.Matches(relative, isDirectory);
    }

    public static int Compare(SearchIgnoreRule left, SearchIgnoreRule right)
    {
        var comparison = left.Precedence.CompareTo(right.Precedence);
        if (comparison != 0)
            return comparison;

        comparison = left.DirectoryDepth.CompareTo(right.DirectoryDepth);
        return comparison != 0
            ? comparison
            : left.Order.CompareTo(right.Order);
    }

    public static bool TryParse(
        string line,
        string baseDirectory,
        string sourcePath,
        int precedence,
        int directoryDepth,
        int lineNumber,
        long order,
        out SearchIgnoreRule rule)
    {
        rule = null!;
        var text = TrimTrailingUnescapedSpaces(line);
        if (text.Length == 0 || text[0] == '#')
            return false;

        var negated = text[0] == '!' && !IsEscaped(text, 0);
        if (negated)
            text = text[1..];
        if (text.Length == 0)
            return false;

        try
        {
            rule = new SearchIgnoreRule(
                new GlobPattern(text, matchAnySegment: true),
                baseDirectory,
                sourcePath,
                precedence,
                directoryDepth,
                lineNumber,
                order,
                negated);
            return true;
        }
        catch (ArgumentException exception)
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(sourcePath),
                exception);
        }
    }

    private static string TrimTrailingUnescapedSpaces(string text)
    {
        var end = text.Length;
        while (end > 0 && text[end - 1] == ' ' && !IsEscaped(text, end - 1))
            end--;

        return end == text.Length ? text : text[..end];
    }

    private static bool IsEscaped(string text, int index)
    {
        var slashCount = 0;
        for (var position = index - 1; position >= 0 && text[position] == '\\'; position--)
            slashCount++;

        return slashCount % 2 == 1;
    }
}

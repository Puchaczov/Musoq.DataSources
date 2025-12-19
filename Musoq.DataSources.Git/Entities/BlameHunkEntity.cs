using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a contiguous group of lines sharing the same attribution (blame hunk).
/// </summary>
public class BlameHunkEntity
{
    private readonly BlameHunk _hunk;
    private readonly Repository _repository;
    private readonly string _filePath;
    private IEnumerable<BlameLineEntity>? _lines;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlameHunkEntity"/> class.
    /// </summary>
    /// <param name="hunk">The LibGit2Sharp blame hunk.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="filePath">The file path.</param>
    public BlameHunkEntity(BlameHunk hunk, Repository repository, string filePath)
    {
        _hunk = hunk;
        _repository = repository;
        _filePath = filePath;
    }

    /// <summary>
    /// A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    /// A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<BlameHunkEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    /// An array of schema columns representing the structure of the blame hunk entity.
    /// </summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(StartLineNumber), 0, typeof(int)),
        new SchemaColumn(nameof(EndLineNumber), 1, typeof(int)),
        new SchemaColumn(nameof(LineCount), 2, typeof(int)),
        new SchemaColumn(nameof(CommitSha), 3, typeof(string)),
        new SchemaColumn(nameof(Author), 4, typeof(string)),
        new SchemaColumn(nameof(AuthorEmail), 5, typeof(string)),
        new SchemaColumn(nameof(AuthorDate), 6, typeof(DateTimeOffset)),
        new SchemaColumn(nameof(Committer), 7, typeof(string)),
        new SchemaColumn(nameof(CommitterEmail), 8, typeof(string)),
        new SchemaColumn(nameof(CommitterDate), 9, typeof(DateTimeOffset)),
        new SchemaColumn(nameof(Summary), 10, typeof(string)),
        new SchemaColumn(nameof(OriginalStartLineNumber), 11, typeof(int)),
        new SchemaColumn(nameof(OriginalFilePath), 12, typeof(string)),
        new SchemaColumn(nameof(Lines), 13, typeof(IEnumerable<BlameLineEntity>)),
        new SchemaColumn(nameof(Self), 14, typeof(BlameHunkEntity))
    ];

    /// <summary>
    /// Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static BlameHunkEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(StartLineNumber), 0},
            {nameof(EndLineNumber), 1},
            {nameof(LineCount), 2},
            {nameof(CommitSha), 3},
            {nameof(Author), 4},
            {nameof(AuthorEmail), 5},
            {nameof(AuthorDate), 6},
            {nameof(Committer), 7},
            {nameof(CommitterEmail), 8},
            {nameof(CommitterDate), 9},
            {nameof(Summary), 10},
            {nameof(OriginalStartLineNumber), 11},
            {nameof(OriginalFilePath), 12},
            {nameof(Lines), 13},
            {nameof(Self), 14}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<BlameHunkEntity, object?>>
        {
            {0, entity => entity.StartLineNumber},
            {1, entity => entity.EndLineNumber},
            {2, entity => entity.LineCount},
            {3, entity => entity.CommitSha},
            {4, entity => entity.Author},
            {5, entity => entity.AuthorEmail},
            {6, entity => entity.AuthorDate},
            {7, entity => entity.Committer},
            {8, entity => entity.CommitterEmail},
            {9, entity => entity.CommitterDate},
            {10, entity => entity.Summary},
            {11, entity => entity.OriginalStartLineNumber},
            {12, entity => entity.OriginalFilePath},
            {13, entity => entity.Lines},
            {14, entity => entity.Self}
        };
    }

    /// <summary>
    /// Gets the first line of the hunk (1-based).
    /// </summary>
    public int StartLineNumber => _hunk.FinalStartLineNumber + 1;

    /// <summary>
    /// Gets the last line of the hunk (1-based).
    /// </summary>
    public int EndLineNumber => _hunk.FinalStartLineNumber + _hunk.LineCount;

    /// <summary>
    /// Gets the number of lines in the hunk.
    /// </summary>
    public int LineCount => _hunk.LineCount;

    /// <summary>
    /// Gets the SHA of the commit that last modified these lines.
    /// </summary>
    public string CommitSha => _hunk.FinalCommit.Sha;

    /// <summary>
    /// Gets the author name.
    /// </summary>
    public string Author => _hunk.FinalCommit.Author.Name;

    /// <summary>
    /// Gets the author email.
    /// </summary>
    public string AuthorEmail => _hunk.FinalCommit.Author.Email;

    /// <summary>
    /// Gets when the author made the change.
    /// </summary>
    public DateTimeOffset AuthorDate => _hunk.FinalCommit.Author.When;

    /// <summary>
    /// Gets the committer name.
    /// </summary>
    public string Committer => _hunk.FinalCommit.Committer.Name;

    /// <summary>
    /// Gets the committer email.
    /// </summary>
    public string CommitterEmail => _hunk.FinalCommit.Committer.Email;

    /// <summary>
    /// Gets when the commit was applied.
    /// </summary>
    public DateTimeOffset CommitterDate => _hunk.FinalCommit.Committer.When;

    /// <summary>
    /// Gets the first line of the commit message.
    /// </summary>
    public string Summary => _hunk.FinalCommit.MessageShort;

    /// <summary>
    /// Gets the original line number if moved/copied.
    /// </summary>
    public int? OriginalStartLineNumber
    {
        get
        {
            // LibGit2Sharp uses 0-based indexing internally
            // If the original start line is different from the final start line, return 1-based value
            if (_hunk.InitialStartLineNumber != _hunk.FinalStartLineNumber)
                return _hunk.InitialStartLineNumber + 1;
            
            return null;
        }
    }

    /// <summary>
    /// Gets the original file path if moved/copied (null if same file).
    /// </summary>
    public string? OriginalFilePath
    {
        get
        {
            // If the initial path is different from the current file path, it was moved/copied
            if (_hunk.InitialPath != _filePath)
                return _hunk.InitialPath;
            
            return null;
        }
    }

    /// <summary>
    /// Gets line details with content (lazy loaded).
    /// </summary>
    public IEnumerable<BlameLineEntity> Lines
    {
        get
        {
            if (_lines != null)
                return _lines;

            // Lazy load the content from the blob
            var lines = new List<BlameLineEntity>();
            
            try
            {
                // Get the blob at the final commit
                var commit = _hunk.FinalCommit;
                var treeEntry = commit[_filePath];
                
                if (treeEntry?.TargetType == TreeEntryTargetType.Blob)
                {
                    var blob = (Blob)treeEntry.Target;
                    
                    // Read content as text
                    using var reader = new System.IO.StreamReader(blob.GetContentStream());
                    var content = reader.ReadToEnd();
                    var allLines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    
                    // Extract only the lines for this hunk (convert from 0-based to 1-based)
                    var startIndex = _hunk.FinalStartLineNumber;
                    var endIndex = Math.Min(startIndex + _hunk.LineCount, allLines.Length);
                    
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        lines.Add(new BlameLineEntity(i + 1, allLines[i]));
                    }
                }
            }
            catch
            {
                // If we can't load the content, return empty
                // This can happen for binary files or missing blobs
            }
            
            _lines = lines;
            return _lines;
        }
    }

    /// <summary>
    /// Gets this instance.
    /// </summary>
    public BlameHunkEntity Self => this;
}

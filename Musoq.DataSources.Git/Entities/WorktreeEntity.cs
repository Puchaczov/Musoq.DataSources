using System.Collections.Generic;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>A detached registration returned by Git, including bare and unavailable worktrees.</summary>
public sealed class WorktreeEntity
{
    /// <summary>The exact public column contract.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(Path), 0, typeof(string)),
        new SchemaColumn(nameof(HeadSha), 1, typeof(string)),
        new SchemaColumn(nameof(Branch), 2, typeof(string)),
        new SchemaColumn(nameof(IsBare), 3, typeof(bool)),
        new SchemaColumn(nameof(IsDetached), 4, typeof(bool)),
        new SchemaColumn(nameof(IsLocked), 5, typeof(bool)),
        new SchemaColumn(nameof(LockReason), 6, typeof(string)),
        new SchemaColumn(nameof(IsPrunable), 7, typeof(bool)),
        new SchemaColumn(nameof(PruneReason), 8, typeof(string))
    ];

    /// <summary>Maps column names to their physical positions.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap =
        Columns.ToDictionary(c => c.ColumnName, c => c.ColumnIndex);

    internal WorktreeEntity(string path, string? headSha, string? branch, bool isBare, bool isDetached,
        bool isLocked, string? lockReason, bool isPrunable, string? pruneReason)
    {
        Path = path; HeadSha = headSha; Branch = branch; IsBare = isBare; IsDetached = isDetached;
        IsLocked = isLocked; LockReason = lockReason; IsPrunable = isPrunable; PruneReason = pruneReason;
    }

    /// <summary>Gets the registered checkout path, or the repository directory for a bare entry.</summary>
    public string Path { get; }
    /// <summary>Gets the checked-out commit; absent for bare or unborn entries.</summary>
    public string? HeadSha { get; }
    /// <summary>Gets the full branch reference; absent for bare or detached entries.</summary>
    public string? Branch { get; }
    /// <summary>Gets whether the entry is a bare repository.</summary>
    public bool IsBare { get; }
    /// <summary>Gets whether HEAD is detached.</summary>
    public bool IsDetached { get; }
    /// <summary>Gets whether the registration is locked.</summary>
    public bool IsLocked { get; }
    /// <summary>Gets Git's optional lock explanation.</summary>
    public string? LockReason { get; }
    /// <summary>Gets whether Git reports the registration as prunable.</summary>
    public bool IsPrunable { get; }
    /// <summary>Gets Git's optional prune explanation.</summary>
    public string? PruneReason { get; }
}

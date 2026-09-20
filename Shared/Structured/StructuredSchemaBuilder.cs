#nullable enable

using System;
using System.Collections.Generic;

namespace Musoq.DataSources.Structured;

internal sealed class StructuredSchemaBuilder
{
    private readonly Dictionary<string, MutableColumn> _columnsByName = new(StringComparer.Ordinal);
    private readonly List<MutableColumn> _columns = [];
    private readonly StructuredTypeConflictBehavior _conflictBehavior;
    private long _currentRow = -1;

    public StructuredSchemaBuilder(StructuredTypeConflictBehavior conflictBehavior)
    {
        _conflictBehavior = conflictBehavior;
    }

    public long RowCount => _currentRow + 1;

    public void DeclareColumn(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_columnsByName.ContainsKey(name))
            throw new ArgumentException($"Structured column '{name}' is already declared.", nameof(name));

        var column = new MutableColumn(name, _columns.Count);
        _columns.Add(column);
        _columnsByName.Add(name, column);
    }

    public void BeginRow()
    {
        _currentRow++;
    }

    public void Observe(string name, StructuredValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_currentRow < 0)
            throw new InvalidOperationException("BeginRow must be called before observing structured fields.");

        if (!_columnsByName.TryGetValue(name, out var column))
        {
            column = new MutableColumn(name, _columns.Count);
            _columns.Add(column);
            _columnsByName.Add(name, column);
        }

        if (column.LastSeenRow == _currentRow)
            throw new StructuredDuplicateFieldException(name, _currentRow);

        column.LastSeenRow = _currentRow;
        column.PresentValueCount++;
        column.TypeState = column.TypeState.Observe(kind, _conflictBehavior);
    }

    public StructuredSchemaSnapshot Build(
        StructuredFileIdentity identity,
        IEnumerable<StructuredPartition>? partitions = null)
    {
        var columns = new StructuredColumnSnapshot[_columns.Count];
        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            var state = column.PresentValueCount < RowCount
                ? column.TypeState.WithMissingValue()
                : column.TypeState;
            columns[index] = new StructuredColumnSnapshot(
                column.Name,
                column.SourceOrdinal,
                state,
                column.PresentValueCount);
        }

        return new StructuredSchemaSnapshot(identity, columns, RowCount, partitions);
    }

    private sealed class MutableColumn(string name, int sourceOrdinal)
    {
        public string Name { get; } = name;

        public int SourceOrdinal { get; } = sourceOrdinal;

        public StructuredTypeState TypeState { get; set; } = StructuredTypeState.Empty;

        public long PresentValueCount { get; set; }

        public long LastSeenRow { get; set; } = -1;
    }
}

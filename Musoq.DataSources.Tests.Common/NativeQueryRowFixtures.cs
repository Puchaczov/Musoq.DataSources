#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Tests.Common;

/// <summary>Creates concretely closed source rows; heterogeneous access is restricted to assertions.</summary>
public static class NativeQueryRowFixtures
{
    public static RowSource<FixtureRow> Open(IQueryScopedRowSourceSchema schema, string name, string path,
        SourceExecutionContext context, QueryRowShape shape)
    {
        var cells = typeof(EmptyCells);
        foreach (var field in shape.Fields.Reverse())
            cells = typeof(Cells<,>).MakeGenericType(field.FieldType, cells);
        var factory = typeof(NativeQueryRowFixtures).GetMethod(nameof(OpenClosed), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(cells).CreateDelegate<Func<IQueryScopedRowSourceSchema, string, string,
                SourceExecutionContext, QueryRowShape, RowSource<FixtureRow>>>();
        return factory(schema, name, path, context, shape);
    }

    private static RowSource<FixtureRow> OpenClosed<TCells>(IQueryScopedRowSourceSchema schema,
        string name, string path, SourceExecutionContext context, QueryRowShape shape)
        where TCells : struct, ICells<TCells>
    {
        var source = NativeSourceCalls.Query(schema, SourceArguments.Create(path, SourceArguments.Empty))
            .Read<ClosedRow<TCells>, Materializer<TCells>>(name, new QueryScopedRowSourceRequest(context, shape));
        return new FixtureSource<TCells>(source);
    }

    public abstract class FixtureRow : IReadOnlyRow, IEnumerable<object?>
    {
        public abstract int Count { get; }
        public int Length => Count;
        public object? this[int index] => NativeRowAssertions.ReadValue(this, index);
        public abstract T GetValue<T>(int columnIndex);
        public abstract void VisitColumns<TVisitor>(ref TVisitor visitor)
            where TVisitor : IColumnValueVisitor, allows ref struct;
        public IEnumerator<object?> GetEnumerator() => NativeRowAssertions.ReadValues(this).AsEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public interface ICells<TSelf> where TSelf : struct
    {
        static abstract int Count { get; }
        static abstract TSelf Read<TReader>(scoped ref TReader reader, int offset)
            where TReader : IQuerySourceFieldReader, allows ref struct;
        T GetValue<T>(int index);
        void Visit<TVisitor>(ref TVisitor visitor, int offset)
            where TVisitor : IColumnValueVisitor, allows ref struct;
    }

    public sealed class FixtureChunk(IReadOnlyList<FixtureRow> rows, Type storageType) : IReadOnlyList<FixtureRow>
    {
        public Type StorageType { get; } = storageType;
        public int Count => rows.Count;
        public FixtureRow this[int index] => rows[index];
        public IEnumerator<FixtureRow> GetEnumerator() => rows.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public readonly struct EmptyCells : ICells<EmptyCells>
    {
        public static int Count => 0;
        public static EmptyCells Read<TReader>(scoped ref TReader reader, int offset)
            where TReader : IQuerySourceFieldReader, allows ref struct => default;
        public T GetValue<T>(int index) => throw new IndexOutOfRangeException();
        public void Visit<TVisitor>(ref TVisitor visitor, int offset)
            where TVisitor : IColumnValueVisitor, allows ref struct { }
    }

    public readonly struct Cells<TValue, TRest>(TValue value, TRest rest) : ICells<Cells<TValue, TRest>>
        where TRest : struct, ICells<TRest>
    {
        private readonly TValue _value = value;
        private readonly TRest _rest = rest;
        public static int Count => 1 + TRest.Count;
        public static Cells<TValue, TRest> Read<TReader>(scoped ref TReader reader, int offset)
            where TReader : IQuerySourceFieldReader, allows ref struct
            => new(reader.Read<TValue>(offset), TRest.Read(ref reader, offset + 1));
        public T GetValue<T>(int index)
        {
            if (index != 0)
                return _rest.GetValue<T>(index - 1);
            if (typeof(T) != typeof(TValue))
                throw new InvalidOperationException("Fixture access must use the concrete field type.");
            return Unsafe.As<TValue, T>(ref Unsafe.AsRef(in _value));
        }
        public void Visit<TVisitor>(ref TVisitor visitor, int offset)
            where TVisitor : IColumnValueVisitor, allows ref struct
        {
            visitor.Visit(offset, in _value);
            _rest.Visit(ref visitor, offset + 1);
        }
    }

    private sealed class ClosedRow<TCells>(TCells cells) : FixtureRow where TCells : struct, ICells<TCells>
    {
        private readonly TCells _cells = cells;
        public override int Count => TCells.Count;
        public override T GetValue<T>(int columnIndex) => _cells.GetValue<T>(columnIndex);
        public override void VisitColumns<TVisitor>(ref TVisitor visitor) => _cells.Visit(ref visitor, 0);
    }

    private readonly struct Materializer<TCells> : IQueryRowMaterializer<ClosedRow<TCells>>
        where TCells : struct, ICells<TCells>
    {
        private static readonly ClosedRow<TCells> Empty = new(default);
        public static ClosedRow<TCells> Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct
            => TCells.Count == 0 ? Empty : new(TCells.Read(ref reader, 0));
    }

    private sealed class FixtureSource<TCells>(RowSource<ClosedRow<TCells>> source) : RowSource<FixtureRow>
        where TCells : struct, ICells<TCells>
    {
        public override IEnumerable<IReadOnlyList<FixtureRow>> Chunks
        {
            get
            {
                foreach (var chunk in source.Chunks)
                {
                    var storageType = chunk is RowChunk<ClosedRow<TCells>> slice ? slice.Source.GetType() : chunk.GetType();
                    yield return new FixtureChunk(chunk, storageType);
                }
            }
        }
    }
}

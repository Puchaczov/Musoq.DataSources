using System;
using System.Collections.Concurrent;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Tests.Common;

public sealed class PatternWorkMetrics : ISourceDiagnosticsSink
{
    private readonly ConcurrentDictionary<string, long> _values = new();
    public long this[string name] => _values.TryGetValue(name, out var value) ? value : 0;
    public IDisposable Measure(string name, SourceDiagnosticOperation operation) => new Noop();
    public void AddRowsProduced(long count) { }
    public void AddBytesRead(long bytes) { }
    public void AddMetric(string name, long value) => _values.AddOrUpdate(name, value, (_, previous) => previous + value);
    private sealed class Noop : IDisposable { public void Dispose() { } }
}

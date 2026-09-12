using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Git.Benchmarks;

internal sealed class ReferenceBenchmarkDiagnostics : ISourceDiagnosticsSink
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _metrics = new(StringComparer.Ordinal);

    public IDisposable Measure(string name, SourceDiagnosticOperation operation) => NoopDisposable.Instance;

    public void AddRowsProduced(long count)
    {
    }

    public void AddBytesRead(long bytes)
    {
    }

    public void AddMetric(string name, long value)
    {
        lock (_gate)
            _metrics[name] = _metrics.GetValueOrDefault(name) + value;
    }

    public IReadOnlyDictionary<string, long> Snapshot()
    {
        lock (_gate)
            return new Dictionary<string, long>(_metrics, StringComparer.Ordinal);
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

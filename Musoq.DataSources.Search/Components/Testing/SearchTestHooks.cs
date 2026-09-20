#nullable enable

using System;
using System.Threading;

namespace Musoq.DataSources.Search.Components.Testing;

/// <summary>
///     Internal-only seams for deterministic Search contract tests. The
///     AsyncLocal scope flows into the producer and worker tasks of one query
///     without sharing mutable test state with another query.
/// </summary>
internal static class SearchTestHooks
{
    private static readonly AsyncLocal<SearchTestHooksState?> Current = new();

    public static IDisposable Install(SearchTestHooksState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var previous = Current.Value;
        Current.Value = state;
        return new RestoreScope(previous);
    }

    public static void BeforeContentOpen(string path)
    {
        Current.Value?.BeforeContentOpen?.Invoke(path);
    }

    public static void AfterObservation(string path)
    {
        Current.Value?.AfterObservation?.Invoke(path);
    }

    public static void Checkpoint()
    {
        Current.Value?.Checkpoint?.Invoke();
    }

    private sealed class RestoreScope(SearchTestHooksState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Current.Value = previous;
        }
    }
}

internal sealed class SearchTestHooksState
{
    public Action<string>? BeforeContentOpen { get; init; }

    public Action<string>? AfterObservation { get; init; }

    public Action? Checkpoint { get; init; }
}

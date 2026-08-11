using System;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Caches a successfully resolved managed snapshot without caching a native LibGit2Sharp object or a failed
/// repository operation. Nested entity properties can therefore be repeated safely after their producer scope ended.
/// </summary>
internal sealed class GitNestedSnapshot<T>
    where T : class
{
    private readonly object _gate = new();
    private bool _resolved;
    private T? _value;

    public T? GetOrCreate(Func<T?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_resolved)
            return _value;

        lock (_gate)
        {
            if (_resolved)
                return _value;

            // Do not set _resolved before factory succeeds: a transient/repository failure remains actionable on
            // a later access instead of being silently converted into a cached null.
            _value = factory();
            _resolved = true;
            return _value;
        }
    }

    public void Set(T? value)
    {
        lock (_gate)
        {
            _value = value;
            _resolved = true;
        }
    }
}

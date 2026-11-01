using System;
using System.Collections;
using System.Collections.Generic;

namespace Musoq.DataSources.Git;

/// <summary>Stable collection identity with a new resource-owning iterator for every enumeration.</summary>
internal sealed class GitLiveSequence<T>(Func<IEnumerable<T>> read) : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator() => read().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

#nullable enable

using System;

namespace Musoq.DataSources.Search.Components.Text;

internal sealed class SearchSliceWindow
{
    private SearchSliceWindow(long skip, long? take)
    {
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip));
        if (take < 0)
            throw new ArgumentOutOfRangeException(nameof(take));

        _remainingSkip = skip;
        _remainingTake = take;
    }

    private long _remainingSkip;
    private long? _remainingTake;

    public bool IsSatisfied => _remainingTake == 0;

    public bool HasTake => _remainingTake.HasValue;

    public static SearchSliceWindow? Create(long? skip, long? take)
    {
        if (!skip.HasValue && !take.HasValue)
            return null;

        return new SearchSliceWindow(skip ?? 0, take);
    }

    public bool TryAccept()
    {
        if (_remainingSkip > 0)
        {
            _remainingSkip--;
            return false;
        }

        if (_remainingTake is 0)
            return false;

        if (_remainingTake.HasValue)
            _remainingTake--;

        return true;
    }
}

using System.Collections;
using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource
{
    public class ChunkEnumerator(
        BlockingCollection<IReadOnlyList<IObjectResolver>> readRows,
        Func<Exception?> getException,
        CancellationToken token)
        : IEnumerator<IObjectResolver>
    {
        private readonly BlockingCollection<IReadOnlyList<IObjectResolver>> _readRows = readRows ?? throw new ArgumentNullException(nameof(readRows));
        private IReadOnlyList<IObjectResolver>? _currentChunk;
        private int _currentIndex = -1;
        private const int MaxTakeAttempts = 10;

        public bool MoveNext()
        {
            var exception = getException();
            
            if (exception != null)
                throw exception;
            
            if (_currentChunk != null && ++_currentIndex < _currentChunk.Count)
                return true;

            return TryGetNextChunk();
        }

        private bool TryGetNextChunk()
        {
            for (var i = 0; i < MaxTakeAttempts; i++)
            {
                if (!TryTakeValidChunk(out _currentChunk)) continue;
                
                _currentIndex = 0;
                return true;
            }

            try
            {
                _currentChunk = _readRows.Take(token);
                _currentIndex = 0;
                return _currentChunk.Count > 0;
            }
            catch (OperationCanceledException)
            {
                return TryTakeRemainingItems();
            }
        }

        private bool TryTakeValidChunk(out IReadOnlyList<IObjectResolver>? chunk)
        {
            return _readRows.TryTake(out chunk) && chunk is {Count: > 0};
        }

        private bool TryTakeRemainingItems()
        {
            while (_readRows.Count > 0)
            {
                if (!TryTakeValidChunk(out _currentChunk)) continue;
                
                _currentIndex = 0;
                return true;
            }
            return false;
        }

        public void Reset() => throw new NotSupportedException("Chunk enumerator does not support reset.");

        public IObjectResolver Current => _currentChunk![_currentIndex];

        object IEnumerator.Current => Current;

        public void Dispose() { }
    }
}
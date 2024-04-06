using System.Collections;
using System.Collections.Concurrent;
using Musoq.DataSources.AsyncRowsSource.Exceptions;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource;

    public class ChunkEnumerator : IEnumerator<IObjectResolver>
    {
        private readonly BlockingCollection<IReadOnlyList<IObjectResolver>> _readRows;

        private IReadOnlyList<IObjectResolver>? _currentChunk;
        private int _currentIndex = -1;
        private readonly CancellationToken _token;
        private readonly Func<Exception?> _getParentException;

        public ChunkEnumerator(BlockingCollection<IReadOnlyList<IObjectResolver>> readRows, CancellationToken token, Func<Exception?> getParentException)
        {
            _readRows = readRows;
            _token = token;
            _getParentException = getParentException;
        }

        public bool MoveNext()
        {
            var exception = _getParentException();
            
            if (exception != null)
                throw exception;
            
            if (_currentChunk != null && _currentIndex++ < _currentChunk.Count - 1)
                return true;

            try
            {
                var wasTaken = false;
                for (var i = 0; i < 10; i++)
                {
                    if (!_readRows.TryTake(out _currentChunk) || _currentChunk == null || _currentChunk.Count == 0) continue;

                    wasTaken = true;
                    break;
                }

                if (!wasTaken)
                {
                    IReadOnlyList<IObjectResolver>? newChunk = null;
                    while (newChunk == null || newChunk.Count == 0)
                        newChunk = _readRows.Count > 0 ? _readRows.Take() : _readRows.Take(_token);

                    _currentChunk = newChunk;
                }

                _currentIndex = 0;
                return true;
            }
            catch (OperationCanceledException)
            {
                exception = _getParentException();
                
                if (exception != null)
                    throw new DataSourceException(exception);
                
                if (_readRows.Count <= 0) return false;

                _currentChunk = _readRows.Take();
                while (_readRows.Count > 0 && _currentChunk.Count == 0)
                    _currentChunk = _readRows.Take();

                _currentIndex = 0;
                return _currentChunk.Count > 0;

            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public void Reset()
        {
            throw new NotSupportedException("Chunk enumerator does not support reseting enumeration.");
        }

        public IObjectResolver Current => _currentChunk[_currentIndex];

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }
    }
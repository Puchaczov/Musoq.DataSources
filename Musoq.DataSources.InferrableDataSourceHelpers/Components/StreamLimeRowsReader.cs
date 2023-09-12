using System.Text;
using Musoq.DataSources.CompiledCode;

namespace Musoq.DataSources.InferrableDataSourceHelpers.Components;

public class StreamLimeRowsReader : IRowsReader<string>
{
    private readonly StreamReader _reader;
    private readonly ICompiledCode<string> _compiledCode;
    private string? _current;

    public StreamLimeRowsReader(Stream stream, ICompiledCode<string> compiledCode)
    {
        _reader = new StreamReader(stream, Encoding.UTF8, true, 1024 * 1024 * 10, false);
        _compiledCode = compiledCode;
    }
    
    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        return new ValueTask();
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        while (!_reader.EndOfStream)
        {
            var line = await _reader.ReadLineAsync();
            
            if (line == null)
                continue;

            if (_compiledCode.IsHeaderLine(line))
                continue;

            if (_compiledCode.IsDataLine(line))
            {
                _current = line;
                return true;
            }

            if (_compiledCode.IsFooterLine(line))
                return false;
        }

        return false;
    }

    public string Current => _current!;
}
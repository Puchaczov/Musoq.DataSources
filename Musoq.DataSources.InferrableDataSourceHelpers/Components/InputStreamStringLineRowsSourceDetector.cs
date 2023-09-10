using System.Text;
using Musoq.DataSources.CompiledCode;
using Musoq.DataSources.CodeGenerator;
using Musoq.DataSources.JsonHelpers;
using Musoq.Schema.DataSources;
using Newtonsoft.Json;

namespace Musoq.DataSources.InferrableDataSourceHelpers.Components;

public class InputStreamStringLineRowsSourceDetector : DynamicRowsSourceDetector<string>
{
    private readonly Stream _stream;
    private IDictionary<int, string>? _indexToNameMap;
    private int _index;
    private string? _allText;
    
    public InputStreamStringLineRowsSourceDetector(ICodeGenerator codeGenerator, string tool, Stream stream) 
        : base(codeGenerator, tool)
    {
        _stream = stream;
    }

    public override IObjectResolver Resolve(string jsonRow, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(jsonRow, Encoding.UTF8);
        using var contentReader = new JsonTextReader(reader);
        
        var parsedObject = JsonParser.ParseObject(contentReader, cancellationToken);

        _indexToNameMap ??= ((IDictionary<string, object?>) parsedObject).Keys
            .ToDictionary(_ => _index++);
        
        return new JsonObjectResolver(parsedObject, _indexToNameMap);
    }

    protected override Task<string> ProbeAsync()
    {
        if (_allText != null)
            return Task.FromResult(_allText);

        using var reader = new StreamReader(_stream, Encoding.UTF8);
        _allText = reader.ReadToEnd();
        
        reader.Dispose();
        
        var lines = _allText.Split('\r','\n', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        for (var index = 0; index < 6 && index < lines.Length; index++)
        {
            sb.AppendLine(lines[index]);
        }

        for (var index = lines.Length - 6; index < lines.Length && index >= 6; index++)
        {
            sb.AppendLine(lines[index]);
        }
        
        return Task.FromResult(sb.ToString());
    }

    protected override IRowsReader<string> CreateReader(ICompiledCode<string> compiledCode)
    {
        if (_allText == null)
            throw new InvalidOperationException("Cannot create reader without probing first.");
        
        return new StreamLimeRowsReader(new MemoryStream(
            Encoding.UTF8.GetBytes(_allText)), compiledCode);
    }
}
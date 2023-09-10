using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Musoq.DataSources.CompiledCode;
using Musoq.DataSources.CodeGenerator;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers.Components;

public abstract class DynamicRowsSourceDetector<TInput> : IRowsSourceDetector<TInput>
{
    private readonly ICodeGenerator _codeGenerator;
    private readonly string _tool;

    protected DynamicRowsSourceDetector(ICodeGenerator codeGenerator, string tool)
    {
        _codeGenerator = codeGenerator;
        _tool = tool;
    }

    public async Task<IRowsReader<TInput>> InferAsync(RuntimeContext context, Type inputType)
    {
        var code = string.Empty;
        code += "using Musoq.DataSources.CompiledCode;\n";
        code += "using System.Linq;\n";
        code += "using System;\n";
        code += "using System.Collections.Generic;\n";
        code += "using System.Threading;\n";
        code += await _codeGenerator.GenerateClassAsync(context.QueryInformation.Columns.Select(f => f.ColumnName).ToArray(), _tool, await ProbeAsync());
        
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        
        var compilationUnit = CSharpCompilation.Create(
            Guid.NewGuid().ToString(),
            new[] {syntaxTree},
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Queryable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ICompiledCode<>).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
        using var ms = new MemoryStream();
        var result = compilationUnit.Emit(ms);
        
        if (!result.Success)
            throw new InvalidOperationException("Cannot compile generated code.");
        
        ms.Seek(0, SeekOrigin.Begin);
        
        var assembly = Assembly.Load(ms.ToArray());
        var type = assembly.GetType("CompiledCode");
        
        if (type == null)
            throw new InvalidOperationException("Cannot find compiled type.");

        var interfaceType = typeof(ICompiledCode<>).MakeGenericType(inputType);
        
        if (!interfaceType.IsAssignableFrom(type))
            throw new InvalidOperationException("Cannot find compiled type.");
        
        //let the compiler do it's magic, with dynamic...
        dynamic compiledCode = Activator.CreateInstance(type) ?? throw new InvalidOperationException();

        return CreateReader(compiledCode);
    }

    public abstract IObjectResolver Resolve(TInput item, CancellationToken cancellationToken);

    protected abstract Task<string> ProbeAsync();
    
    protected abstract IRowsReader<TInput> CreateReader(ICompiledCode<TInput> compiledCode);
}
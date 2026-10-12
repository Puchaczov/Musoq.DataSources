using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn.Benchmarks;

/// <summary>Measures complete named results; BenchmarkDotNet generates derived execution runners.</summary>
[MemoryDiagnoser]
public class NativeNameSelectionBenchmarks
{
    private AdhocWorkspace _workspace = null!;
    private SolutionEntity _entity = null!;
    private readonly CSharpLibrary _library = new();
    private string _lastName = null!;

    [Params(100, 1000)]
    public int ClassCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _workspace = new AdhocWorkspace();
        var project = _workspace.AddProject("Names", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var source = string.Join(Environment.NewLine,
            Enumerable.Range(0, ClassCount).Select(static index => $"public class C{index:D4} {{ }}"));
        var document = project.AddDocument("Names.cs", SourceText.From(source));
        _entity = new SolutionEntity(document.Project.Solution, null!, CancellationToken.None);
        foreach (var input in _entity.Projects.SelectMany(static item => item.Documents))
            input.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        _lastName = $"C{ClassCount - 1:D4}";
        var actual = _library.GetClassesByNames(_entity, "C0000", _lastName).Select(static type => type.Name).ToArray();
        if (!actual.SequenceEqual(new[] { "C0000", _lastName }) || ConsumeNames() != 10 || ConsumeOriginalNames() != 10)
            throw new InvalidOperationException("Name selection differs from independent generated fixture expectations.");
    }

    [Benchmark]
    public int ConsumeNames()
    {
        var checksum = 0;
        foreach (var type in _library.GetClassesByNames(_entity, "C0000", _lastName))
            checksum += type.Name.Length;
        return checksum;
    }

    [Benchmark(Baseline = true)]
    public int ConsumeOriginalNames()
    {
        var checksum = 0;
        foreach (var type in OriginalNames(_entity, "C0000", _lastName))
            checksum += type.Name.Length;
        return checksum;
    }

    private static IEnumerable<ClassEntity> OriginalNames(SolutionEntity entity, params string[] names)
        => entity.Projects.SelectMany(static project => project.Documents).SelectMany(static document => document.Classes)
            .Where(type => names.Contains(type.Name));

    [GlobalCleanup]
    public void Cleanup() => _workspace.Dispose();
}

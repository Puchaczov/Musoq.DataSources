using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Converter;
using Musoq.Schema;
using Musoq.DataSources.Json;
using Musoq.DataSources.Search;
using Musoq.DataSources.SeparatedValues;

var root = args[0];
Directory.CreateDirectory(root);
var json = Path.Combine(root, "rows.json").Replace('\\','/');
var csv = Path.Combine(root, "rows.csv").Replace('\\','/');
File.WriteAllText(json, "[{\"Id\":1,\"Amount\":1.25},{\"Id\":2,\"Amount\":null},{\"Id\":3,\"Amount\":2.5}]");
File.WriteAllText(csv, "Id,Amount\n1,1.25\n2,2.5\n");
var searchRoot = Path.Combine(root, "search");
Directory.CreateDirectory(searchRoot);
File.WriteAllText(Path.Combine(searchRoot, "fixture.txt"), "TODO\nTODO\nTODO\n");
VerifyJson();
VerifyCsv();
VerifySearch();
Console.WriteLine("TYPED_DATASOURCES_CONSUMER_OK");

void VerifyJson()
{
    using var compiled = Compile($"select Id, Sum(Amount) over (order by Id) as Total from json.file('{json}') order by Id");
    using var table = compiled.Run();
    if (table.Count != 3 || table.Columns.Last().ColumnType != typeof(decimal?) ||
        !table.Select(static row => row.GetValue<long>(0)).SequenceEqual(new long[] { 1, 2, 3 }) ||
        !table.Select(static row => row.GetValue<decimal?>(1)).SequenceEqual(new decimal?[] { 1.25m, 1.25m, 3.75m }))
        throw new InvalidOperationException("Native JSON/window contract differs.");
}

void VerifyCsv()
{
    using var compiled = Compile("table CsvRow { Id: int, Amount: decimal }; couple separatedvalues.comma with table CsvRow as Rows; " +
        $"select Sum(Amount) as Total from Rows('{csv}', true, 0)");
    using var table = compiled.Run();
    if (table.Count != 1 || table.Columns.Single().ColumnName != "Total" || table.Columns.Single().ColumnType != typeof(decimal?) ||
        table[0].GetValue<decimal?>(0) != 3.75m) throw new InvalidOperationException("Native CSV contract differs.");
}

void VerifySearch()
{
    using var compiled = Compile($"select LineNumber from search.lines('{searchRoot.Replace('\\','/')}','TODO') where LineNumber > 1.5 order by LineNumber");
    using var table = compiled.Run();
    if (table.Columns.Single().ColumnType != typeof(long) || !table.Select(static row => row.GetValue<long>(0)).SequenceEqual(new long[] { 2, 3 }))
        throw new InvalidOperationException("Typed Search predicate contract differs.");
}

Musoq.Evaluator.CompiledQuery Compile(string sql) => InstanceCreator.CompileForExecution(sql,
    "TypedDataSources" + Guid.NewGuid().ToString("N"), new Provider(), new LoggerResolver());

sealed class Provider : ISchemaProvider
{
    public ISchema GetSchema(string name) => name.TrimStart('#') switch
    {
        "json" => new JsonSchema(), "search" => new SearchSchema(), "separatedvalues" => new SeparatedValuesSchema(),
        _ => throw new InvalidOperationException("Unknown consumer schema.")
    };
}
sealed class LoggerResolver : ILoggerResolver
{
    public ILogger ResolveLogger() => NullLogger.Instance;
    public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
}

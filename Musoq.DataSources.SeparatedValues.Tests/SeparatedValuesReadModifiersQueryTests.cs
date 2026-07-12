using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesReadModifiersQueryTests
{
    static SeparatedValuesReadModifiersQueryTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-US"));
    }

    [TestInitialize]
    public void SetCulture()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-US"));
    }

    [TestMethod]
    public void Query_WhenColumnsDeclareDifferentCultures_ShouldInterpretSameTextPerColumn()
    {
        var path = WriteTempFile("AmountPl;AmountUs\r\n1,234;1,234\r\n", Encoding.UTF8);

        try
        {
            var query = $@"
table CsvRow {{
    AmountPl: decimal culture 'pl-PL',
    AmountUs: decimal culture 'en-US'
}};
couple #separatedvalues.semicolon with table CsvRow as SourceRows;
select AmountPl, AmountUs from SourceRows('{ToQueryPath(path)}', true, 0);";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(2, table.Columns.Count());
            Assert.AreEqual("AmountPl", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual("AmountUs", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(decimal?), table.Columns.ElementAt(1).ColumnType);

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(1.234m, table[0][0]);
            Assert.AreEqual(1234m, table[0][1]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void Query_WhenDateTimeOffsetFormatIsDeclared_ShouldEvaluateAndReturnDateTimeOffset()
    {
        var path = WriteTempFile("ObservedAt\r\n2023-12-31T10:15:30.0000000+02:00\r\n", Encoding.UTF8);

        try
        {
            var query = $@"
table CsvRow {{
    ObservedAt: datetimeoffset format 'O'
}};
couple #separatedvalues.comma with table CsvRow as SourceRows;
select ObservedAt from SourceRows('{ToQueryPath(path)}', true, 0);";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("ObservedAt", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(DateTimeOffset?), table.Columns.ElementAt(0).ColumnType);

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(new DateTimeOffset(2023, 12, 31, 10, 15, 30, TimeSpan.FromHours(2)), table[0][0]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [TestMethod]
    public void Query_WhenCultureModifierIsInvalid_ShouldFailWithSeparatedValuesContractMessage()
    {
        var path = WriteTempFile("Amount\r\n1.23\r\n", Encoding.UTF8);

        try
        {
            var query = $@"
table CsvRow {{
    Amount: decimal culture 'invalid_culture'
}};
couple #separatedvalues.comma with table CsvRow as SourceRows;
select Amount from SourceRows('{ToQueryPath(path)}', true, 0);";

            try
            {
                var vm = CreateAndRunVirtualMachine(query);
                _ = vm.Run();
                Assert.Fail("Query should fail because the declared culture is invalid.");
            }
            catch (Exception exception)
            {
                var message = exception.ToString();
                StringAssert.Contains(message, "Culture 'invalid_culture' is not supported by #separatedvalues");
                Assert.IsFalse(
                    message.Contains(nameof(CultureNotFoundException), StringComparison.Ordinal),
                    "Invalid culture should be reported as a separated-values contract failure, not as a raw culture exception.");
            }
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new CsvSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string WriteTempFile(string content, Encoding encoding)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, encoding);
        return path;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string ToQueryPath(string path)
    {
        return path.Replace("\\", "/");
    }
}

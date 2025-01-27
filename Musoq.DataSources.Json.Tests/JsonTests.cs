using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Json.Tests
{
    [TestClass]
    public class JsonTests
    {
        [TestMethod]
        public void SimpleSelectTest()
        {
            var query =
                @"select Name, Age from #json.file('./JsonTestFile_First.json', './JsonTestFile_First.schema.json')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(2, table.Columns.Count());
            Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual("Age", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(long), table.Columns.ElementAt(1).ColumnType);
            
            Assert.IsTrue(table.Count == 3, "Table should have 3 entries");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "Aleksander" && 
                (long)row.Values[1] == 24L
            ), "First entry should be Aleksander, 24");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "Mikolaj" && 
                (long)row.Values[1] == 11L
            ), "Second entry should be Mikolaj, 11");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "Marek" && 
                (long)row.Values[1] == 45L
            ), "Third entry should be Marek, 45");
        }

        [TestMethod]
        public void SelectWithArrayLengthTest()
        {
            var query =
                @"select Name, Length(Books) from #json.file('./JsonTestFile_First.json', './JsonTestFile_First.schema.json')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(2, table.Columns.Count());
            Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual("Length(Books)", table.Columns.ElementAt(1).ColumnName);
            Assert.AreEqual(typeof(int), table.Columns.ElementAt(1).ColumnType);

            Assert.IsTrue(table.Count == 3, "Table should contain exactly 3 records");

            Assert.IsTrue(table.Any(r => 
                    (string)r.Values[0] == "Aleksander" && (int)r.Values[1] == 2),
                "Missing record for Aleksander with value 2");

            Assert.IsTrue(table.Any(r => 
                    (string)r.Values[0] == "Mikolaj" && (int)r.Values[1] == 0),
                "Missing record for Mikolaj with value 0");

            Assert.IsTrue(table.Any(r => 
                    (string)r.Values[0] == "Marek" && (int)r.Values[1] == 0),
                "Missing record for Marek with value 0");
        }

        [TestMethod]
        public void MakeFlatArrayTest()
        {
            var query =
                @"select MakeFlat(Array) from #json.file('./JsonTestFile_MakeFlatArray.json', './JsonTestFile_MakeFlatArray.schema.json')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("MakeFlat(Array)", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            
            Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "1, 2, 3"
            ), "First entry should be '1, 2, 3'");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == string.Empty
            ), "Second entry should be an empty string");
        }

        [TestMethod]
        public void JsonSource_Cancelled_ShouldBeEmpty()
        {
            using var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var source = new JsonSource("./JsonTestFile_First.json", tokenSource.Token);

            var fired = source.Rows.Count();

            Assert.AreEqual(0, fired);
        }

        [TestMethod]
        public void JsonSource_FullLoadTest()
        {
            var source = new JsonSource("./JsonTestFile_First.json", CancellationToken.None);

            var fired = source.Rows.Count();

            Assert.AreEqual(3, fired);
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new JsonSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }

        static JsonTests()
        {
            new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

            Culture.ApplyWithDefaultCulture();
        }
    }
}
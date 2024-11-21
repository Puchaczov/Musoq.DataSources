using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
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

            Assert.AreEqual(3L, table.Count);
            Assert.AreEqual("Aleksander", table[0].Values[0]);
            Assert.AreEqual(24L, table[0].Values[1]);
            Assert.AreEqual("Mikolaj", table[1].Values[0]);
            Assert.AreEqual(11L, table[1].Values[1]);
            Assert.AreEqual("Marek", table[2].Values[0]);
            Assert.AreEqual(45L, table[2].Values[1]);
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

            Assert.AreEqual(3, table.Count);
            Assert.AreEqual("Aleksander", table[0].Values[0]);
            Assert.AreEqual(2, table[0].Values[1]);
            Assert.AreEqual("Mikolaj", table[1].Values[0]);
            Assert.AreEqual(0, table[1].Values[1]);
            Assert.AreEqual("Marek", table[2].Values[0]);
            Assert.AreEqual(0, table[2].Values[1]);
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

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("1, 2, 3", table[0].Values[0]);
            Assert.AreEqual(string.Empty, table[1].Values[0]);
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
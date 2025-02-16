using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Os.Tests
{
    [TestClass]
    public class ZipTests
    {
        [TestInitialize]
        public void Initialize()
        {
            if (!Directory.Exists("./Results"))
                Directory.CreateDirectory("./Results");
        }

        [TestMethod]
        public void SimpleZipSelectTest()
        {
            var query = @"select FullName from #disk.zip('./Files.zip')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("FullName", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);
            
            Assert.IsTrue(table.Count == 3, "Table should have 3 entries");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "Files/File1.txt"
            ), "First entry should be Files/File1.txt");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "Files/File2.txt"
            ), "Second entry should be Files/File2.txt");

            Assert.IsTrue(table.Any(row => 
                (string)row.Values[0] == "Files/SubFolder/File3.txt"
            ), "Third entry should be Files/SubFolder/File3.txt");
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new OsSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }

        static ZipTests()
        {
            Culture.ApplyWithDefaultCulture();
        }
    }
}
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Os.Zip;
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

            Assert.AreEqual(3, table.Count);
            Assert.AreEqual("Files/File1.txt", table[0].Values[0]);
            Assert.AreEqual("Files/File2.txt", table[1].Values[0]);
            Assert.AreEqual("Files/SubFolder/File3.txt", table[2].Values[0]);
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new OsSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }

        static ZipTests()
        {
            new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

            Culture.ApplyWithDefaultCulture();
        }
    }
}
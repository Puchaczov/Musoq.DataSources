using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;

namespace Musoq.DataSources.System.Tests
{
    [TestClass]
    public class RangeTests
    {

        [TestMethod]
        public void RangeMaxTest()
        {
            var query = "select Value from #system.range(5)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            
            Assert.IsTrue(table.Count == 5, "Table should contain exactly 5 records");

            Assert.IsTrue(table.Any(r => (long)r[0] == 0L), "Missing record with value 0");
            Assert.IsTrue(table.Any(r => (long)r[0] == 1L), "Missing record with value 1");
            Assert.IsTrue(table.Any(r => (long)r[0] == 2L), "Missing record with value 2");
            Assert.IsTrue(table.Any(r => (long)r[0] == 3L), "Missing record with value 3");
            Assert.IsTrue(table.Any(r => (long)r[0] == 4L), "Missing record with value 4");
        }

        [TestMethod]
        public void RangeMinMaxTest()
        {
            var query = "select Value from #system.range(1, 5)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            
            Assert.IsTrue(table.Count == 4, "Table should contain exactly 4 records");

            Assert.IsTrue(table.Any(r => (long)r[0] == 1L), "Missing record with value 1");
            Assert.IsTrue(table.Any(r => (long)r[0] == 2L), "Missing record with value 2");
            Assert.IsTrue(table.Any(r => (long)r[0] == 3L), "Missing record with value 3");
            Assert.IsTrue(table.Any(r => (long)r[0] == 4L), "Missing record with value 4");
        }


        [TestMethod]
        public void RangeMinSignedMaxTest()
        {
            var query = "select Value from #system.range(-1, 2)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            
            Assert.IsTrue(table.Count == 3, "Table should contain exactly 3 records");

            Assert.IsTrue(table.Any(r => (long)r[0] == -1L), "Missing record with value -1");
            Assert.IsTrue(table.Any(r => (long)r[0] == 0L), "Missing record with value 0");
            Assert.IsTrue(table.Any(r => (long)r[0] == 1L), "Missing record with value 1");
        }


        [Ignore]
        [TestMethod]
        public void RowNumberEvenForRangeMinSignedMaxTest()
        {
            var query = "select Value from #system.range(0, 5) where RowNumber() % 2 = 0";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            
            Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

            Assert.IsTrue(table.Any(r => (long)r[0] == 1L), "Missing record with value 1");
            Assert.IsTrue(table.Any(r => (long)r[0] == 3L), "Missing record with value 3");
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new SystemSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }
    }
}

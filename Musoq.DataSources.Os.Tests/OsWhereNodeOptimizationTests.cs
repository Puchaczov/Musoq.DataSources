using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Os.Tests
{
    [TestClass]
    public class OsWhereNodeOptimizationTests
    {
        [TestInitialize]
        public void Initialize()
        {
            if (!System.IO.Directory.Exists("./Results"))
                System.IO.Directory.CreateDirectory("./Results");
        }

        [TestMethod]
        public void WhenFilesFilteredByExtension_ShouldReturnOnlyMatchingFiles()
        {
            var query = "select f.Name, f.Extension from #os.files('./Files', false) f where f.Extension = '.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var result = vm.Run();

            Assert.AreEqual(2, result.Count, "Both File1.txt and File2.txt should be returned when filtering by .txt extension");
            Assert.IsTrue(result.All(r => (string)r[1] == ".txt"), "All returned files should have .txt extension");
            Assert.IsTrue(result.Any(r => (string)r[0] == "File1.txt"), "File1.txt should be in results");
            Assert.IsTrue(result.Any(r => (string)r[0] == "File2.txt"), "File2.txt should be in results");
        }

        [TestMethod]
        public void WhenFilesFilteredByName_ShouldReturnOnlyMatchingFile()
        {
            var query = "select f.Name, f.Extension from #os.files('./Files', false) f where f.Name = 'File1.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var result = vm.Run();

            Assert.AreEqual(1, result.Count, "Exactly one file named 'File1.txt' should be returned");
            Assert.AreEqual("File1.txt", (string)result[0][0]);
            Assert.AreEqual(".txt", (string)result[0][1]);
        }

        [TestMethod]
        public void WhenFilesFilteredByNonExistentName_ShouldReturnNoFiles()
        {
            var query = "select f.Name from #os.files('./Files', false) f where f.Name = 'NonExistent.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var result = vm.Run();

            Assert.AreEqual(0, result.Count, "No files should match a non-existent name");
        }

        [TestMethod]
        public void WhenDirectoriesFilteredByName_ShouldReturnOnlyMatchingDirectory()
        {
            var query = "select d.Name from #os.directories('./Directories', false) d where d.Name = 'Directory1'";

            var vm = CreateAndRunVirtualMachine(query);
            var result = vm.Run();

            Assert.AreEqual(1, result.Count, "Exactly one directory named 'Directory1' should be returned");
            Assert.AreEqual("Directory1", (string)result[0][0]);
        }

        [TestMethod]
        public void WhenDirectoriesFilteredByNonExistentName_ShouldReturnNoDirectories()
        {
            var query = "select d.Name from #os.directories('./Directories', false) d where d.Name = 'NonExistent'";

            var vm = CreateAndRunVirtualMachine(query);
            var result = vm.Run();

            Assert.AreEqual(0, result.Count, "No directories should match a non-existent name");
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new OsSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }

        static OsWhereNodeOptimizationTests()
        {
            Culture.ApplyWithDefaultCulture();
        }
    }
}

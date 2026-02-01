using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Os.Compare.Directories;
using Musoq.DataSources.Os.Directories;
using Musoq.DataSources.Os.Dlls;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Os.Tests.Utils;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Tests
{
    [TestClass]
    public class QueryDiskTests
    {
        [TestInitialize]
        public void Initialize()
        {
            if (!Directory.Exists("./Results"))
                Directory.CreateDirectory("./Results");
        }

        [TestMethod]
        public void ComplexObjectPropertyTest()
        {
            var query = "select Parent.Name from #disk.directories('./Directories', false)";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Parent.Name", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("Directories", table[0].Values[0]);
            Assert.AreEqual("Directories", table[1].Values[0]);
        }

        [TestMethod]
        public void DescFilesTest()
        {
            var query = "desc #os.files('./','false')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(3, table.Columns.Count());

            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.Name) && (string)row[2] == typeof(string).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.FileName) && (string)row[2] == typeof(string).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.CreationTime) && (string)row[2] == typeof(DateTimeOffset).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.CreationTimeUtc) && (string)row[2] == typeof(DateTimeOffset).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.DirectoryPath) && (string)row[2] == typeof(string).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.DirectoryName) && (string)row[2] == typeof(string).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.Extension) && (string)row[2] == typeof(string).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.FullPath) && (string)row[2] == typeof(string).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.Exists) && (string)row[2] == typeof(bool).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.IsReadOnly) && (string)row[2] == typeof(bool).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(FileEntity.Length) && (string)row[2] == typeof(long).FullName));
        }

        [TestMethod]
        public void DescDllsTest()
        {
            var query = "desc #os.dlls('./','false')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(3, table.Columns.Count());

            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(DllInfo.FileInfo) && (string)row[2] == typeof(FileInfo).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(DllInfo.Assembly) && (string)row[2] == typeof(Assembly).FullName));
            Assert.IsTrue(table.Any(row => (string)row[0] == nameof(DllInfo.Version) && (string)row[2] == typeof(FileVersionInfo).FullName));
        }

        [TestMethod]
        public void FilesSourceIterateDirectoriesTest()
        {
            var mockLogger = new Mock<ILogger>();
            
            var source = new TestFilesSource("./Directories", false, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty, mockLogger.Object));

            var folders = source.GetFiles();

            Assert.AreEqual(1, folders.Count);

            Assert.AreEqual("TestFile1.txt", ((FileEntity)folders[0].Contexts[0]).Name);
        }

        [TestMethod]
        public void FilesSourceIterateWithNestedDirectoriesTest()
        {
            var mockLogger = new Mock<ILogger>();
            
            var source = new TestFilesSource("./Directories", true, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var folders = source.GetFiles();

            Assert.AreEqual(4, folders.Count);

            Assert.AreEqual("TestFile1.txt", ((FileEntity)folders[0].Contexts[0]).Name);
            Assert.AreEqual("TextFile2.txt", ((FileEntity)folders[1].Contexts[0]).Name);
            Assert.AreEqual("TextFile3.txt", ((FileEntity)folders[2].Contexts[0]).Name);
            Assert.AreEqual("TextFile1.txt", ((FileEntity)folders[3].Contexts[0]).Name);
        }

        [TestMethod]
        public void DirectoriesSourceIterateDirectoriesTest()
        {
            var mockLogger = new Mock<ILogger>();
            
            var source = new TestDirectoriesSource("./Directories", false, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var directories = source.GetDirectories();

            Assert.AreEqual(2, directories.Count);

            Assert.IsTrue(directories.Any(dir => ((DirectoryInfo)dir.Contexts[0]).Name == "Directory1"));
            Assert.IsTrue(directories.Any(dir => ((DirectoryInfo)dir.Contexts[0]).Name == "Directory2"));
        }

        [TestMethod]
        public void TestDirectoriesSourceIterateWithNestedDirectories()
        {
            var mockLogger = new Mock<ILogger>();
            
            var source = new TestDirectoriesSource("./Directories", true, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var directories = source.GetDirectories();

            Assert.AreEqual(3, directories.Count);
            
            Assert.IsTrue(directories.Any(dir => ((DirectoryInfo)dir.Contexts[0]).Name == "Directory1"));
            Assert.IsTrue(directories.Any(dir => ((DirectoryInfo)dir.Contexts[0]).Name == "Directory2"));
            Assert.IsTrue(directories.Any(dir => ((DirectoryInfo)dir.Contexts[0]).Name == "Directory3"));
        }

        [TestMethod]
        public void NonExistingDirectoryTest()
        {
            var mockLogger = new Mock<ILogger>();
            
            var source = new TestDirectoriesSource("./Some/Non/Existing/Path", true, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var directories = source.GetDirectories();

            Assert.AreEqual(0, directories.Count);
        }

        [TestMethod]
        public void NonExistingFileTest()
        {
            var mockLogger = new Mock<ILogger>();
            
            var source = new TestFilesSource("./Some/Non/Existing/Path.pdf", true, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var directories = source.GetFiles();

            Assert.AreEqual(0, directories.Count);
        }

        [TestMethod]
        public void DirectoriesSource_CancelledLoadTest()
        {
            var mockLogger = new Mock<ILogger>();
            
            using var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var source = new DirectoriesSource("./Directories", true, new RuntimeContext(
                "test",
                tokenSource.Token, 
                Array.Empty<ISchemaColumn>(), 
                new Dictionary<string, string>(),
                QuerySourceInfo.Empty,
                mockLogger.Object));

            var fired = source.Rows.Count();

            Assert.AreEqual(0, fired);
        }

        [TestMethod]
        public void DirectoriesSource_FullLoadTest()
        {
            var mockLogger = new Mock<ILogger>();
            var source = new DirectoriesSource("./Directories", true, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var fired = source.Rows.Count();

            Assert.AreEqual(3, fired);
        }

        [TestMethod]
        public void File_GetFirstByte_Test()
        {
            var query = "select ToHex(GetFileBytes(2), '|') from #disk.files('./Files', false) where Name = 'File1.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Count);

            Assert.AreEqual("EF|BB", table[0][0]);
        }

        [TestMethod]
        public void File_SkipTwoBytesAndTakeFiveBytes_Test()
        {
            var query = "select ToHex(EnumerableToArray(Take(Skip(GetFileBytes(), 2), 5)), '|') from #disk.files('./Files', false) where Name = 'File1.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Count);

            Assert.AreEqual("BF|45|78|61|6D", table[0][0]);
        }

        [TestMethod]
        public void File_SkipTwoBytesAndTakeFiveBytes2_Test()
        {
            var query = "select ToHex(EnumerableToArray(SkipAndTake(GetFileBytes(), 2, 5)), '|') from #disk.files('./Files', false) where Name = 'File1.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Count);

            Assert.AreEqual("BF|45|78|61|6D", table[0][0]);
        }

        [TestMethod]
        public void File_GetHead_Test()
        {
            var query = "select ToHex(Head(2), '|') from #disk.files('./Files', false) where Name = 'File1.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Count);

            Assert.AreEqual("EF|BB", table[0][0]);
        }

        [TestMethod]
        public void File_GetTail_Test()
        {
            var query = "select ToHex(Tail(2), '|') from #disk.files('./Files', false) where Name = 'File1.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Count);

            Assert.AreEqual("31|2E", table[0][0]);
        }

        [TestMethod]
        public void File_GetBase64_Test()
        {
            var query = "select Base64File() from #disk.files('./Files', false) where Name = 'File1.txt'";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(1, table.Count);

            Assert.AreEqual("77u/RXhhbXBsZSBmaWxlIDEu", table[0][0]);
        }

        [TestMethod]
        public void FilesSource_CancelledLoadTest()
        {
            var mockLogger = new Mock<ILogger>();
            using var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var source = new FilesSource("./Directories", true, new RuntimeContext(
                "test",
                tokenSource.Token, 
                Array.Empty<ISchemaColumn>(), 
                new Dictionary<string, string>(),
                QuerySourceInfo.Empty,
                mockLogger.Object));

            var fired = source.Rows.Count();

            Assert.AreEqual(0, fired);
        }

        [TestMethod]
        public void FilesSource_FullLoadTest()
        {
            var mockLogger = new Mock<ILogger>();
            var source = new FilesSource("./Directories", true, 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var fired = source.Rows.Count();

            Assert.AreEqual(4, fired);
        }

        [TestMethod]
        public void DirectoriesCompare_CompareTwoDirectories()
        {
            var mockLogger = new Mock<ILogger>();
            var source = new CompareDirectoriesSource("./Directories/Directory1", "./Directories/Directory2", 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var rows = source.Rows.ToArray();

            var firstRow = rows[0].Contexts[0] as CompareDirectoriesResult;
            var secondRow = rows[1].Contexts[0] as CompareDirectoriesResult;
            var thirdRow = rows[2].Contexts[0] as CompareDirectoriesResult;

            Assert.AreEqual(new FileInfo("./Directories/Directory1/TextFile1.txt").FullName, firstRow.SourceFile.FullPath);
            Assert.AreEqual(null, firstRow.DestinationFile);
            Assert.AreEqual(State.Removed, firstRow.State);


            Assert.AreEqual(null, secondRow.SourceFile);
            Assert.AreEqual(new FileInfo("./Directories/Directory2/TextFile2.txt").FullName, secondRow.DestinationFile.FullPath);
            Assert.AreEqual(State.Added, secondRow.State);


            Assert.AreEqual(null, thirdRow.SourceFile);
            Assert.AreEqual(new FileInfo("./Directories/Directory2/Directory3/TextFile3.txt").FullName, thirdRow.DestinationFile.FullPath);
            Assert.AreEqual(State.Added, thirdRow.State);
        }

        [TestMethod]
        public void DirectoriesCompare_CompareWithItself()
        {
            var mockLogger = new Mock<ILogger>();
            var source = new CompareDirectoriesSource("./Directories/Directory1", "./Directories/Directory1", 
                new RuntimeContext(
                    "test",
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    QuerySourceInfo.Empty,
                    mockLogger.Object));

            var rows = source.Rows.ToArray();

            var firstRow = rows[0].Contexts[0] as CompareDirectoriesResult;

            Assert.AreEqual(new FileInfo("./Directories/Directory1/TextFile1.txt").FullName, firstRow!.SourceFile!.FullPath);
            Assert.AreEqual(new FileInfo("./Directories/Directory1/TextFile1.txt").FullName, firstRow.DestinationFile!.FullPath);
            Assert.AreEqual(State.TheSame, firstRow.State);
        }

        [TestMethod]
        public void Query_CompareTwoDirectories()
        {
            var query = "select * from #disk.DirsCompare('./Directories/Directory1', './Directories/Directory2')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
        }

        [TestMethod]
        public void Query_CompareTwoDirectories_WithSha()
        {
            var query = "select Sha256File(SourceFile) from #disk.DirsCompare('./Directories/Directory1', './Directories/Directory2') where SourceFile is not null";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
        }

        [TestMethod]
        public void Query_IntersectSameDirectoryTest()
        {
            var query = @"
with IntersectedFiles as (
	select a.Name as Name, a.Sha256File() as sha1, b.Sha256File() as sha2 from #os.files('.\Files', true) a inner join #os.files('.\Files', true) b on a.FullPath = b.FullPath
)
select * from IntersectedFiles";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            
            Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "File1.txt" && 
                    r[1].Equals(r[2])),
                "Missing File1.txt record or content mismatch");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "File2.txt" && 
                    r[1].Equals(r[2])),
                "Missing File2.txt record or content mismatch");
        }

        [TestMethod]
        public void Query_DirectoryDiffTest()
        {
            var query = @"
with FirstDirectory as (
    select a.GetRelativePath('.\Files') as RelativeName, a.Sha256File() as sha from #os.files('.\Files', true) a
), SecondDirectory as (
    select a.GetRelativePath('.\Files2') as RelativeName, a.Sha256File() as sha from #os.files('.\Files2', true) a
), IntersectedFiles as (
	select a.RelativeName as RelativeName, a.sha as sha1, b.sha as sha2 from FirstDirectory a inner join SecondDirectory b on a.RelativeName = b.RelativeName
), ThoseInLeft as (
	select a.RelativeName as RelativeName, a.sha as sha1, '' as sha2 from FirstDirectory a left outer join SecondDirectory b on a.RelativeName = b.RelativeName where b.RelativeName is null
), ThoseInRight as (
	select b.RelativeName as RelativeName, '' as sha1, b.sha as sha2 from FirstDirectory a right outer join SecondDirectory b on a.RelativeName = b.RelativeName where a.RelativeName is null
)
select RelativeName, (case when sha1 <> sha2 then 'modified' else 'the same' end) as state from IntersectedFiles
union all (RelativeName)
select RelativeName, 'removed' as state from ThoseInLeft
union all (RelativeName)
select RelativeName, 'added' as state from ThoseInRight";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.IsTrue(table.Count == 3, "Table should contain exactly 3 records");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "\\File1.txt" && 
                    (string)r[1] == "modified"),
                "Missing modified File1.txt record");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "\\File2.txt" && 
                    (string)r[1] == "removed"),
                "Missing removed File2.txt record");

            Assert.IsTrue(table.Any(r => 
                    (string)r[0] == "\\File3.txt" && 
                    (string)r[1] == "added"),
                "Missing added File3.txt record");
        }

        [TestMethod]
        public void Query_ShouldNotThrowException()
        {
            var query = "select (case when SourceFile is not null then ToHex(Head(SourceFile, 5), '|') else '' end) as t, DestinationFileRelative, State from #os.dirscompare('./Files', './Files')";
            
            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
        }

        [TestMethod]
        public void TraverseToDirectoryFromRootTest() 
        {
            var library = new OsLibrary();
            var separator = Path.DirectorySeparatorChar;

            Assert.AreEqual("this", library.SubPath($"this{separator}is{separator}test", 0));
            Assert.AreEqual($"this{separator}is", library.SubPath($"this{separator}is{separator}test", 1));
            Assert.AreEqual($"this{separator}is{separator}test", library.SubPath($"this{separator}is{separator}test", 2));
            Assert.AreEqual($"this{separator}is{separator}test", library.SubPath($"this{separator}is{separator}test", 10));
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new OsSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }

        static QueryDiskTests()
        {
            Culture.ApplyWithDefaultCulture();
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.Os.Compare.Directories;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public sealed class OsStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "file",
            [typeof(string)],
            ["./Files/File1.txt"],
            "select * from os.file('./Files/File1.txt')",
            FileColumns(),
            []),
        new(
            "files",
            [typeof(string), typeof(bool)],
            ["./Files", false],
            "select * from os.files('./Files', false)",
            FileColumns(),
            []),
        new(
            "directories",
            [typeof(string), typeof(bool)],
            ["./Directories", false],
            "select * from os.directories('./Directories', false)",
            [
                Column("FullName", typeof(string)),
                Column(
                    "Attributes",
                    typeof(int),
                    typeof(FileAttributes),
                    EnumTypeOrigin.NativeClr,
                    typeof(FileAttributes).FullName!,
                    EnumUnderlyingKind.Int32,
                    true),
                Column("CreationTime", typeof(DateTimeOffset)),
                Column("CreationTimeUtc", typeof(DateTimeOffset)),
                Column("LastAccessTime", typeof(DateTimeOffset)),
                Column("LastAccessTimeUtc", typeof(DateTimeOffset)),
                Column("LastWriteTime", typeof(DateTimeOffset)),
                Column("LastWriteTimeUtc", typeof(DateTimeOffset)),
                Column("Exists", typeof(bool)),
                Column("Extension", typeof(string)),
                Column("Name", typeof(string)),
                Column("Root", typeof(string))
            ],
            ["Parent", "DirectoryInfo"]),
        new(
            "zip",
            [typeof(string)],
            ["./Files.zip"],
            "select * from os.zip('./Files.zip')",
            [
                Column("Name", typeof(string)),
                Column("FullName", typeof(string)),
                Column("CompressedLength", typeof(long)),
                Column("LastWriteTime", typeof(DateTimeOffset)),
                Column("Length", typeof(long)),
                Column("IsDirectory", typeof(bool)),
                Column("Level", typeof(int))
            ],
            []),
        new(
            "processes",
            [],
            [],
            "select * from os.processes()",
            [
                Column("BasePriority", typeof(int)),
                Column("EnableRaisingEvents", typeof(bool)),
                Column("ExitCode", typeof(int)),
                Column("ExitTime", typeof(DateTime)),
                Column("HandleCount", typeof(int)),
                Column("HasExited", typeof(bool)),
                Column("Id", typeof(int)),
                Column("MachineName", typeof(string)),
                Column("MainWindowTitle", typeof(string)),
                Column("PagedMemorySize64", typeof(long)),
                Column("ProcessName", typeof(string)),
                Column("Responding", typeof(bool)),
                Column("StartTime", typeof(DateTime)),
                Column("Directory", typeof(string)),
                Column("FileName", typeof(string))
            ],
            ["Handle", "ProcessorAffinity", "TotalProcessorTime", "UserProcessorTime"]),
        new(
            "dlls",
            [typeof(string), typeof(bool)],
            ["./", false],
            "select * from os.dlls('./', false)",
            [],
            ["FileInfo", "Assembly", "Version"]),
        new(
            "dirscompare",
            [typeof(string), typeof(string)],
            ["./Directories/Directory1", "./Directories/Directory2"],
            "select * from os.dirscompare('./Directories/Directory1', './Directories/Directory2')",
            [
                Column(
                    "State",
                    typeof(int),
                    typeof(State),
                    EnumTypeOrigin.NativeClr,
                    typeof(State).FullName!,
                    EnumUnderlyingKind.Int32,
                    false),
                Column("SourceFileRelative", typeof(string)),
                Column("DestinationFileRelative", typeof(string))
            ],
            ["SourceFile", "DestinationFile", "SourceRoot", "DestinationRoot"]),
        new(
            "cultures",
            [],
            [],
            "select * from os.cultures()",
            [
                Column("Name", typeof(string)),
                Column("EnglishName", typeof(string)),
                Column("DisplayName", typeof(string)),
                Column("NativeName", typeof(string)),
                Column("IsNeutralCulture", typeof(bool)),
                Column("ParentName", typeof(string)),
                Column("LCID", typeof(int)),
                Column("CultureTypes", typeof(string)),
                Column("DecimalSeparator", typeof(string)),
                Column("NumberGroupSeparator", typeof(string)),
                Column("ShortDatePattern", typeof(string)),
                Column("LongDatePattern", typeof(string)),
                Column("ShortTimePattern", typeof(string)),
                Column("LongTimePattern", typeof(string))
            ],
            []),
        new(
            "currentculture",
            [],
            [],
            "select * from os.currentculture()",
            [
                Column("CurrentCulture", typeof(string)),
                Column("CurrentUICulture", typeof(string)),
                Column("DecimalSeparator", typeof(string)),
                Column("NumberGroupSeparator", typeof(string)),
                Column("ShortDatePattern", typeof(string)),
                Column("LongDatePattern", typeof(string)),
                Column("ShortTimePattern", typeof(string)),
                Column("LongTimePattern", typeof(string))
            ],
            []),
        new(
            "encodings",
            [],
            [],
            "select * from os.encodings()",
            [
                Column("Name", typeof(string)),
                Column("WebName", typeof(string)),
                Column("CodePage", typeof(int)),
                Column("EncodingName", typeof(string)),
                Column("BodyName", typeof(string)),
                Column("HeaderName", typeof(string)),
                Column("IsSingleByte", typeof(bool))
            ],
            []),
        new(
            "timezones",
            [],
            [],
            "select * from os.timezones()",
            [
                Column("Id", typeof(string)),
                Column("DisplayName", typeof(string)),
                Column("StandardName", typeof(string)),
                Column("DaylightName", typeof(string)),
                Column("SupportsDaylightSavingTime", typeof(bool))
            ],
            ["BaseUtcOffset"]),
        new(
            "runtime",
            [],
            [],
            "select * from os.runtime()",
            [
                Column("DotNetVersion", typeof(string)),
                Column("FrameworkDescription", typeof(string)),
                Column("OSDescription", typeof(string)),
                Column("OSArchitecture", typeof(string)),
                Column("ProcessArchitecture", typeof(string)),
                Column("Is64BitOperatingSystem", typeof(bool)),
                Column("Is64BitProcess", typeof(bool)),
                Column("ProcessorCount", typeof(int)),
                Column("CurrentDirectory", typeof(string))
            ],
            []),
        new(
            "drives",
            [],
            [],
            "select * from os.drives()",
            [
                Column("Name", typeof(string)),
                Column("DriveType", typeof(string)),
                Column("DriveFormat", typeof(string)),
                Column("IsReady", typeof(bool)),
                Column("AvailableFreeSpace", typeof(long?)),
                Column("TotalFreeSpace", typeof(long?)),
                Column("TotalSize", typeof(long?)),
                Column("RootDirectory", typeof(string))
            ],
            []),
        new(
            "specialfolders",
            [],
            [],
            "select * from os.specialfolders()",
            [Column("Name", typeof(string)), Column("Path", typeof(string)), Column("Exists", typeof(bool))],
            []),
        new(
            "fileattributes",
            [],
            [],
            "select * from os.fileattributes()",
            [Column("Name", typeof(string)), Column("Value", typeof(int))],
            []),
        new(
            "environmentvariables",
            [],
            [],
            "select * from os.environmentvariables()",
            [Column("Name", typeof(string)), Column("Target", typeof(string))],
            []),
        new(
            "pathinfo",
            [typeof(string)],
            ["./Files/File1.txt"],
            "select * from os.pathinfo('./Files/File1.txt')",
            [
                Column("InputPath", typeof(string)),
                Column("FullPath", typeof(string)),
                Column("Exists", typeof(bool)),
                Column("IsFile", typeof(bool)),
                Column("IsDirectory", typeof(bool)),
                Column("Root", typeof(string)),
                Column("DirectoryName", typeof(string)),
                Column("FileName", typeof(string)),
                Column("Extension", typeof(string))
            ],
            []),
        new(
            "metadata",
            [typeof(string)],
            ["./Files/File1.txt"],
            "select * from os.metadata('./Files/File1.txt')",
            MetadataColumns(),
            []),
        new(
            "metadata",
            [typeof(string), typeof(bool)],
            ["./Files/File1.txt", true],
            "select * from os.metadata('./Files/File1.txt', true)",
            MetadataColumns(),
            []),
        new(
            "metadata",
            [typeof(string), typeof(bool), typeof(bool)],
            ["./Files", false, true],
            "select * from os.metadata('./Files', false, true)",
            MetadataColumns(),
            [])
    ];

    [TestMethod]
    public void EveryOsConstructor_HasOneExactStarContract()
    {
        var schema = new OsSchema();
        var context = CreateMetadataContext();

        StarContractAssertions.AssertConstructors(schema.GetRawConstructors(context), Cases);

        foreach (var contract in Cases)
        {
            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);

            var result = Compile(contract.Query).Run();
            StarContractAssertions.AssertResult(result, contract);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new OsSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "os-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static IReadOnlyList<StarContractColumn> FileColumns()
    {
        return
        [
            Column("Name", typeof(string)),
            Column("FileName", typeof(string)),
            Column("CreationTime", typeof(DateTimeOffset)),
            Column("CreationTimeUtc", typeof(DateTimeOffset)),
            Column("LastAccessTime", typeof(DateTimeOffset)),
            Column("LastAccessTimeUtc", typeof(DateTimeOffset)),
            Column("LastWriteTime", typeof(DateTimeOffset)),
            Column("LastWriteTimeUtc", typeof(DateTimeOffset)),
            Column("DirectoryName", typeof(string)),
            Column("DirectoryPath", typeof(string)),
            Column("Extension", typeof(string)),
            Column("FullPath", typeof(string)),
            Column("Exists", typeof(bool)),
            Column("IsReadOnly", typeof(bool)),
            Column("Length", typeof(long))
        ];
    }

    private static IReadOnlyList<StarContractColumn> MetadataColumns()
    {
        return
        [
            Column("FullName", typeof(string)),
            Column("DirectoryName", typeof(string)),
            Column("TagName", typeof(string)),
            Column("Description", typeof(string))
        ];
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);

    private static StarContractColumn Column(
        string name,
        Type type,
        Type schemaSourceReadType,
        EnumTypeOrigin enumOrigin,
        string enumDisplayName,
        EnumUnderlyingKind enumUnderlyingKind,
        bool enumIsFlags) =>
        new(
            name,
            type,
            schemaSourceReadType,
            enumOrigin,
            enumDisplayName,
            enumUnderlyingKind,
            enumIsFlags);
}

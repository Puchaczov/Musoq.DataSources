using System.Globalization;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Roslyn.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Parser.Helpers;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetPackageTests
{
    [TestMethod]
    public void WhenNugetPackagesQueried_ShouldPass()
    {
        var query = @"
        select
            p.Name,
            np.Id,
            np.Version,
            np.LicenseUrl,
            np.ProjectUrl,
            np.Title,
            np.Authors,
            np.Owners,
            np.RequireLicenseAcceptance,
            np.Description,
            np.Summary,
            np.ReleaseNotes,
            np.Copyright,
            np.Language,
            np.Tags
        from #csharp.solution('{Solution1SolutionPath}') s
        cross apply s.Projects p
        cross apply p.NugetPackages np"
        .Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(8, result.Count,
            $"Expected 8 entries but got {result.Count}. Entries:\n{DumpResult(result)}");

        var expectedPackages = new[]
        {
            new NugetPackageExpectation
            {
                ProjectName = "Solution1.ClassLibrary1",
                Id = "Musoq.Converter",
                Version = "3.5.0",
                LicenseUrl = "https://github.com/Puchaczov/Musoq/blob/master/LICENSE",
                ProjectUrl = "https://github.com/Puchaczov/Musoq",
                Title = null,
                Authors = "Jakub Puchała",
                Owners = null,
                RequireLicenseAcceptance = false,
                Description = "Package Description",
                Summary = null,
                ReleaseNotes = null,
                Copyright = null,
                Language = null,
                Tags = "sql, dotnet-core"
            },
            new NugetPackageExpectation
            {
                ProjectName = "Solution1.ClassLibrary1",
                Id = "Musoq.Evaluator",
                Version = "7.7.0",
                LicenseUrl = "https://github.com/Puchaczov/Musoq/blob/master/LICENSE",
                ProjectUrl = "https://github.com/Puchaczov/Musoq",
                Title = null,
                Authors = "Jakub Puchała",
                Owners = null,
                RequireLicenseAcceptance = false,
                Description = "Package Description",
                Summary = null,
                ReleaseNotes = null,
                Copyright = null,
                Language = null,
                Tags = "sql, dotnet-core"
            },
            new NugetPackageExpectation
            {
                ProjectName = "Solution1.ClassLibrary1",
                Id = "Musoq.Schema",
                Version = "7.3.5",
                LicenseUrl = "https://github.com/Puchaczov/Musoq/blob/master/LICENSE",
                ProjectUrl = "https://github.com/Puchaczov/Musoq",
                Title = null,
                Authors = "Jakub Puchała",
                Owners = null,
                RequireLicenseAcceptance = false,
                Description = "Package Description",
                Summary = null,
                ReleaseNotes = null,
                Copyright = null,
                Language = null,
                Tags = "sql, dotnet-core"
            },
            new NugetPackageExpectation
            {
                ProjectName = "Solution1.ClassLibrary1.Tests",
                Id = "coverlet.collector",
                Version = "6.0.0",
                LicenseUrl = null,
                ProjectUrl = null,
                Title = null,
                Authors = null,
                Owners = null,
                RequireLicenseAcceptance = null,
                Description = null,
                Summary = null,
                ReleaseNotes = null,
                Copyright = null,
                Language = null,
                Tags = null
            },
            new NugetPackageExpectation()
            {
                ProjectName = "Solution1.ClassLibrary1.Tests",
                Id = "Microsoft.NET.Test.Sdk",
                Version = "17.8.0",
                LicenseUrl = "https://aka.ms/deprecateLicenseUrl",
                ProjectUrl = "https://github.com/microsoft/vstest",
                Title = null,
                Authors = "Microsoft",
                Owners = null,
                RequireLicenseAcceptance = true,
                Description = "The MSbuild targets and properties for building .NET test projects.",
                Summary = null,
                ReleaseNotes = null,
                Copyright = "© Microsoft Corporation. All rights reserved.",
                Language = null,
                Tags = "vstest visual-studio unittest testplatform mstest microsoft test testing"
            },
            new NugetPackageExpectation()
            {
                ProjectName = "Solution1.ClassLibrary1.Tests",
                Id = "NUnit",
                Version = "3.14.0",
                LicenseUrl = "https://aka.ms/deprecateLicenseUrl",
                ProjectUrl = "https://nunit.org/",
                Title = "NUnit",
                Authors = "Charlie Poole, Rob Prouse",
                Owners = "Charlie Poole, Rob Prouse",
                RequireLicenseAcceptance = null,
                Description = """
                              NUnit features a fluent assert syntax, parameterized, generic and theory tests and is user-extensible.
                              
                              This package includes the NUnit 3 framework assembly, which is referenced by your tests. You will need to install version 3 of the nunit3-console program or a third-party runner that supports NUnit 3 in order to execute tests. Runners intended for use with NUnit 2.x will not run NUnit 3 tests correctly.
                              
                              Supported platforms:
                              - .NET Framework 3.5+
                              - .NET Standard 2.0+
                              """,
                Summary = """
                NUnit is a unit-testing framework for all .NET languages with a strong TDD focus.
                """,
                ReleaseNotes = "This package includes the NUnit 3 framework assembly, which is referenced by your tests. You will need to install version 3 of the nunit3-console program or a third-party runner that supports NUnit 3 in order to execute tests. Runners intended for use with NUnit 2.x will not run NUnit 3 tests correctly.",
                Copyright = "Copyright (c) 2023 Charlie Poole, Rob Prouse",
                Language = "en-US",
                Tags = "nunit test testing tdd framework fluent assert theory plugin addin"
            },
            new NugetPackageExpectation()
            {
                ProjectName = "Solution1.ClassLibrary1.Tests",
                Id = "NUnit.Analyzers",
                Version = "3.9.0",
                LicenseUrl = null,
                ProjectUrl = null,
                Title = null,
                Authors = null,
                Owners = null,
                RequireLicenseAcceptance = null,
                Description = null,
                Summary = null,
                ReleaseNotes = null,
                Copyright = null,
                Language = null,
                Tags = null
            },
            new NugetPackageExpectation()
            {
                ProjectName = "Solution1.ClassLibrary1.Tests",
                Id = "NUnit3TestAdapter",
                Version = "4.5.0",
                LicenseUrl = null,
                ProjectUrl = null,
                Title = null,
                Authors = null,
                Owners = null,
                RequireLicenseAcceptance = null,
                Description = null,
                Summary = null,
                ReleaseNotes = null,
                Copyright = null,
                Language = null,
                Tags = null
            }
        };

        foreach (var expectedPkg in expectedPackages)
        {
            AssertHasNugetPackage(result, expectedPkg);
        }
    }

    private class NugetPackageExpectation
    {
        public string ProjectName { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public string? LicenseUrl { get; set; }
        public string? ProjectUrl { get; set; }
        public string? Title { get; set; }
        public string? Authors { get; set; }
        public string? Owners { get; set; }
        public bool? RequireLicenseAcceptance { get; set; }
        public string? Description { get; set; }
        public string? Summary { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? Copyright { get; set; }
        public string? Language { get; set; }
        public string? Tags { get; set; }
    }

    private static void AssertHasNugetPackage(
        Table table,
        NugetPackageExpectation expectedPkg)
    {
        var incorrectRow = table.FirstOrDefault(r =>
            r[1]?.ToString() == expectedPkg.Id && (
            r[0]?.ToString() != expectedPkg.ProjectName ||
            r[2]?.ToString() != expectedPkg.Version ||
            r[3]?.ToString() != expectedPkg.LicenseUrl ||
            r[4]?.ToString() != expectedPkg.ProjectUrl ||
            r[5]?.ToString() != expectedPkg.Title ||
            r[6]?.ToString() != expectedPkg.Authors ||
            r[7]?.ToString() != expectedPkg.Owners ||
            (expectedPkg.RequireLicenseAcceptance != null && r[8]?.ToString() != expectedPkg.RequireLicenseAcceptance.ToString()) ||
            r[9]?.ToString() != expectedPkg.Description ||
            r[10]?.ToString() != expectedPkg.Summary ||
            r[11]?.ToString() != expectedPkg.ReleaseNotes ||
            r[12]?.ToString() != expectedPkg.Copyright ||
            r[13]?.ToString() != expectedPkg.Language ||
            r[14]?.ToString() != expectedPkg.Tags));

        if (incorrectRow == null)
            return;

        var failTable = new Table(table.Name, table.Columns.ToArray()) {incorrectRow};

        Assert.Fail(
            $"Invalid package '{expectedPkg.Id}' for '{expectedPkg.ProjectName}'.\n" +
            $"Differences: {DumpDifferences(expectedPkg, failTable)}");
    }

    private static string DumpDifferences(NugetPackageExpectation expectedPkg, Table table)
    {
        var differences = new StringBuilder();
        
        var row = table[0];

        if (row[0]?.ToString() != expectedPkg.ProjectName)
            differences.AppendLine($"  ProjectName: Expected = {expectedPkg.ProjectName}, Actual = {row[0]}");
        if (row[2]?.ToString() != expectedPkg.Version)
            differences.AppendLine($"  Version: Expected = {expectedPkg.Version}, Actual = {row[2]}");
        if (row[3]?.ToString() != expectedPkg.LicenseUrl)
            differences.AppendLine($"  LicenseUrl: Expected = {expectedPkg.LicenseUrl}, Actual = {row[3]}");
        if (row[4]?.ToString() != expectedPkg.ProjectUrl)
            differences.AppendLine($"  ProjectUrl: Expected = {expectedPkg.ProjectUrl}, Actual = {row[4]}");
        if (row[5]?.ToString() != expectedPkg.Title)
            differences.AppendLine($"  Title: Expected = {expectedPkg.Title}, Actual = {row[5]}");
        if (row[6]?.ToString() != expectedPkg.Authors)
            differences.AppendLine($"  Authors: Expected = {expectedPkg.Authors}, Actual = {row[6]}");
        if (row[7]?.ToString() != expectedPkg.Owners)
            differences.AppendLine($"  Owners: Expected = {expectedPkg.Owners}, Actual = {row[7]}");
        if (expectedPkg.RequireLicenseAcceptance != null && row[8]?.ToString() != expectedPkg.RequireLicenseAcceptance.ToString())
            differences.AppendLine($"  RequireLicenseAcceptance: Expected = {expectedPkg.RequireLicenseAcceptance}, Actual = {row[8]}");
        if (row[9]?.ToString() != expectedPkg.Description)
            differences.AppendLine($"  Description: Expected = {expectedPkg.Description}, Actual = {row[9]}");
        if (row[10]?.ToString() != expectedPkg.Summary)
            differences.AppendLine($"  Summary: Expected = {expectedPkg.Summary}, Actual = {row[10]}");
        if (row[11]?.ToString() != expectedPkg.ReleaseNotes)
            differences.AppendLine($"  ReleaseNotes: Expected = {expectedPkg.ReleaseNotes}, Actual = {row[11]}");
        if (row[12]?.ToString() != expectedPkg.Copyright)
            differences.AppendLine($"  Copyright: Expected = {expectedPkg.Copyright}, Actual = {row[12]}");
        if (row[13]?.ToString() != expectedPkg.Language)
            differences.AppendLine($"  Language: Expected = {expectedPkg.Language}, Actual = {row[13]}");
        if (row[14]?.ToString() != expectedPkg.Tags)
            differences.AppendLine($"  Tags: Expected = {expectedPkg.Tags}, Actual = {row[14]}");

        return differences.ToString();
    }

    private static string DumpResult(Table rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.AppendLine(string.Join(" | ", row.Values.Select(x => x?.ToString() ?? "<null>")));
        return sb.ToString();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new RoslynSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string Solution1SolutionPath => Path.Combine(StartDirectory, "TestsSolutions", "Solution1", "Solution1.sln");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(RoslynToSqlTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    static NugetPackageTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }
}

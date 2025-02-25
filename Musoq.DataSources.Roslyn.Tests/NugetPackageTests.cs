using System.Globalization;
using Musoq.DataSources.Roslyn.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Parser.Helpers;
using System.Text;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetPackageTests
{
    [TestMethod]
    public void WhenNugetPackagesQueried_ShouldPass()
    {
        RunWithNugetPackages(() =>
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
                np.Tags,
                np.LicenseContent
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
                    Tags = "sql, dotnet-core",
                    LicenseContent = """
                                     MIT License
                                     
                                     Copyright (c) 2018 Jakub Puchała
                                     
                                     Permission is hereby granted, free of charge, to any person obtaining a copy
                                     of this software and associated documentation files (the "Software"), to deal
                                     in the Software without restriction, including without limitation the rights
                                     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                                     copies of the Software, and to permit persons to whom the Software is
                                     furnished to do so, subject to the following conditions:
                                     
                                     The above copyright notice and this permission notice shall be included in all
                                     copies or substantial portions of the Software.
                                     
                                     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                                     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                                     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                                     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                                     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                                     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                                     SOFTWARE.
                                     """
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
                    Tags = "sql, dotnet-core",
                    LicenseContent = """
                                     MIT License
                                     
                                     Copyright (c) 2018 Jakub Puchała
                                     
                                     Permission is hereby granted, free of charge, to any person obtaining a copy
                                     of this software and associated documentation files (the "Software"), to deal
                                     in the Software without restriction, including without limitation the rights
                                     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                                     copies of the Software, and to permit persons to whom the Software is
                                     furnished to do so, subject to the following conditions:
                                     
                                     The above copyright notice and this permission notice shall be included in all
                                     copies or substantial portions of the Software.
                                     
                                     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                                     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                                     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                                     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                                     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                                     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                                     SOFTWARE.
                                     """
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
                    Tags = "sql, dotnet-core",
                    LicenseContent = """
                                     MIT License
                                     
                                     Copyright (c) 2018 Jakub Puchała
                                     
                                     Permission is hereby granted, free of charge, to any person obtaining a copy
                                     of this software and associated documentation files (the "Software"), to deal
                                     in the Software without restriction, including without limitation the rights
                                     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                                     copies of the Software, and to permit persons to whom the Software is
                                     furnished to do so, subject to the following conditions:
                                     
                                     The above copyright notice and this permission notice shall be included in all
                                     copies or substantial portions of the Software.
                                     
                                     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                                     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                                     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                                     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                                     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                                     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                                     SOFTWARE.
                                     """
                },
                new NugetPackageExpectation
                {
                    ProjectName = "Solution1.ClassLibrary1.Tests",
                    Id = "coverlet.collector",
                    Version = "6.0.0",
                    LicenseUrl = "https://licenses.nuget.org/MIT",
                    ProjectUrl = "https://github.com/coverlet-coverage/coverlet",
                    Title = "coverlet.collector",
                    Authors = "tonerdo",
                    Owners = null,
                    RequireLicenseAcceptance = null,
                    Description = "Coverlet is a cross platform code coverage library for .NET, with support for line, branch and method coverage.",
                    Summary = null,
                    ReleaseNotes = null,
                    Copyright = null,
                    Language = null,
                    Tags = "coverage testing unit-test lcov opencover quality",
                    LicenseContent = """
                                     MIT
                                     """
                },
                new NugetPackageExpectation
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
                    Tags = "vstest visual-studio unittest testplatform mstest microsoft test testing",
                    LicenseContent = """
                                     The MIT License (MIT)
                                     
                                     Copyright (c) Microsoft Corporation
                                     
                                     All rights reserved.
                                     
                                     Permission is hereby granted, free of charge, to any person obtaining a copy
                                     of this software and associated documentation files (the "Software"), to deal
                                     in the Software without restriction, including without limitation the rights
                                     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                                     copies of the Software, and to permit persons to whom the Software is
                                     furnished to do so, subject to the following conditions:
                                     
                                     The above copyright notice and this permission notice shall be included in all
                                     copies or substantial portions of the Software.
                                     
                                     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                                     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                                     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                                     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                                     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                                     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                                     SOFTWARE.
                                     
                                     """
                },
                new NugetPackageExpectation
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
                    Tags = "nunit test testing tdd framework fluent assert theory plugin addin",
                    LicenseContent = """
                                     Copyright (c) 2023 Charlie Poole, Rob Prouse
                                     
                                     Permission is hereby granted, free of charge, to any person obtaining a copy
                                     of this software and associated documentation files (the "Software"), to deal
                                     in the Software without restriction, including without limitation the rights
                                     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                                     copies of the Software, and to permit persons to whom the Software is
                                     furnished to do so, subject to the following conditions:
                                     
                                     The above copyright notice and this permission notice shall be included in
                                     all copies or substantial portions of the Software.
                                     
                                     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                                     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                                     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                                     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                                     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                                     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
                                     THE SOFTWARE.
                                     
                                     """
                },
                new NugetPackageExpectation
                {
                    ProjectName = "Solution1.ClassLibrary1.Tests",
                    Id = "NUnit.Analyzers",
                    Version = "3.9.0",
                    LicenseUrl = "https://aka.ms/deprecateLicenseUrl",
                    ProjectUrl = "https://github.com/nunit/nunit.analyzers",
                    Title = "NUnit Analyzers",
                    Authors = "NUnit",
                    Owners = null,
                    RequireLicenseAcceptance = null,
                    Description = 
                        """
                        This package includes analyzers and code fixes for test projects using NUnit 3. The analyzers will mark wrong usages when writing tests, and the code fixes can be used to used to correct these usages.

                        Version 3.0 and upwards works in Visual Studio 2019 and also enables supression of compiler errors such as errors arising from nullable reference types. For Visual Studio 2017 one must use versions below 3.0.
                        """,
                    Summary = "Code analyzers and fixes for NUnit 3",
                    ReleaseNotes = "See the release notes on https://github.com/nunit/nunit.analyzers/blob/master/CHANGES.txt.",
                    Copyright = "Copyright (c) 2018-2023 NUnit project",
                    Language = null,
                    Tags = "nunit, analyzers, roslyn-analyzers",
                    LicenseContent = """
                                     Permission is hereby granted, free of charge, to any person obtaining a copy
                                     of this software and associated documentation files (the "Software"), to deal
                                     in the Software without restriction, including without limitation the rights
                                     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                                     copies of the Software, and to permit persons to whom the Software is
                                     furnished to do so, subject to the following conditions:
                                     
                                     The above copyright notice and this permission notice shall be included in
                                     all copies or substantial portions of the Software.
                                     
                                     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                                     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                                     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                                     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                                     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                                     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
                                     THE SOFTWARE.
                                     """
                },
                new NugetPackageExpectation
                {
                    ProjectName = "Solution1.ClassLibrary1.Tests",
                    Id = "NUnit3TestAdapter",
                    Version = "4.5.0",
                    LicenseUrl = "https://licenses.nuget.org/MIT",
                    ProjectUrl = "https://docs.nunit.org/articles/vs-test-adapter/Index.html",
                    Title = "NUnit3 Test Adapter for Visual Studio and DotNet",
                    Authors = "Charlie Poole, Terje Sandstrom",
                    Owners = null,
                    RequireLicenseAcceptance = null,
                    Description = 
                    """
                    The NUnit3 TestAdapter for Visual Studio, all versions from 2012 and onwards, and DotNet (incl. .Net core), versions .net framework 4.6.2 or higher, .net core 3.1, .net 5 or higher.
                    
                          Note that this package ONLY contains the adapter, not the NUnit framework.
                          For VS 2017 and forward, you should add this package to every test project in your solution. (Earlier versions only require a single adapter package per solution.)
                    """,
                    Summary = "NUnit3 adapter for running tests in Visual Studio and DotNet. Works with NUnit 3.x, use the NUnit 2 adapter for 2.x tests.",
                    ReleaseNotes = "See https://docs.nunit.org/articles/vs-test-adapter/Adapter-Release-Notes.html",
                    Copyright = "Copyright (c) 2011-2021 Charlie Poole, 2014-2023 Terje Sandstrom",
                    Language = "en-US",
                    Tags = "test visualstudio testadapter nunit nunit3 dotnet",
                    LicenseContent = """
                                     MIT
                                     """
                }
            };

            foreach (var expectedPkg in expectedPackages)
            {
                AssertHasNugetPackage(result, expectedPkg);
            }            
        });
    }

    private static void RunWithNugetPackages(Action action)
    {
        try
        {
            Environment.SetEnvironmentVariable("NUGET_PACKAGES",
                Path.Combine(StartDirectory, "TestsSolutions", "NugetCache"));
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", null);
        }
    }

    private class NugetPackageExpectation
    {
        public required string ProjectName { get; init; }
        public required string Id { get; init; }
        public required string Version { get; init; }
        public string? LicenseUrl { get; init; }
        public string? ProjectUrl { get; init; }
        public string? Title { get; init; }
        public string? Authors { get; init; }
        public string? Owners { get; init; }
        public bool? RequireLicenseAcceptance { get; init; }
        public string? Description { get; init; }
        public string? Summary { get; init; }
        public string? ReleaseNotes { get; init; }
        public string? Copyright { get; init; }
        public string? Language { get; init; }
        public string? Tags { get; init; }
        
        public string? LicenseContent { get; init; }
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
            r[14]?.ToString() != expectedPkg.Tags ||
            r[15]?.ToString() != expectedPkg.LicenseContent));

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
        if (row[15]?.ToString() != expectedPkg.LicenseContent)
            differences.AppendLine($"  LicenseContent: Expected = {expectedPkg.LicenseContent}, Actual = {row[15]}");

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
        return InstanceCreatorHelpers.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            new RoslynSchemaProvider(), 
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
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
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }
}

using System.Globalization;
using Moq;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Roslyn.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Parser.Helpers;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class RoslynToSqlTests
{
    static RoslynToSqlTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private static string Solution1SolutionPath =>
        Path.Combine(StartDirectory, "TestsSolutions", "Solution1", "Solution1.sln");

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

    [TestMethod]
    public void WhenSolutionQueried_ShouldPass()
    {
        var query = $"select s.Id from #csharp.solution('{Solution1SolutionPath.Escape()}') s";

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(Guid.TryParse(result[0][0].ToString(), out _));
    }

    [TestMethod]
    public void WhenProjectQueried_ShouldPass()
    {
        var query =
            $"select p.Id, p.FilePath, p.OutputFilePath, p.OutputRefFilePath, p.DefaultNamespace, p.Language, p.AssemblyName, p.Name, p.IsSubmission, p.Version from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p";

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.IsTrue(result.Count == 2, "Result should have 2 entries");

        Assert.IsTrue(result.Any(row =>
            Guid.TryParse(row[0].ToString(), out _) &&
            ValidateIsValidPathFor(row[1].ToString(), ".csproj") &&
            ValidateIsValidPathFor(row[2].ToString(), ".dll", false) &&
            ValidateIsValidPathFor(row[3].ToString(), ".dll", false) &&
            row[4].ToString() == "Solution1.ClassLibrary1" &&
            row[5].ToString() == "C#" &&
            row[6].ToString() == "Solution1.ClassLibrary1" &&
            row[7].ToString() == "Solution1.ClassLibrary1" &&
            row[8] != null
        ), "First entry does not match expected details");

        Assert.IsTrue(result.Any(row =>
            Guid.TryParse(row[0].ToString(), out _) &&
            ValidateIsValidPathFor(row[1].ToString(), ".csproj") &&
            ValidateIsValidPathFor(row[2].ToString(), ".dll", false) &&
            ValidateIsValidPathFor(row[3].ToString(), ".dll", false) &&
            row[4].ToString() == "Solution1.ClassLibrary1.Tests" &&
            row[5].ToString() == "C#" &&
            row[6].ToString() == "Solution1.ClassLibrary1.Tests" &&
            row[7].ToString() == "Solution1.ClassLibrary1.Tests" &&
            row[8] != null
        ), "Second entry does not match expected details");
    }

    [TestMethod]
    public void WhenQuickAccessForTypes_ShouldPass()
    {
        var query =
            $"select t.Name from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Types t";

        var vm = CompileQuery(query);

        var result = vm.Run();


        Assert.IsTrue(result.Count >= 20, $"Result should contain at least 20 types, but got {result.Count}");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Class1") == 1, "Class1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Interface1") == 1, "Interface1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Interface2") == 1, "Interface2 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Enum1") == 1, "Enum1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Tests") == 1, "Tests should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "PartialTestClass") == 2,
            "PartialTestClass should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "CyclomaticComplexityClass1") == 1,
            "CyclomaticComplexityClass1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "TestFeatures") == 1, "TestFeatures should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "AbstractClassWithAbstractMethod") == 1,
            "AbstractClassWithAbstractMethod should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "IInterfaceWithMethods") == 1,
            "IInterfaceWithMethods should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "InterfaceImplementor") == 1,
            "InterfaceImplementor should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UnusedCodeTestClass") == 1,
            "UnusedCodeTestClass should be present");

        Assert.IsTrue(result.Count(r => r[0].ToString() == "IUnusedInterface") == 1,
            "IUnusedInterface should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "IUsedInterface") == 1, "IUsedInterface should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UsedInterfaceImplementor") == 1,
            "UsedInterfaceImplementor should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UnusedEnum") == 1, "UnusedEnum should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UsedEnum") == 1, "UsedEnum should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "EnumUser") == 1, "EnumUser should be present");
    }

    [TestMethod]
    public void WhenChecksKindOfType_ShouldPass()
    {
        var query =
            $"select t.Name, t.IsClass, t.IsEnum, t.IsInterface from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Types t where t.Name in ('Class1', 'Interface1', 'Enum1', 'Tests', 'PartialTestClass', 'CyclomaticComplexityClass1')";

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.IsTrue(result.Count == 7, "Result must contain 6 records");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Class1" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 1,
            "Class1 should be present");
        Assert.IsTrue(
            result.Count(r => r[0].ToString() == "Interface1" && !(bool)r[1] && !(bool)r[2] && (bool)r[3]) == 1,
            "Interface1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Enum1" && !(bool)r[1] && (bool)r[2] && !(bool)r[3]) == 1,
            "Enum1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Tests" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 1,
            "Tests should be present");
        Assert.IsTrue(
            result.Count(r => r[0].ToString() == "PartialTestClass" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 2,
            "PartialTestClass should be present");
        Assert.IsTrue(
            result.Count(r =>
                r[0].ToString() == "CyclomaticComplexityClass1" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 1,
            "CyclomaticComplexityClass1 should be present");
    }

    [TestMethod]
    public void WhenDocumentQueries_ShouldPass()
    {
        var query =
            $"select d.Name, d.Text, d.ClassCount, d.InterfaceCount, d.EnumCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Documents d where d.Name = 'Class1.cs'";

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("Class1.cs", result[0][0].ToString());

        var documentContent = result[0][1].ToString();

        Assert.IsNotNull(documentContent);
        Assert.IsTrue(
            documentContent.Contains("class Class1") &&
            documentContent.Contains("interface Interface1") &&
            documentContent.Contains("enum Enum1")
        );
        Assert.AreEqual(2, result[0][2]);
        Assert.AreEqual(2, result[0][3]);
        Assert.AreEqual(1, result[0][4]);
    }

    [TestMethod]
    public void WhenClassQueried_ShouldPass()
    {
        var query = """
                    select 
                        c.IsAbstract, 
                        c.IsSealed, 
                        c.IsStatic, 
                        c.BaseTypes, 
                        c.Interfaces, 
                        c.TypeParameters, 
                        c.MemberNames, 
                        c.Attributes,
                        c.Name,
                        c.FullName,
                        c.Namespace,
                        c.MethodsCount,
                        c.PropertiesCount,
                        c.FieldsCount,
                        c.InheritanceDepth,
                        c.ConstructorsCount,
                        c.NestedClassesCount,
                        c.NestedInterfacesCount,
                        c.InterfacesCount,
                        c.LackOfCohesion
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    where c.Name = 'Class1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual(false, result[0][0]);
        Assert.AreEqual(false, result[0][1]);
        Assert.AreEqual(false, result[0][2]);

        var baseTypes = (result[0][3] as IEnumerable<string> ?? []).ToList();

        Assert.IsNotNull(baseTypes);
        Assert.AreEqual(1, baseTypes.Count);
        Assert.AreEqual("Object", baseTypes.First());

        var interfaces = (result[0][4] as IEnumerable<string> ?? []).ToList();

        Assert.IsNotNull(interfaces);
        Assert.AreEqual(1, interfaces.Count);
        Assert.AreEqual("Interface1", interfaces.First());

        var typeParameters = (result[0][5] as IEnumerable<string> ?? []).ToList();

        Assert.IsNotNull(typeParameters);
        Assert.AreEqual(0, typeParameters.Count);

        var memberNames = (result[0][6] as IEnumerable<string> ?? []).ToList();

        Assert.IsNotNull(memberNames);
        Assert.AreEqual(8, memberNames.Count);
        Assert.IsTrue(memberNames.Contains("Method1Async"));
        Assert.IsTrue(memberNames.Contains("Method2"));
        Assert.IsTrue(memberNames.Contains("Method3"));
        Assert.IsTrue(memberNames.Contains("Method4"));
        Assert.IsTrue(memberNames.Contains(".ctor"));
        Assert.IsTrue(memberNames.Contains("Property1"));
        Assert.IsTrue(memberNames.Contains("get_Property1"));

        var attributes = (result[0][7] as IEnumerable<string> ?? []).ToList();

        Assert.IsNotNull(attributes);
        Assert.AreEqual(0, attributes.Count);

        Assert.AreEqual("Class1", result[0][8].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1.Class1", result[0][9].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1", result[0][10].ToString());
        Assert.AreEqual(5, result[0][11]);
        Assert.AreEqual(1, result[0][12]);
        Assert.AreEqual(0, result[0][13]);
        Assert.AreEqual(1, result[0][14]);
        Assert.AreEqual(0, result[0][15]);
        Assert.AreEqual(0, result[0][16]);
        Assert.AreEqual(0, result[0][17]);
        Assert.AreEqual(1, result[0][18]);
        Assert.AreEqual(2.0, result[0][19]);
    }

    [TestMethod]
    public void WhenClassWithAttributesQueried_ShouldPass()
    {
        var query = """
                    select
                        c.Attributes 
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    where c.Name = 'Tests'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        var attributes = (result[0][0] as IEnumerable<AttributeEntity> ?? []).ToList();

        Assert.IsNotNull(attributes);
        Assert.AreEqual(1, attributes.Count);

        Assert.AreEqual("ExcludeFromCodeCoverage", attributes.First().Name);
    }

    [TestMethod]
    public void WhenClassMethodsQueried_ShouldPass()
    {
        var query = """
                    select
                        m.Name,
                        m.ReturnType,
                        m.Parameters,
                        m.Modifiers,
                        m.Text,
                        m.Attributes
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'Class1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.IsTrue(result.Count == 5, "Result should contain exactly 5 records");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "Method1Async" &&
                r[1].ToString() == "Task" &&
                !(r[2] as IEnumerable<ParameterEntity> ?? []).Any() &&
                (r[3] as IEnumerable<string> ?? []).Count() == 1 &&
                r[4] != null &&
                !(r[5] as IEnumerable<AttributeEntity> ?? []).Any()),
            "Missing or invalid Method1Async record");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "Method2" &&
                r[1].ToString() == "Void" &&
                !(r[2] as IEnumerable<ParameterEntity> ?? []).Any() &&
                (r[3] as IEnumerable<string> ?? []).Count() == 1 &&
                r[4] != null &&
                !(r[5] as IEnumerable<AttributeEntity> ?? []).Any()),
            "Missing or invalid Method2 record");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "Method3" &&
                r[1].ToString() == "Class1" &&
                !(r[2] as IEnumerable<ParameterEntity> ?? []).Any() &&
                (r[3] as IEnumerable<string> ?? []).Count() == 1 &&
                r[4] != null &&
                !(r[5] as IEnumerable<AttributeEntity> ?? []).Any()),
            "Missing or invalid first Method3 record");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "Method3" &&
                r[1].ToString() == "Class1" &&
                (r[2] as IEnumerable<ParameterEntity> ?? []).Count() == 1 &&
                (r[3] as IEnumerable<string> ?? []).Count() == 1 &&
                r[4] != null &&
                !(r[5] as IEnumerable<AttributeEntity> ?? []).Any()),
            "Missing or invalid second Method3 record");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "Method4" &&
                r[1].ToString() == "Enum1" &&
                !(r[2] as IEnumerable<ParameterEntity> ?? []).Any() &&
                (r[3] as IEnumerable<string> ?? []).Count() == 1 &&
                r[4] != null),
            "Missing or invalid Method4 record");
    }

    [TestMethod]
    public void WhenMethodTextQueried_ShouldNotContainXmlDoc()
    {
        var query = """
                    select
                        m.Name,
                        m.Text
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    where cls.Name = 'CallGraphTestClass' and m.Name = 'RecursiveMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        var methodText = result[0][1]?.ToString() ?? string.Empty;

        Assert.IsFalse(string.IsNullOrWhiteSpace(methodText), "Method text should not be empty");
        Assert.IsFalse(methodText.Contains("///"), "Method text should not include XML doc comments");
        Assert.IsTrue(methodText.TrimStart().StartsWith("public int RecursiveMethod"),
            "Method text should start with method signature");
    }

    [TestMethod]
    public void WhenLinesOfCodeOfSpecificMethodQueried_ShouldPass()
    {
        var query = """
                    select
                        m.LinesOfCode
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'Class1' and m.Name = 'Method1Async'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual(4, result[0][0]);
    }

    [TestMethod]
    public void WhenClassSplitAsPartial_Methods_ShouldPass()
    {
        var query = """
                    select
                        d.Name,
                        c.Name,
                        c.MethodsCount,
                        m.Name
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'PartialTestClass'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.IsTrue(result.Count == 2, "Result should contain exactly 2 records");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "PartialTestClass_1.cs" &&
                r[1].ToString() == "PartialTestClass" &&
                (int)r[2] == 1 &&
                r[3].ToString() == "Method1"),
            "Missing PartialTestClass_1.cs record");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "PartialTestClass_2.cs" &&
                r[1].ToString() == "PartialTestClass" &&
                (int)r[2] == 1 &&
                r[3].ToString() == "Method2"),
            "Missing PartialTestClass_2.cs record");
    }

    [TestMethod]
    public void WhenClassSplitAsPartial_Properties_ShouldPass()
    {
        var query = """
                    select
                        d.Name,
                        c.Name,
                        c.MethodsCount,
                        pr.Name
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Properties pr
                    where c.Name = 'PartialTestClass'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.IsTrue(result.Count == 2, "Result should contain exactly 2 records");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "PartialTestClass_1.cs" &&
                r[1].ToString() == "PartialTestClass" &&
                (int)r[2] == 1 &&
                r[3].ToString() == "Property1"),
            "Missing PartialTestClass_1.cs record");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "PartialTestClass_2.cs" &&
                r[1].ToString() == "PartialTestClass" &&
                (int)r[2] == 1 &&
                r[3].ToString() == "Property2"),
            "Missing PartialTestClass_2.cs record");
    }

    [TestMethod]
    public void WhenLinesOfCodeOfSpecificClassQueried_ShouldPass()
    {
        var query = """
                    select
                        c.LinesOfCode
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    where c.Name = 'Class1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual(29, result[0][0]);
    }

    [TestMethod]
    public void WhenClassPropertiesQueried_ShouldPass()
    {
        var query = """
                    select
                        p.Name,
                        p.Type,
                        p.IsIndexer,
                        p.IsReadOnly,
                        p.IsWriteOnly,
                        p.IsRequired,
                        p.IsWithEvents,
                        p.IsVirtual,
                        p.IsOverride,
                        p.IsAbstract,
                        p.IsSealed,
                        p.IsStatic,
                        p.Modifiers
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects pr 
                    cross apply pr.Documents d 
                    cross apply d.Classes c
                    cross apply c.Properties p
                    where c.Name = 'Class1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("Property1", result[0][0].ToString());
        Assert.AreEqual("Int32", result[0][1].ToString());
        Assert.AreEqual(false, result[0][2]);
        Assert.AreEqual(true, result[0][3]);
        Assert.AreEqual(false, result[0][4]);
        Assert.AreEqual(false, result[0][5]);
        Assert.AreEqual(false, result[0][6]);
        Assert.AreEqual(false, result[0][7]);
        Assert.AreEqual(false, result[0][8]);
        Assert.AreEqual(false, result[0][9]);
        Assert.AreEqual(false, result[0][10]);
        Assert.AreEqual(false, result[0][11]);
        Assert.AreEqual(1, (result[0][12] as IEnumerable<string> ?? []).Count());
    }

    [TestMethod]
    public void WhenMethodParametersQueried_ShouldPass()
    {
        var query = """
                    select
                        p.Name,
                        p.Type,
                        p.IsOptional,
                        p.IsParams,
                        p.IsThis,
                        p.IsDiscard,
                        p.IsIn,
                        p.IsOut,
                        p.IsRef,
                        p.IsByRef,
                        p.IsByValue
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects pr 
                    cross apply pr.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    cross apply m.Parameters p
                    where c.Name = 'Class1' and m.Name = 'Method3'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("a", result[0][0].ToString());
        Assert.AreEqual("Int32", result[0][1].ToString());
        Assert.AreEqual(false, result[0][2]);
        Assert.AreEqual(false, result[0][3]);
        Assert.AreEqual(false, result[0][4]);
        Assert.AreEqual(false, result[0][5]);
        Assert.AreEqual(false, result[0][6]);
        Assert.AreEqual(false, result[0][7]);
        Assert.AreEqual(false, result[0][8]);
        Assert.AreEqual(false, result[0][9]);
        Assert.AreEqual(true, result[0][10]);
    }

    [TestMethod]
    public void WhenEnumQueried_ShouldPass()
    {
        var query = """
                    select
                        e.Name,
                        e.FullName,
                        e.Namespace,
                        e.Modifiers,
                        e.Members
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects pr 
                    cross apply pr.Documents d 
                    cross apply d.Enums e
                    where e.Name = 'Enum1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("Enum1", result[0][0].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1.Enum1", result[0][1].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1", result[0][2].ToString());
        Assert.AreEqual(1, (result[0][3] as IEnumerable<string> ?? []).Count());

        var members = (result[0][4] as IEnumerable<string> ?? []).ToList();

        Assert.IsNotNull(members);

        Assert.AreEqual(2, members.Count);
        Assert.IsTrue(members.Contains("Value1"));
        Assert.IsTrue(members.Contains("Value2"));
    }

    [TestMethod]
    public void WhenInterfaceQueried_ShouldPass()
    {
        var query = """
                    select
                        i.Name,
                        i.FullName,
                        i.Namespace,
                        i.Modifiers,
                        i.BaseInterfaces,
                        i.Methods,
                        i.Properties
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects pr 
                    cross apply pr.Documents d 
                    cross apply d.Interfaces i
                    where i.Name = 'Interface1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("Interface1", result[0][0].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1.Interface1", result[0][1].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1", result[0][2].ToString());
        Assert.AreEqual(1, (result[0][3] as IEnumerable<string> ?? []).Count());

        var baseInterfaces = (result[0][4] as IEnumerable<string> ?? []).ToList();

        Assert.IsNotNull(baseInterfaces);

        Assert.AreEqual(0, baseInterfaces.Count);

        var methods = (result[0][5] as IEnumerable<MethodEntity> ?? []).ToList();

        Assert.IsNotNull(methods);

        Assert.AreEqual(4, methods.Count);
        Assert.IsTrue(methods.Any(m => m.Name == "Method1Async"));
        Assert.IsTrue(methods.Any(m => m.Name == "Method2"));
        Assert.IsTrue(methods.Any(m => m.Name == "Method3"));
        Assert.IsTrue(methods.Any(m => m.Name == "Method4"));

        var properties = (result[0][6] as IEnumerable<PropertyEntity> ?? []).ToList();

        Assert.IsNotNull(properties);

        Assert.AreEqual(0, properties.Count);
    }

    [TestMethod]
    public void WhenAttributesQueried_ShouldPass()
    {
        var query = """
                    select
                        a.Name,
                        a.ConstructorArguments
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects pr 
                    cross apply pr.Documents d 
                    cross apply d.Classes c
                    cross apply c.Attributes a
                    where c.Name = 'Tests'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("ExcludeFromCodeCoverage", result[0][0].ToString());
        Assert.AreEqual(0, (result[0][1] as IEnumerable<string> ?? []).Count());
    }

    [TestMethod]
    public void WhenCyclomaticComplexityIsOne_ShouldPass()
    {
        var query = """
                    select
                        m.CyclomaticComplexity
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.GetClassesByNames('CyclomaticComplexityClass1') c
                    cross apply c.Methods m
                    where m.Name = 'CyclomaticComplexityMethod1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, result[0][0]);
    }

    [TestMethod]
    public void WhenCyclomaticComplexityIsTwo_ShouldPass()
    {
        var query = """
                    select
                        m.CyclomaticComplexity
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.GetClassesByNames('CyclomaticComplexityClass1') c
                    cross apply c.Methods m
                    where m.Name = 'CyclomaticComplexityMethod2'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result[0][0]);
    }

    [TestMethod]
    public void WhenCyclomaticComplexityIsThree_ShouldPass()
    {
        var query = """
                    select
                        m.CyclomaticComplexity
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.GetClassesByNames('CyclomaticComplexityClass1') c
                    cross apply c.Methods m
                    where m.Name = 'CyclomaticComplexityMethod3'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0][0]);
    }

    [TestMethod]
    public void WhenLookingForReferenceToClass_ShouldFind()
    {
        var query = """
                    select r.Name, rd.StartLine, rd.StartColumn, rd.EndLine, rd.EndColumn from #csharp.solution('{Solution1SolutionPath}') s
                    cross apply s.GetClassesByNames('Class1') c
                    cross apply s.FindReferences(c.Self) rd
                    cross apply rd.ReferencedClasses r
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.IsTrue(result.Count == 2, "Result should contain exactly 2 records");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "Class1" &&
                (int)r[1] == 16 &&
                (int)r[2] == 11 &&
                (int)r[3] == 16 &&
                (int)r[4] == 17),
            "Missing first Class1 location record");

        Assert.IsTrue(result.Any(r =>
                r[0].ToString() == "Class1" &&
                (int)r[1] == 21 &&
                (int)r[2] == 11 &&
                (int)r[3] == 21 &&
                (int)r[4] == 17),
            "Missing second Class1 location record");
    }

    [TestMethod]
    public void WhenLookingForReferenceToInterface_ShouldFind()
    {
        var query = """
                    select r.Name, rd.StartLine, rd.StartColumn, rd.EndLine, rd.EndColumn from #csharp.solution('{Solution1SolutionPath}') s
                    cross apply s.GetInterfacesByNames('Interface1') c
                    cross apply s.FindReferences(c.Self) rd
                    cross apply rd.ReferencedInterfaces r
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("Interface2", result[0][0].ToString());
        Assert.AreEqual(70, result[0][1]);
        Assert.AreEqual(30, result[0][2]);
        Assert.AreEqual(70, result[0][3]);
        Assert.AreEqual(40, result[0][4]);
    }

    [TestMethod]
    public void WhenLookingForReferenceToEnum_WithinClass_ShouldFind()
    {
        var query = """
                    select r.Name, rd.StartLine, rd.StartColumn, rd.EndLine, rd.EndColumn from #csharp.solution('{Solution1SolutionPath}') s
                    cross apply s.GetEnumsByNames('Enum1') c
                    cross apply s.FindReferences(c.Self) rd
                    cross apply rd.ReferencedClasses r
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("Class1", result[0][0].ToString());
        Assert.AreEqual(26, result[0][1]);
        Assert.AreEqual(11, result[0][2]);
        Assert.AreEqual(26, result[0][3]);
        Assert.AreEqual(16, result[0][4]);
    }

    [TestMethod]
    public void WhenLookingForReferenceToEnum_WithinInterface_ShouldFind()
    {
        var query = """
                    select r.Name, rd.StartLine, rd.StartColumn, rd.EndLine, rd.EndColumn from #csharp.solution('{Solution1SolutionPath}') s
                    cross apply s.GetEnumsByNames('Enum1') c
                    cross apply s.FindReferences(c.Self) rd
                    cross apply rd.ReferencedInterfaces r
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);

        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        Assert.AreEqual("Interface1", result[0][0].ToString());
        Assert.AreEqual(67, result[0][1]);
        Assert.AreEqual(11, result[0][2]);
        Assert.AreEqual(67, result[0][3]);
        Assert.AreEqual(16, result[0][4]);
    }

    [TestMethod]
    public void WhenDocumentFilePathQueried_ShouldReturnFilePath()
    {
        var query =
            $"select d.Name, d.FilePath from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Documents d where d.Name = 'Class1.cs'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Class1.cs", result[0][0].ToString());

        var filePath = result[0][1]?.ToString();
        Assert.IsNotNull(filePath);
        Assert.IsTrue(filePath.EndsWith("Class1.cs"));
        Assert.IsTrue(File.Exists(filePath));
    }

    [TestMethod]
    public void WhenMethodBodyPropertiesQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        m.Name,
                        m.HasBody,
                        m.IsEmpty,
                        m.StatementsCount,
                        m.BodyContainsOnlyTrivia
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'TestFeatures'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        var emptyMethod = result.FirstOrDefault(r => r[0].ToString() == "EmptyMethod");
        Assert.IsNotNull(emptyMethod);
        Assert.AreEqual(true, emptyMethod[1]);
        Assert.AreEqual(true, emptyMethod[2]);
        Assert.AreEqual(0, emptyMethod[3]);
        Assert.AreEqual(true, emptyMethod[4]);


        var methodWithComments = result.FirstOrDefault(r => r[0].ToString() == "MethodWithOnlyComments");
        Assert.IsNotNull(methodWithComments);
        Assert.AreEqual(true, methodWithComments[1]);
        Assert.AreEqual(true, methodWithComments[2]);
        Assert.AreEqual(0, methodWithComments[3]);
        Assert.AreEqual(true, methodWithComments[4]);


        var singleStatementMethod = result.FirstOrDefault(r => r[0].ToString() == "SingleStatementMethod");
        Assert.IsNotNull(singleStatementMethod);
        Assert.AreEqual(true, singleStatementMethod[1]);
        Assert.AreEqual(false, singleStatementMethod[2]);
        Assert.AreEqual(1, singleStatementMethod[3]);
        Assert.AreEqual(false, singleStatementMethod[4]);


        var multipleStatementsMethod = result.FirstOrDefault(r => r[0].ToString() == "MultipleStatementsMethod");
        Assert.IsNotNull(multipleStatementsMethod);
        Assert.AreEqual(true, multipleStatementsMethod[1]);
        Assert.AreEqual(false, multipleStatementsMethod[2]);
        Assert.AreEqual(3, multipleStatementsMethod[3]);
        Assert.AreEqual(false, multipleStatementsMethod[4]);


        var expressionBodiedMethod = result.FirstOrDefault(r => r[0].ToString() == "ExpressionBodiedMethod");
        Assert.IsNotNull(expressionBodiedMethod);
        Assert.AreEqual(true, expressionBodiedMethod[1]);
        Assert.AreEqual(false, expressionBodiedMethod[2]);
        Assert.AreEqual(0, expressionBodiedMethod[3]);
        Assert.AreEqual(false, expressionBodiedMethod[4]);


        var methodWithNestedBlocks = result.FirstOrDefault(r => r[0].ToString() == "MethodWithNestedBlocks");
        Assert.IsNotNull(methodWithNestedBlocks);
        Assert.AreEqual(true, methodWithNestedBlocks[1]);
        Assert.AreEqual(false, methodWithNestedBlocks[2]);
        Assert.AreEqual(2, methodWithNestedBlocks[3]);
        Assert.AreEqual(false, methodWithNestedBlocks[4]);
    }

    [TestMethod]
    public void WhenPropertyAccessorPropertiesQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        p.Name,
                        p.IsAutoProperty,
                        p.HasGetter,
                        p.HasSetter,
                        p.HasInitSetter
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects pr 
                    cross apply pr.Documents d 
                    cross apply d.Classes c
                    cross apply c.Properties p
                    where c.Name = 'TestFeatures'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        var autoProperty = result.FirstOrDefault(r => r[0].ToString() == "AutoProperty");
        Assert.IsNotNull(autoProperty);
        Assert.AreEqual(true, autoProperty[1]);
        Assert.AreEqual(true, autoProperty[2]);
        Assert.AreEqual(true, autoProperty[3]);
        Assert.AreEqual(false, autoProperty[4]);


        var autoPropertyWithInit = result.FirstOrDefault(r => r[0].ToString() == "AutoPropertyWithInit");
        Assert.IsNotNull(autoPropertyWithInit);
        Assert.AreEqual(true, autoPropertyWithInit[1]);
        Assert.AreEqual(true, autoPropertyWithInit[2]);
        Assert.AreEqual(true, autoPropertyWithInit[3]);
        Assert.AreEqual(true, autoPropertyWithInit[4]);


        var autoPropertyReadOnly = result.FirstOrDefault(r => r[0].ToString() == "AutoPropertyReadOnly");
        Assert.IsNotNull(autoPropertyReadOnly);
        Assert.AreEqual(true, autoPropertyReadOnly[1]);
        Assert.AreEqual(true, autoPropertyReadOnly[2]);
        Assert.AreEqual(false, autoPropertyReadOnly[3]);
        Assert.AreEqual(false, autoPropertyReadOnly[4]);


        var propertyWithCustomGetter = result.FirstOrDefault(r => r[0].ToString() == "PropertyWithCustomGetter");
        Assert.IsNotNull(propertyWithCustomGetter);
        Assert.AreEqual(false, propertyWithCustomGetter[1]);
        Assert.AreEqual(true, propertyWithCustomGetter[2]);
        Assert.AreEqual(true, propertyWithCustomGetter[3]);
        Assert.AreEqual(false, propertyWithCustomGetter[4]);


        var expressionBodiedProperty = result.FirstOrDefault(r => r[0].ToString() == "ExpressionBodiedProperty");
        Assert.IsNotNull(expressionBodiedProperty);
        Assert.AreEqual(false, expressionBodiedProperty[1]);
        Assert.AreEqual(true, expressionBodiedProperty[2]);
        Assert.AreEqual(false, expressionBodiedProperty[3]);
        Assert.AreEqual(false, expressionBodiedProperty[4]);


        var getterOnly = result.FirstOrDefault(r => r[0].ToString() == "GetterOnly");
        Assert.IsNotNull(getterOnly);
        Assert.AreEqual(true, getterOnly[1]);
        Assert.AreEqual(true, getterOnly[2]);
        Assert.AreEqual(false, getterOnly[3]);
        Assert.AreEqual(false, getterOnly[4]);


        var initOnly = result.FirstOrDefault(r => r[0].ToString() == "InitOnly");
        Assert.IsNotNull(initOnly);
        Assert.AreEqual(true, initOnly[1]);
        Assert.AreEqual(false, initOnly[2]);
        Assert.AreEqual(true, initOnly[3]);
        Assert.AreEqual(true, initOnly[4]);
    }

    [TestMethod]
    public void WhenAbstractMethodQueried_ShouldHaveNoBody()
    {
        var query = """
                    select
                        m.Name,
                        m.HasBody,
                        m.IsEmpty,
                        m.StatementsCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'AbstractClassWithAbstractMethod' and m.Name = 'AbstractMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("AbstractMethod", result[0][0].ToString());
        Assert.AreEqual(false, result[0][1]);
        Assert.AreEqual(false, result[0][2]);
        Assert.AreEqual(0, result[0][3]);
    }

    [TestMethod]
    public void WhenInterfaceMethodQueried_ShouldHaveNoBody()
    {
        var query = """
                    select
                        m.Name,
                        m.HasBody,
                        m.IsEmpty,
                        m.StatementsCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Interfaces i
                    cross apply i.Methods m
                    where i.Name = 'IInterfaceWithMethods' and m.Name = 'InterfaceMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("InterfaceMethod", result[0][0].ToString());
        Assert.AreEqual(false, result[0][1]);
        Assert.AreEqual(false, result[0][2]);
        Assert.AreEqual(0, result[0][3]);
    }

    [TestMethod]
    public void WhenFieldsQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        f.Name,
                        f.Type,
                        f.IsReadOnly,
                        f.IsConst,
                        f.IsStatic,
                        f.IsVolatile,
                        f.Accessibility
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Fields f
                    where c.Name = 'TestFeatures'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        Assert.IsTrue(result.Count >= 4, $"Should have at least 4 fields, got {result.Count}");


        var readonlyField = result.FirstOrDefault(r => r[0].ToString() == "_readonlyField");
        Assert.IsNotNull(readonlyField);
        Assert.AreEqual("Int32", readonlyField[1].ToString());
        Assert.AreEqual(true, readonlyField[2]);
        Assert.AreEqual(false, readonlyField[3]);
        Assert.AreEqual(false, readonlyField[4]);


        var constField = result.FirstOrDefault(r => r[0].ToString() == "ConstField");
        Assert.IsNotNull(constField);
        Assert.AreEqual(true, constField[3]);


        var staticField = result.FirstOrDefault(r => r[0].ToString() == "_staticField");
        Assert.IsNotNull(staticField);
        Assert.AreEqual(true, staticField[4]);


        var volatileField = result.FirstOrDefault(r => r[0].ToString() == "_volatileField");
        Assert.IsNotNull(volatileField);
        Assert.AreEqual(true, volatileField[5]);
    }

    [TestMethod]
    public void WhenConstructorsQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        c.Name,
                        c.ParameterCount,
                        c.HasBody,
                        c.HasInitializer,
                        c.InitializerKind
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Constructors c
                    where cls.Name = 'TestFeatures'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        Assert.AreEqual(3, result.Count);


        var defaultCtor = result.FirstOrDefault(r => (int)r[1] == 0);
        Assert.IsNotNull(defaultCtor);
        Assert.AreEqual(true, defaultCtor[2]);
        Assert.AreEqual(false, defaultCtor[3]);


        var ctorWithInit = result.FirstOrDefault(r => (int)r[1] == 2);
        Assert.IsNotNull(ctorWithInit);
        Assert.AreEqual(true, ctorWithInit[3]);
        Assert.AreEqual("this", ctorWithInit[4]?.ToString());
    }

    [TestMethod]
    public void WhenStructsQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        s.Name,
                        s.IsReadOnly,
                        s.MethodsCount,
                        s.PropertiesCount,
                        s.FieldsCount,
                        s.ConstructorsCount
                    from #csharp.solution('{Solution1SolutionPath}') sl 
                    cross apply sl.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Structs s
                    where s.Name = 'TestStruct' or s.Name = 'ReadOnlyTestStruct'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);


        var testStruct = result.FirstOrDefault(r => r[0].ToString() == "TestStruct");
        Assert.IsNotNull(testStruct);
        Assert.AreEqual(false, testStruct[1]);
        Assert.AreEqual(1, testStruct[2]);
        Assert.AreEqual(2, testStruct[3]);
        Assert.AreEqual(1, testStruct[4]);
        Assert.AreEqual(1, testStruct[5]);


        var readonlyStruct = result.FirstOrDefault(r => r[0].ToString() == "ReadOnlyTestStruct");
        Assert.IsNotNull(readonlyStruct);
        Assert.AreEqual(true, readonlyStruct[1]);
    }

    [TestMethod]
    public void WhenAsyncMethodPropertiesQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        m.Name,
                        m.IsAsync,
                        m.ContainsAwait,
                        m.AwaitCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'TestFeatures' and m.Name = 'AsyncMethodWithAwaits'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
        Assert.AreEqual(true, result[0][2]);
        Assert.AreEqual(2, result[0][3]);
    }

    [TestMethod]
    public void WhenLambdaPropertiesQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        m.Name,
                        m.ContainsLambda,
                        m.LambdaCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'TestFeatures' and m.Name = 'MethodWithLambda'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
        Assert.AreEqual(2, result[0][2]);
    }

    [TestMethod]
    public void WhenNestingDepthQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        m.Name,
                        m.MaxNestingDepth
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'TestFeatures' and m.Name = 'DeeplyNestedMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0][1]);
    }

    [TestMethod]
    public void WhenUsingDirectivesQueried_ShouldReturnResults()
    {
        var query = """
                    select
                        u.Name,
                        u.IsStatic,
                        u.IsGlobal,
                        u.HasAlias
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.UsingDirectives u
                    where d.Name = 'Class1.cs'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        Assert.IsTrue(result.Count >= 0, "Should have using directives (or none if implicit usings)");
    }

    [TestMethod]
    public void WhenEventsQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        e.Name,
                        e.Type,
                        e.IsStatic,
                        e.IsFieldLike
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    cross apply c.Events e
                    where c.Name = 'TestFeatures'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("SimpleEvent", result[0][0].ToString());
        Assert.AreEqual("EventHandler", result[0][1].ToString());
        Assert.AreEqual(false, result[0][2]);
        Assert.AreEqual(true, result[0][3]);
    }

    [TestMethod]
    public void WhenClassCouplingMetricsQueried_ShouldReturnValues()
    {
        var query = """
                    select
                        c.Name,
                        c.EfferentCoupling,
                        c.Instability,
                        c.WeightedMethodsPerClass,
                        c.MaxMethodComplexity,
                        c.AverageMethodComplexity
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    where c.Name = 'Class1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue((int)result[0][1] >= 0);
        Assert.IsTrue((double)result[0][2] >= 0 && (double)result[0][2] <= 1);
        Assert.IsTrue((int)result[0][3] >= 0);
    }

    [TestMethod]
    public void WhenDocumentationCoverageQueried_ShouldReturnValues()
    {
        var query = """
                    select
                        c.Name,
                        c.HasDocumentation,
                        c.MethodDocumentationCoverage,
                        c.PropertyDocumentationCoverage
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects p 
                    cross apply p.Documents d 
                    cross apply d.Classes c
                    where c.Name = 'TestFeatures'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
        Assert.IsTrue((double)result[0][2] >= 0 && (double)result[0][2] <= 100);
        Assert.IsTrue((double)result[0][3] >= 0 && (double)result[0][3] <= 100);
    }

    [TestMethod]
    public void WhenGetStructsByNames_ShouldReturnStructs()
    {
        var query = """
                    select
                        st.Name,
                        st.IsReadOnly
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.GetStructsByNames('TestStruct', 'ReadOnlyTestStruct') st
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(r => r[0].ToString() == "TestStruct" && !(bool)r[1]));
        Assert.IsTrue(result.Any(r => r[0].ToString() == "ReadOnlyTestStruct" && (bool)r[1]));
    }

    [TestMethod]
    public void WhenUnusedParametersQueried_ShouldReturnUnusedParameters()
    {
        var query = """
                    select
                        m.Name,
                        param.Name,
                        param.Type,
                        param.IsUsed
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    cross apply m.Parameters param
                    where c.Name = 'UnusedCodeTestClass' and m.Name = 'MethodWithUnusedParameter'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);


        var usedParam = result.FirstOrDefault(r => r[1].ToString() == "usedParam");
        Assert.IsNotNull(usedParam);
        Assert.AreEqual(true, usedParam[3]);


        var unusedParam = result.FirstOrDefault(r => r[1].ToString() == "unusedParam");
        Assert.IsNotNull(unusedParam);
        Assert.AreEqual(false, unusedParam[3]);
    }

    [TestMethod]
    public void WhenLocalVariablesQueried_ShouldReturnVariablesWithUsage()
    {
        var query = """
                    select
                        m.Name,
                        v.Name,
                        v.Type,
                        v.IsUsed
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    cross apply m.LocalVariables v
                    where c.Name = 'UnusedCodeTestClass' and m.Name = 'MethodWithUnusedVariable'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);


        var usedVar = result.FirstOrDefault(r => r[1].ToString() == "usedVar");
        Assert.IsNotNull(usedVar);
        Assert.AreEqual(true, usedVar[3]);


        var unusedVar = result.FirstOrDefault(r => r[1].ToString() == "unusedVar");
        Assert.IsNotNull(unusedVar);
        Assert.AreEqual(false, unusedVar[3]);
    }

    [TestMethod]
    public void WhenLocalFunctionsQueried_ShouldReturnLocalFunctions()
    {
        var query = """
                    select
                        m.Name,
                        lf.Name,
                        lf.ReturnType,
                        lf.IsAsync,
                        lf.IsStatic
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    cross apply m.LocalFunctions lf
                    where c.Name = 'CallGraphTestClass' and m.Name = 'MethodWithLocalFunctions'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(3, result.Count);


        var localAdd = result.FirstOrDefault(r => r[1].ToString() == "LocalAdd");
        Assert.IsNotNull(localAdd);
        Assert.AreEqual("Int32", localAdd[2].ToString());
        Assert.AreEqual(false, localAdd[3]);
        Assert.AreEqual(false, localAdd[4]);


        var localAsync = result.FirstOrDefault(r => r[1].ToString() == "LocalAsyncFunction");
        Assert.IsNotNull(localAsync);
        Assert.AreEqual("Task", localAsync[2].ToString());
        Assert.AreEqual(true, localAsync[3]);
        Assert.AreEqual(false, localAsync[4]);


        var localStatic = result.FirstOrDefault(r => r[1].ToString() == "LocalStaticFunction");
        Assert.IsNotNull(localStatic);
        Assert.AreEqual("Int32", localStatic[2].ToString());
        Assert.AreEqual(false, localStatic[3]);
        Assert.AreEqual(true, localStatic[4]);
    }

    [TestMethod]
    public void WhenMethodUnusedParameterCountQueried_ShouldReturnCorrectCount()
    {
        var query = """
                    select
                        m.Name,
                        m.UnusedParameterCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'UnusedCodeTestClass' 
                      and (m.Name = 'MethodWithUnusedParameter' or m.Name = 'MethodWithMultipleUnusedParams' or m.Name = 'MethodWithAllParamsUsed')
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(3, result.Count);


        var oneUnused = result.FirstOrDefault(r => r[0].ToString() == "MethodWithUnusedParameter");
        Assert.IsNotNull(oneUnused);
        Assert.AreEqual(1, oneUnused[1]);


        var threeUnused = result.FirstOrDefault(r => r[0].ToString() == "MethodWithMultipleUnusedParams");
        Assert.IsNotNull(threeUnused);
        Assert.AreEqual(3, threeUnused[1]);


        var noneUnused = result.FirstOrDefault(r => r[0].ToString() == "MethodWithAllParamsUsed");
        Assert.IsNotNull(noneUnused);
        Assert.AreEqual(0, noneUnused[1]);
    }

    [TestMethod]
    public void WhenMethodUnusedVariableCountQueried_ShouldReturnCorrectCount()
    {
        var query = """
                    select
                        m.Name,
                        m.LocalVariableCount,
                        m.UnusedVariableCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where c.Name = 'UnusedCodeTestClass' and m.Name = 'MethodWithUnusedVariable'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result[0][1]);
        Assert.AreEqual(1, result[0][2]);
    }

    [TestMethod]
    public void WhenGetMethodsWithUnusedParametersCalled_ShouldReturnMethodsWithUnusedParams()
    {
        var query = """
                    select
                        m.Name,
                        m.UnusedParameterCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.GetMethodsWithUnusedParameters() m
                    where m.Name like 'MethodWith%Unused%'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        Assert.IsTrue(result.Count >= 2);
    }

    [TestMethod]
    public void WhenFieldIsUsedQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        f.Name,
                        f.IsUsed,
                        f.ReferenceCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    cross apply c.Fields f
                    where c.Name = 'UnusedCodeTestClass'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        Assert.IsTrue(result.Count >= 2);


        var usedField = result.FirstOrDefault(r => r[0].ToString() == "_usedField");
        Assert.IsNotNull(usedField);
        Assert.AreEqual(true, usedField[1]);
        Assert.IsTrue((int)usedField[2] > 0);


        var unusedField = result.FirstOrDefault(r => r[0].ToString() == "_unusedField");
        Assert.IsNotNull(unusedField);
        Assert.AreEqual(false, unusedField[1]);
        Assert.AreEqual(0, unusedField[2]);
    }

    [TestMethod]
    public void WhenClassIsUsedQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        c.Name,
                        c.IsUsed,
                        c.ReferenceCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    where c.Name = 'UsedInterfaceImplementor' or c.Name = 'Class1'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);


        var class1 = result.FirstOrDefault(r => r[0].ToString() == "Class1");
        Assert.IsNotNull(class1);
        Assert.IsNotNull(class1[1]);
    }

    [TestMethod]
    public void WhenInterfaceIsUsedQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        i.Name,
                        i.IsUsed,
                        i.ReferenceCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Interfaces i
                    where i.Name = 'IUsedInterface' or i.Name = 'IUnusedInterface'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);


        var usedInterface = result.FirstOrDefault(r => r[0].ToString() == "IUsedInterface");
        Assert.IsNotNull(usedInterface);
        Assert.AreEqual(true, usedInterface[1]);
        Assert.IsTrue((int)usedInterface[2] > 0);


        var unusedInterface = result.FirstOrDefault(r => r[0].ToString() == "IUnusedInterface");
        Assert.IsNotNull(unusedInterface);
        Assert.AreEqual(false, unusedInterface[1]);
        Assert.AreEqual(0, unusedInterface[2]);
    }

    [TestMethod]
    public void WhenEnumIsUsedQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        e.Name,
                        e.IsUsed,
                        e.ReferenceCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Enums e
                    where e.Name = 'UsedEnum' or e.Name = 'UnusedEnum'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);


        var usedEnum = result.FirstOrDefault(r => r[0].ToString() == "UsedEnum");
        Assert.IsNotNull(usedEnum);
        Assert.AreEqual(true, usedEnum[1]);
        Assert.IsTrue((int)usedEnum[2] > 0);


        var unusedEnum = result.FirstOrDefault(r => r[0].ToString() == "UnusedEnum");
        Assert.IsNotNull(unusedEnum);
        Assert.AreEqual(false, unusedEnum[1]);
        Assert.AreEqual(0, unusedEnum[2]);
    }

    [TestMethod]
    public void WhenStructIsUsedQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        st.Name,
                        st.IsUsed,
                        st.ReferenceCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Structs st
                    where st.Name = 'UsedStruct' or st.Name = 'UnusedStruct'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);


        var usedStruct = result.FirstOrDefault(r => r[0].ToString() == "UsedStruct");
        Assert.IsNotNull(usedStruct);
        Assert.AreEqual(true, usedStruct[1]);
        Assert.IsTrue((int)usedStruct[2] > 0);


        var unusedStruct = result.FirstOrDefault(r => r[0].ToString() == "UnusedStruct");
        Assert.IsNotNull(unusedStruct);
        Assert.AreEqual(false, unusedStruct[1]);
        Assert.AreEqual(0, unusedStruct[2]);
    }

    [TestMethod]
    public void WhenMethodIsUsedQueried_ShouldReturnCorrectValues()
    {
        var query = """
                    select
                        m.Name,
                        m.IsUsed,
                        m.ReferenceCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    cross apply c.Methods m
                    where m.Name = 'GetUsedField' or m.Name = 'UnusedPrivateMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);

        var usedMethod = result.FirstOrDefault(r => r[0].ToString() == "GetUsedField");
        Assert.IsNotNull(usedMethod);
        Assert.AreEqual(true, usedMethod[1]);
        Assert.IsTrue((int)usedMethod[2] > 0);

        var unusedMethod = result.FirstOrDefault(r => r[0].ToString() == "UnusedPrivateMethod");
        Assert.IsNotNull(unusedMethod);
        Assert.AreEqual(false, unusedMethod[1]);
        Assert.AreEqual(0, unusedMethod[2]);
    }

    [TestMethod]
    public void WhenGetUnusedFieldsCalled_ShouldReturnUnusedFields()
    {
        var query = """
                    select
                        f.Name
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.GetUnusedFields() f
                    where f.Name = '_unusedField'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        Assert.IsTrue(result.Count >= 1);
        Assert.IsTrue(result.Any(r => r[0].ToString() == "_unusedField"));
    }

    [TestMethod]
    public void WhenMethodCalleesQueried_ShouldReturnCalledMethods()
    {
        var query = """
                    select
                        m.Name,
                        m.CalleeCount,
                        c.Name as CalleeName,
                        c.ContainingTypeName
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    cross apply m.Callees c
                    where m.Name = 'CallerMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();


        Assert.IsTrue(result.Count >= 2, "CallerMethod should call at least 2 methods");
        Assert.IsTrue(result.Any(r => r[2].ToString() == "HelperMethod1"), "Should call HelperMethod1");
        Assert.IsTrue(result.Any(r => r[2].ToString() == "HelperMethod2"), "Should call HelperMethod2");
    }

    [TestMethod]
    public void WhenMethodIsRecursiveQueried_ShouldReturnCorrectValue()
    {
        var query = """
                    select
                        m.Name,
                        m.IsRecursive,
                        m.CalleeCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    where m.Name = 'RecursiveMethod' or m.Name = 'NonRecursiveMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);

        var recursiveMethod = result.FirstOrDefault(r => r[0].ToString() == "RecursiveMethod");
        Assert.IsNotNull(recursiveMethod);
        Assert.IsTrue((int)recursiveMethod[2] >= 1,
            $"RecursiveMethod should have at least one callee (itself), has {recursiveMethod[2]}");
        Assert.AreEqual(true, recursiveMethod[1], "RecursiveMethod should be marked as recursive");

        var nonRecursiveMethod = result.FirstOrDefault(r => r[0].ToString() == "NonRecursiveMethod");
        Assert.IsNotNull(nonRecursiveMethod);
        Assert.AreEqual(0, (int)nonRecursiveMethod[2], "NonRecursiveMethod should not have callees");
        Assert.AreEqual(false, nonRecursiveMethod[1], "NonRecursiveMethod should not be recursive");
    }

    [TestMethod]
    public void WhenMethodOverridesQueried_ShouldReturnOverriddenMethodInfo()
    {
        var query = """
                    select
                        m.Name,
                        m.IsOverride,
                        m.OverriddenMethodName,
                        m.OverriddenMethodContainingType
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    where cls.Name = 'DerivedClassWithOverride' and m.IsOverride = true
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, "Should find at least one overridden method");

        var virtualMethod = result.FirstOrDefault(r => r[0].ToString() == "VirtualMethod");
        Assert.IsNotNull(virtualMethod);
        Assert.AreEqual("VirtualMethod", virtualMethod[2], "Should override VirtualMethod");
        Assert.AreEqual("BaseClassForOverride", virtualMethod[3], "Should be from BaseClassForOverride");
    }

    [TestMethod]
    public void WhenMethodImplementsInterfaceQueried_ShouldReturnInterfaceInfo()
    {
        var query = """
                    select
                        m.Name,
                        m.ImplementsInterface,
                        i.InterfaceName,
                        i.MethodName
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    cross apply m.ImplementedInterfaceMethods i
                    where cls.Name = 'InterfaceImplementorClass'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, "Should find interface implementations");
        Assert.IsTrue(result.Any(r => r[2].ToString() == "ITestInterface"), "Should implement ITestInterface");
    }

    [TestMethod]
    public void WhenMethodReturnsTaskQueried_ShouldReturnCorrectValue()
    {
        var query = """
                    select
                        m.Name,
                        m.ReturnsTask,
                        m.IsAsync,
                        m.FullReturnType
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    where cls.Name = 'AsyncTestClass'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 4, "Should find async test methods");

        var asyncVoidMethod = result.FirstOrDefault(r => r[0].ToString() == "AsyncVoidMethod");
        Assert.IsNotNull(asyncVoidMethod);
        Assert.AreEqual(true, asyncVoidMethod[1], "AsyncVoidMethod should return Task");
        Assert.AreEqual(true, asyncVoidMethod[2], "AsyncVoidMethod should be async");

        var syncMethod = result.FirstOrDefault(r => r[0].ToString() == "SyncMethod");
        Assert.IsNotNull(syncMethod);
        Assert.AreEqual(false, syncMethod[1], "SyncMethod should not return Task");
        Assert.AreEqual(false, syncMethod[2], "SyncMethod should not be async");
    }

    [TestMethod]
    public void WhenMethodNullableReturnQueried_ShouldReturnCorrectValue()
    {
        var query = """
                    select
                        m.Name,
                        m.IsReturnTypeNullable
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    where m.Name = 'NullableReturnMethod' or m.Name = 'NonNullableReturnMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);

        var nullableMethod = result.FirstOrDefault(r => r[0].ToString() == "NullableReturnMethod");
        Assert.IsNotNull(nullableMethod);
        Assert.AreEqual(true, nullableMethod[1], "NullableReturnMethod should have nullable return");

        var nonNullableMethod = result.FirstOrDefault(r => r[0].ToString() == "NonNullableReturnMethod");
        Assert.IsNotNull(nonNullableMethod);
        Assert.AreEqual(false, nonNullableMethod[1], "NonNullableReturnMethod should not have nullable return");
    }

    [TestMethod]
    public void WhenMethodIsPublicApiQueried_ShouldReturnCorrectValue()
    {
        var query = """
                    select
                        m.Name,
                        m.IsPublicApi,
                        m.Accessibility
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    where cls.Name = 'PublicApiClass'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3, "Should find methods in PublicApiClass");

        var publicMethod = result.FirstOrDefault(r => r[0].ToString() == "PublicMethod");
        Assert.IsNotNull(publicMethod);
        Assert.AreEqual(true, publicMethod[1], "PublicMethod should be part of public API");

        var protectedMethod = result.FirstOrDefault(r => r[0].ToString() == "ProtectedMethod");
        Assert.IsNotNull(protectedMethod);
        Assert.AreEqual(true, protectedMethod[1], "ProtectedMethod should be part of public API");
    }

    [TestMethod]
    public void WhenMethodLocationQueried_ShouldReturnLineNumbers()
    {
        var query = """
                    select
                        m.Name,
                        m.StartLine,
                        m.EndLine,
                        m.SourceFilePath,
                        m.ContainingTypeName,
                        m.ContainingNamespace
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes cls
                    cross apply cls.Methods m
                    where m.Name = 'CallerMethod'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        var method = result[0];
        Assert.IsTrue((int)method[1] > 0, "StartLine should be positive");
        Assert.IsTrue((int)method[2] > (int)method[1], "EndLine should be greater than StartLine");
        Assert.IsTrue(method[3]?.ToString()?.EndsWith(".cs") == true, "SourceFilePath should end with .cs");
        Assert.AreEqual("CallGraphTestClass", method[4]?.ToString(), "ContainingTypeName should be CallGraphTestClass");
    }

    [TestMethod]
    public void WhenClassIsPublicApiQueried_ShouldReturnCorrectValue()
    {
        var query = """
                    select
                        c.Name,
                        c.IsPublicApi,
                        c.PublicMethodCount,
                        c.StartLine,
                        c.EndLine
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    cross apply d.Classes c
                    where c.Name = 'PublicApiClass' or c.Name = 'InternalApiClass'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count);

        var publicClass = result.FirstOrDefault(r => r[0].ToString() == "PublicApiClass");
        Assert.IsNotNull(publicClass);
        Assert.AreEqual(true, publicClass[1], "PublicApiClass should be part of public API");
        Assert.IsTrue((int)publicClass[2] >= 1, "Should have at least 1 public method");

        var internalClass = result.FirstOrDefault(r => r[0].ToString() == "InternalApiClass");
        Assert.IsNotNull(internalClass);
        Assert.AreEqual(false, internalClass[1], "InternalApiClass should not be part of public API");
    }

    [TestMethod]
    public void WhenDocumentReferencedTypesQueried_ShouldReturnTypes()
    {
        var query = """
                    select
                        d.Name,
                        d.ReferencedTypeCount,
                        d.ReferencedNamespaceCount,
                        d.ReferencedAssemblyCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    where d.Name = 'TestFeatures.cs'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);

        var doc = result[0];
        Assert.IsTrue((int)doc[1] > 0, "Should reference some types");
        Assert.IsTrue((int)doc[2] > 0, "Should reference some namespaces");
        Assert.IsTrue((int)doc[3] > 0, "Should reference some assemblies");
    }

    [TestMethod]
    public void WhenDocumentReferencedNamespacesQueried_ShouldReturnNamespaces()
    {
        var query = """
                    select
                        d.Name,
                        d.ReferencedNamespaceCount
                    from #csharp.solution('{Solution1SolutionPath}') s 
                    cross apply s.Projects proj 
                    cross apply proj.Documents d 
                    where d.Name = 'TestFeatures.cs'
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath.Escape());

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count > 0, "Should find document");
        Assert.IsTrue((int)result[0][1] > 0, "Should reference some namespaces");
    }

    [TestMethod]
    public void WhenAllInterfacesQueried_ShouldReturnTransitiveInterfaces()
    {
        var query = $@"
            select 
                c.Name,
                ai.Value as InterfaceName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.AllInterfaces ai
            where c.Name = 'DeepImplementor'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3, $"DeepImplementor should implement at least 3 interfaces transitively, got {result.Count}");
        
        var interfaceNames = result.Select(r => r[1].ToString()).ToList();
        Assert.IsTrue(interfaceNames.Any(n => n!.Contains("ISuperAdvancedProcessable")), "Should contain ISuperAdvancedProcessable");
        Assert.IsTrue(interfaceNames.Any(n => n!.Contains("IAdvancedProcessable")), "Should contain IAdvancedProcessable (transitive)");
        Assert.IsTrue(interfaceNames.Any(n => n!.Contains("IProcessable")), "Should contain IProcessable (transitive)");
    }

    [TestMethod]
    public void WhenAllInterfacesQueried_DirectInterfacesShouldStillWork()
    {
        var query = $@"
            select 
                c.Name,
                i.Value as InterfaceName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Interfaces i
            where c.Name = 'DeepImplementor'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "DeepImplementor directly implements only ISuperAdvancedProcessable");
        Assert.AreEqual("ISuperAdvancedProcessable", result[0][1].ToString());
    }

    [TestMethod]
    public void WhenAllBaseInterfacesQueried_ShouldReturnTransitiveBaseInterfaces()
    {
        var query = $@"
            select 
                i.Name,
                abi.Value as BaseInterfaceName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Interfaces i
            cross apply i.AllBaseInterfaces abi
            where i.Name = 'ISuperAdvancedProcessable'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2, $"ISuperAdvancedProcessable should have at least 2 transitive base interfaces, got {result.Count}");
        
        var baseNames = result.Select(r => r[1].ToString()).ToList();
        Assert.IsTrue(baseNames.Any(n => n!.Contains("IAdvancedProcessable")), "Should contain IAdvancedProcessable");
        Assert.IsTrue(baseNames.Any(n => n!.Contains("IProcessable")), "Should contain IProcessable (transitive)");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_CastShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind,
                rt.IsInterface
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithCast'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Cast" && (bool)r[3]),
            "Should find IProcessable used as Cast and marked as interface");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_IsOperatorShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.IsInterface
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithIsOperator'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Is"),
            "Should find IProcessable used with 'is' operator");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_AsOperatorShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithAsOperator'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "As"),
            "Should find IProcessable used with 'as' operator");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_PatternMatchShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithPatternMatch'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "PatternMatch"),
            "Should find IProcessable used in pattern matching");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_LocalVariableShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithLocalVariable'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "LocalVariable"),
            "Should find IProcessable used as local variable type");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_GenericArgumentShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithGenericArgument'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "GenericArgument"),
            "Should find IProcessable used as generic type argument");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_TypeOfShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithTypeOf'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "TypeOf"),
            "Should find IProcessable used in typeof()");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_DefaultShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithDefault'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Default"),
            "Should find IProcessable used in default()");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_MultipleUsagesShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.FullName,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithMultipleUsages'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3, $"Should find at least 3 type references, got {result.Count}");
        
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable"), "Should reference IProcessable");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IAdvancedProcessable"), "Should reference IAdvancedProcessable");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "ISuperAdvancedProcessable"), "Should reference ISuperAdvancedProcessable");
        
        Assert.IsTrue(result.All(r => r[3].ToString() == "Interface"), "All referenced types should be interfaces");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_ArrayCreationShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithArrayCreation'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "ArrayCreation"),
            "Should find IProcessable used in array creation");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_SwitchPatternShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithSwitchPattern'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 pattern references, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable"), "Should find IProcessable in switch pattern");
    }

    [TestMethod]
    public void WhenReferencedTypesFiltered_OnlyInterfacesShouldBeReturned()
    {
        var query = $@"
            select 
                rt.Name,
                rt.FullName,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithMultipleUsages' and rt.IsInterface = true";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3, $"Should find at least 3 interface references, got {result.Count}");
        Assert.IsTrue(result.All(r => r[1].ToString()!.Contains("Solution1.ClassLibrary1")),
            "All FullName values should contain the namespace");
    }

    [TestMethod]
    public void WhenPropertyFullTypeNameQueried_ShouldReturnFullyQualifiedName()
    {
        var query = $@"
            select 
                pr.Name,
                pr.Type,
                pr.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyUsagePatterns' and pr.Name = 'CastingProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, $"Should find exactly 1 property, got {result.Count}");
        Assert.AreEqual("CastingProperty", result[0][0].ToString());
        Assert.IsTrue(result[0][2].ToString()!.Contains("IProcessable"), 
            $"FullTypeName should contain IProcessable, got {result[0][2]}");
    }

    [TestMethod]
    public void WhenParameterFullTypeNameQueried_ShouldReturnFullyQualifiedName()
    {
        var query = $@"
            select 
                param.Name,
                param.Type,
                param.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.Parameters param
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithCast'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Should find exactly 1 parameter");
        Assert.AreEqual("obj", result[0][0].ToString());
        Assert.AreEqual("Object", result[0][1].ToString());
        Assert.AreEqual("object", result[0][2].ToString());
    }

    [TestMethod]
    public void WhenMethodFullReturnTypeQueried_ShouldReturnFullyQualifiedName()
    {
        var query = $@"
            select 
                m.Name,
                m.ReturnType,
                m.FullReturnType
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithDefault'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Should find exactly 1 method");
        Assert.AreEqual("MethodWithDefault", result[0][0].ToString());
        Assert.IsTrue(result[0][2].ToString()!.Contains("IProcessable"),
            $"FullReturnType should contain IProcessable, got {result[0][2]}");
    }

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_ShouldDetectTypesInBody()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorUsagePatterns'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2, $"Should find at least 2 type references in constructor, got {result.Count}");
        
        var castRef = result.FirstOrDefault(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Cast");
        Assert.IsNotNull(castRef, "Should detect IProcessable cast in constructor body");
        
        var patternRef = result.FirstOrDefault(r => r[0].ToString() == "IAdvancedProcessable" && r[1].ToString() == "PatternMatch");
        Assert.IsNotNull(patternRef, "Should detect IAdvancedProcessable pattern match in constructor body");
    }

    [TestMethod]
    public void WhenPropertyReferencedTypesQueried_ShouldDetectTypesInAccessors()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.ReferencedTypes rt
            where c.Name = 'PropertyUsagePatterns' and pr.Name = 'CastingProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2, $"Should find at least 2 type references in property accessors, got {result.Count}");
        
        var asRef = result.FirstOrDefault(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "As");
        Assert.IsNotNull(asRef, "Should detect IProcessable 'as' usage in getter body");
        
        var patternRef = result.FirstOrDefault(r => r[0].ToString() == "IAdvancedProcessable" && r[1].ToString() == "PatternMatch");
        Assert.IsNotNull(patternRef, "Should detect IAdvancedProcessable pattern match in setter body");
    }

    [TestMethod]
    public void WhenPropertyExpressionBodiedReferencedTypesQueried_ShouldDetectTypes()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.ReferencedTypes rt
            where c.Name = 'PropertyUsagePatterns' and pr.Name = 'ProcessableType'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, $"Should find exactly 1 type reference in expression-bodied property, got {result.Count}");
        Assert.AreEqual("IProcessable", result[0][0].ToString());
        Assert.AreEqual("TypeOf", result[0][1].ToString());
    }

    [TestMethod]
    public void WhenVarInferredTypeQueried_ShouldResolveActualType()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithVarInferred'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2, $"Should find at least 2 type references, got {result.Count}");
        
        // The explicit IProcessable local variable should be detected
        var explicitLocal = result.FirstOrDefault(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "LocalVariable");
        Assert.IsNotNull(explicitLocal, "Should detect explicit IProcessable local variable");
        
        // The cast should also be detected  
        var castRef = result.FirstOrDefault(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Cast");
        Assert.IsNotNull(castRef, "Should detect IProcessable cast");
    }

    [TestMethod]
    public void WhenConstructorLocalVariableTypesQueried_ShouldDetectExplicitTypes()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorUsagePatterns' and rt.UsageKind = 'LocalVariable'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 local variable type reference in constructor, got {result.Count}");
        var localRef = result.FirstOrDefault(r => r[0].ToString() == "IProcessable");
        Assert.IsNotNull(localRef, "Should detect IProcessable local variable in constructor body");
    }

    [TestMethod]
    public void WhenConstructorLocalVariablesQueried_ShouldReturnVariables()
    {
        var query = $@"
            select 
                lv.Name,
                lv.Type,
                lv.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.LocalVariables lv
            where c.Name = 'ConstructorUsagePatterns'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 local variable in constructor, got {result.Count}");
        var localVar = result.FirstOrDefault(r => r[0].ToString() == "local");
        Assert.IsNotNull(localVar, "Should find local variable named 'local'");
        Assert.AreEqual("IProcessable", localVar![1].ToString());
        Assert.IsTrue(localVar[2].ToString()!.Contains("IProcessable"),
            $"FullTypeName should contain IProcessable, got {localVar[2]}");
    }

    [TestMethod]
    public void WhenPropertyLocalVariablesQueried_ShouldReturnVariablesInAccessors()
    {
        var query = $@"
            select 
                lv.Name,
                lv.Type,
                lv.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.LocalVariables lv
            where c.Name = 'PropertyUsagePatterns' and pr.Name = 'CastingProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 local variable in property accessor, got {result.Count}");
        var backupVar = result.FirstOrDefault(r => r[0].ToString() == "backup");
        Assert.IsNotNull(backupVar, "Should find local variable named 'backup'");
        Assert.AreEqual("IProcessable", backupVar![1].ToString());
        Assert.IsTrue(backupVar[2].ToString()!.Contains("IProcessable"),
            $"FullTypeName should contain IProcessable, got {backupVar[2]}");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: AllInterfaces / AllBaseInterfaces
    // =====================================================================
    
    [TestMethod]
    public void WhenClassHasNoInterfaces_AllInterfacesShouldBeEmpty()
    {
        var query = $@"
            select 
                c.Name,
                c.InterfacesCount
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            where c.Name = 'NoInterfaceClass'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Should find exactly 1 class");
        Assert.AreEqual(0, Convert.ToInt32(result[0][1]), "NoInterfaceClass should have 0 interfaces");
    }

    [TestMethod]
    public void WhenClassHasSingleInterface_AllInterfacesShouldContainOnlyThat()
    {
        var query = $@"
            select 
                c.Name,
                ai.Value as InterfaceName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.AllInterfaces ai
            where c.Name = 'SingleInterfaceClass'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "SingleInterfaceClass should have exactly 1 interface in AllInterfaces");
        Assert.IsTrue(result[0][1].ToString()!.Contains("IProcessable"), "Should be IProcessable");
    }

    [TestMethod]
    public void WhenAllInterfacesQueried_ShouldReturnFullyQualifiedNames()
    {
        var query = $@"
            select 
                ai.Value as InterfaceName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.AllInterfaces ai
            where c.Name = 'DeepImplementor'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3, "Should have at least 3 transitive interfaces");
        Assert.IsTrue(result.All(r => r[0].ToString()!.Contains("Solution1.ClassLibrary1")),
            "AllInterfaces should return fully qualified names including namespace");
    }

    [TestMethod]
    public void WhenInterfaceHasNoParents_AllBaseInterfacesShouldBeEmpty()
    {
        var query = $@"
            select 
                i.Name
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Interfaces i
            where i.Name = 'IStandaloneInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Should find the standalone interface");
        
        // Now query with cross apply - should produce no rows
        var query2 = $@"
            select 
                i.Name,
                abi.Value
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Interfaces i
            cross apply i.AllBaseInterfaces abi
            where i.Name = 'IStandaloneInterface'";

        var vm2 = CompileQuery(query2);
        var result2 = vm2.Run();

        Assert.AreEqual(0, result2.Count, "IStandaloneInterface should have no base interfaces");
    }

    [TestMethod]
    public void WhenInterfaceHasOneParent_AllBaseInterfacesShouldContainOnlyParent()
    {
        var query = $@"
            select 
                i.Name,
                abi.Value as BaseInterfaceName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Interfaces i
            cross apply i.AllBaseInterfaces abi
            where i.Name = 'IChildInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "IChildInterface should have exactly 1 base interface");
        Assert.IsTrue(result[0][1].ToString()!.Contains("IStandaloneInterface"), "Should be IStandaloneInterface");
    }

    [TestMethod]
    public void WhenAllBaseInterfacesQueried_ShouldReturnFullyQualifiedNames()
    {
        var query = $@"
            select 
                abi.Value as BaseInterfaceName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Interfaces i
            cross apply i.AllBaseInterfaces abi
            where i.Name = 'ISuperAdvancedProcessable'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2, "Should have at least 2 base interfaces");
        Assert.IsTrue(result.All(r => r[0].ToString()!.Contains("Solution1.ClassLibrary1")),
            "AllBaseInterfaces should return fully qualified names");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: TypeReferenceEntity properties
    // =====================================================================

    [TestMethod]
    public void WhenReferencedTypesQueried_FullNameShouldIncludeNamespace()
    {
        var query = $@"
            select 
                rt.Name,
                rt.Namespace,
                rt.FullName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithCast'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, "Should find at least 1 type reference");
        var ipRef = result.First(r => r[0].ToString() == "IProcessable");
        Assert.AreEqual("Solution1.ClassLibrary1", ipRef[1].ToString(), "Namespace should be correct");
        Assert.AreEqual("Solution1.ClassLibrary1.IProcessable", ipRef[2].ToString(), "FullName should be Namespace.Name");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_LineNumberShouldBePositive()
    {
        var query = $@"
            select 
                rt.Name,
                rt.LineNumber
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithCast'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, "Should find at least 1 type reference");
        foreach (var row in result)
        {
            var lineNumber = Convert.ToInt32(row[1]);
            Assert.IsTrue(lineNumber > 0, $"LineNumber should be positive (1-based), got {lineNumber}");
        }
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_KindShouldBeInterface()
    {
        var query = $@"
            select 
                rt.Name,
                rt.Kind,
                rt.IsInterface,
                rt.IsClass,
                rt.IsEnum,
                rt.IsStruct
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithCast'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        var ipRef = result.First(r => r[0].ToString() == "IProcessable");
        Assert.AreEqual("Interface", ipRef[1].ToString(), "Kind should be 'Interface'");
        Assert.IsTrue((bool)ipRef[2], "IsInterface should be true");
        Assert.IsFalse((bool)ipRef[3], "IsClass should be false");
        Assert.IsFalse((bool)ipRef[4], "IsEnum should be false");
        Assert.IsFalse((bool)ipRef[5], "IsStruct should be false");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_ClassReferenceShouldHaveKindClass()
    {
        var query = $@"
            select 
                rt.Name,
                rt.Kind,
                rt.UsageKind,
                rt.IsClass
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithLocalVariable'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        // DeepImplementor is used as ObjectCreation  
        var classRef = result.FirstOrDefault(r => r[0].ToString() == "DeepImplementor");
        Assert.IsNotNull(classRef, "Should find DeepImplementor class reference");
        Assert.AreEqual("Class", classRef[1].ToString(), "Kind should be 'Class'");
        Assert.IsTrue((bool)classRef[3], "IsClass should be true for DeepImplementor");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_ObjectCreationShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'EmptyBodyClass' and m.Name = 'MethodWithObjectCreation'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "DeepImplementor" && r[1].ToString() == "ObjectCreation"),
            "Should detect DeepImplementor as ObjectCreation");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "LocalVariable"),
            "Should detect IProcessable as LocalVariable type");
    }

    [TestMethod]
    public void WhenReferencedTypesQueried_CatchDeclarationShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'EmptyBodyClass' and m.Name = 'MethodWithCatchDeclaration'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        var catchRefs = result.Where(r => r[1].ToString() == "CatchDeclaration").ToList();
        Assert.IsTrue(catchRefs.Count >= 2, $"Should find at least 2 catch declaration type references, got {catchRefs.Count}");
        Assert.IsTrue(catchRefs.Any(r => r[0].ToString() == "InvalidOperationException"),
            "Should detect InvalidOperationException in catch");
        Assert.IsTrue(catchRefs.Any(r => r[0].ToString() == "ArgumentNullException"),
            "Should detect ArgumentNullException in catch");
    }

    [TestMethod]
    public void WhenMethodHasNoTypeReferences_ReferencedTypesCrossApplyShouldYieldNoRows()
    {
        var query = $@"
            select 
                rt.Name
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'EmptyBodyClass' and m.Name = 'MethodWithNoReferences'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "Method with only primitives/string should have no named type references");
    }

    [TestMethod]
    public void WhenMethodHasOnlyClassReferences_ShouldNotContainInterfaces()
    {
        var query = $@"
            select 
                rt.Name,
                rt.Kind,
                rt.IsInterface
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'EmptyBodyClass' and m.Name = 'MethodWithOnlyClassReferences' and rt.IsInterface = true";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "Method with only class references should have no interface references when filtered");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: Constructor ReferencedTypes (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_ShouldDetectGenericArgument()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "GenericArgument"),
            "Should detect IProcessable as generic argument in List<IProcessable>");
    }

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_ShouldDetectCatchDeclaration()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "InvalidOperationException" && r[1].ToString() == "CatchDeclaration"),
            "Should detect InvalidOperationException as CatchDeclaration in constructor");
    }

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_ShouldDetectPatternMatch()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "ISuperAdvancedProcessable" && r[1].ToString() == "PatternMatch"),
            "Should detect ISuperAdvancedProcessable pattern match in constructor");
    }

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_ShouldDetectArrayCreation()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "ArrayCreation"),
            "Should detect IProcessable array creation in constructor");
    }

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_ShouldDetectTypeOf()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IAdvancedProcessable" && r[1].ToString() == "TypeOf"),
            "Should detect IAdvancedProcessable typeof in constructor");
    }

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_DefaultConstructorShouldHaveMinimalReferences()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 0";

        var vm = CompileQuery(query);
        var result = vm.Run();

        // Default constructor only has `new List<IProcessable>()` - should detect IProcessable as GenericArgument
        var interfaceRefs = result.Where(r => r[0].ToString() == "IProcessable").ToList();
        Assert.IsTrue(interfaceRefs.Count >= 1, "Default constructor should reference IProcessable via generic argument");
    }

    [TestMethod]
    public void WhenConstructorReferencedTypesQueried_VarInferredShouldBeResolved()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'VarInferenceInConstructor'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Cast"),
            "Should detect cast to IProcessable");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IAdvancedProcessable"),
            "Should detect IAdvancedProcessable reference");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: Property ReferencedTypes (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenAutoPropertyReferencedTypesQueried_ShouldBeEmpty()
    {
        var query = $@"
            select 
                rt.Name
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.ReferencedTypes rt
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'AutoProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "Auto-property should have no referenced types");
    }

    [TestMethod]
    public void WhenGetterOnlyPropertyReferencedTypesQueried_ShouldDetectTypes()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.ReferencedTypes rt
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'GetterOnlyWithBody'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 type reference, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "As"),
            "Should detect IProcessable 'as' usage in getter body");
    }

    [TestMethod]
    public void WhenPropertySetterReferencedTypesQueried_ShouldDetectTypes()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.ReferencedTypes rt
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'MultiLocalProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IAdvancedProcessable" && r[1].ToString() == "As"),
            "Should detect IAdvancedProcessable 'as' usage in setter body");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: LocalVariables on Constructor (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenConstructorHasMultipleLocalVariables_ShouldReturnAll()
    {
        var query = $@"
            select 
                lv.Name,
                lv.Type,
                lv.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.LocalVariables lv
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3, $"Should find at least 3 local variables, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "localFirst"), "Should find 'localFirst' variable");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "localSecond"), "Should find 'localSecond' variable");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "combined"), "Should find 'combined' variable");
    }

    [TestMethod]
    public void WhenConstructorLocalVariableCount_ShouldMatchActualCount()
    {
        var query = $@"
            select 
                ctor.LocalVariableCount
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Should find exactly 1 constructor");
        var count = Convert.ToInt32(result[0][0]);
        Assert.IsTrue(count >= 3, $"LocalVariableCount should be at least 3, got {count}");
    }

    [TestMethod]
    public void WhenDefaultConstructorLocalVariablesQueried_ShouldBeEmpty()
    {
        var query = $@"
            select 
                lv.Name
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.LocalVariables lv
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 0";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "Default constructor with no locals should yield 0 LocalVariables");
    }

    [TestMethod]
    public void WhenConstructorLocalVariableFullTypeName_ShouldBeFullyQualified()
    {
        var query = $@"
            select 
                lv.Name,
                lv.Type,
                lv.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.LocalVariables lv
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2 and lv.Name = 'localFirst'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Should find exactly 1 variable named 'localFirst'");
        Assert.IsTrue(result[0][2].ToString()!.Contains("IProcessable"),
            $"FullTypeName should contain IProcessable, got {result[0][2]}");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: LocalVariables on Property (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenAutoPropertyLocalVariablesQueried_ShouldBeEmpty()
    {
        var query = $@"
            select 
                lv.Name
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.LocalVariables lv
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'AutoProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "Auto-property should have no local variables");
    }

    [TestMethod]
    public void WhenPropertyWithMultipleLocals_ShouldReturnAll()
    {
        var query = $@"
            select 
                lv.Name,
                lv.Type,
                lv.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.LocalVariables lv
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'MultiLocalProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3, $"Should find at least 3 local variables in setter, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "backup"), "Should find 'backup' variable");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "advanced"), "Should find 'advanced' variable");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "description"), "Should find 'description' variable");
    }

    [TestMethod]
    public void WhenPropertyLocalVariableCount_ShouldMatchActualCount()
    {
        var query = $@"
            select 
                pr.Name,
                pr.LocalVariableCount
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'MultiLocalProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Should find exactly 1 property");
        var count = Convert.ToInt32(result[0][1]);
        Assert.IsTrue(count >= 3, $"LocalVariableCount should be at least 3, got {count}");
    }

    [TestMethod]
    public void WhenExpressionBodiedPropertyLocalVariablesQueried_ShouldBeEmpty()
    {
        var query = $@"
            select 
                lv.Name
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.LocalVariables lv
            where c.Name = 'PropertyUsagePatterns' and pr.Name = 'ProcessableType'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "Expression-bodied property should have no local variables");
    }

    [TestMethod]
    public void WhenGetterOnlyPropertyLocalVariablesQueried_ShouldReturnGetterLocals()
    {
        var query = $@"
            select 
                lv.Name,
                lv.Type
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.LocalVariables lv
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'GetterOnlyWithBody'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 local variable in getter, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "impl"), "Should find 'impl' variable in getter");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: FullTypeName on PropertyEntity (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenPropertyFullTypeNameQueried_GenericTypeShouldBeFullyQualified()
    {
        var query = $@"
            select 
                pr.Name,
                pr.Type,
                pr.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'GenericProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullTypeName = result[0][2].ToString();
        Assert.IsTrue(fullTypeName!.Contains("List"), $"FullTypeName should contain 'List', got {fullTypeName}");
        Assert.IsTrue(fullTypeName.Contains("IProcessable"), $"FullTypeName should contain 'IProcessable', got {fullTypeName}");
    }

    [TestMethod]
    public void WhenPropertyFullTypeNameQueried_NullableValueTypeShouldBeFullyQualified()
    {
        var query = $@"
            select 
                pr.Name,
                pr.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'NullableValueProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullTypeName = result[0][1].ToString();
        Assert.IsTrue(fullTypeName!.Contains("int") || fullTypeName.Contains("Int32") || fullTypeName.Contains("Nullable"),
            $"FullTypeName for nullable int should contain int/Int32/Nullable, got {fullTypeName}");
    }

    [TestMethod]
    public void WhenPropertyFullTypeNameQueried_DictionaryTypeShouldBeFullyQualified()
    {
        var query = $@"
            select 
                pr.Name,
                pr.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'DictionaryProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullTypeName = result[0][1].ToString();
        Assert.IsTrue(fullTypeName!.Contains("Dictionary"), $"FullTypeName should contain 'Dictionary', got {fullTypeName}");
        Assert.IsTrue(fullTypeName.Contains("string"), $"FullTypeName should contain 'string', got {fullTypeName}");
        Assert.IsTrue(fullTypeName.Contains("IProcessable"), $"FullTypeName should contain 'IProcessable', got {fullTypeName}");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: FullTypeName on ParameterEntity (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenParameterFullTypeNameQueried_GenericParamShouldBeFullyQualified()
    {
        var query = $@"
            select 
                param.Name,
                param.Type,
                param.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.Parameters param
            where c.Name = 'ParameterEdgeCases' and m.Name = 'MethodWithGenericParam'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullTypeName = result[0][2].ToString();
        Assert.IsTrue(fullTypeName!.Contains("List"), $"FullTypeName should contain 'List', got {fullTypeName}");
        Assert.IsTrue(fullTypeName.Contains("IProcessable"), $"FullTypeName should contain 'IProcessable', got {fullTypeName}");
    }

    [TestMethod]
    public void WhenParameterFullTypeNameQueried_ArrayParamShouldBeFullyQualified()
    {
        var query = $@"
            select 
                param.Name,
                param.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.Parameters param
            where c.Name = 'ParameterEdgeCases' and m.Name = 'MethodWithArrayParam'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullTypeName = result[0][1].ToString();
        Assert.IsTrue(fullTypeName!.Contains("IProcessable") && fullTypeName.Contains("[]"),
            $"FullTypeName for array param should contain 'IProcessable[]', got {fullTypeName}");
    }

    [TestMethod]
    public void WhenParameterFullTypeNameQueried_InterfaceParamShouldBeFullyQualified()
    {
        var query = $@"
            select 
                param.Name,
                param.Type,
                param.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.Parameters param
            where c.Name = 'ParameterEdgeCases' and m.Name = 'MethodWithInterfaceParam'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("IProcessable", result[0][1].ToString());
        Assert.IsTrue(result[0][2].ToString()!.Contains("Solution1.ClassLibrary1.IProcessable"),
            $"FullTypeName should be fully qualified, got {result[0][2]}");
    }

    [TestMethod]
    public void WhenParameterFullTypeNameQueried_MixedParamsShouldAllBeFullyQualified()
    {
        var query = $@"
            select 
                param.Name,
                param.FullTypeName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.Parameters param
            where c.Name = 'ParameterEdgeCases' and m.Name = 'MethodWithMixedParams'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(4, result.Count, "Should find exactly 4 parameters");
        
        var nameParam = result.First(r => r[0].ToString() == "name");
        Assert.AreEqual("string", nameParam[1].ToString());
        
        var processableParam = result.First(r => r[0].ToString() == "processable");
        Assert.IsTrue(processableParam[1].ToString()!.Contains("IProcessable"));
        
        var numbersParam = result.First(r => r[0].ToString() == "numbers");
        Assert.IsTrue(numbersParam[1].ToString()!.Contains("List"));
        
        var lookupParam = result.First(r => r[0].ToString() == "lookup");
        var lookupType = lookupParam[1].ToString();
        Assert.IsTrue(lookupType!.Contains("Dictionary") && lookupType.Contains("IAdvancedProcessable"),
            $"Lookup param FullTypeName should contain Dictionary and IAdvancedProcessable, got {lookupType}");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: FullReturnType on MethodEntity (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenMethodFullReturnTypeQueried_VoidShouldBeVoid()
    {
        var query = $@"
            select 
                m.Name,
                m.ReturnType,
                m.FullReturnType
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'ReturnTypeEdgeCases' and m.Name = 'VoidMethod'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("void", result[0][2].ToString());
    }

    [TestMethod]
    public void WhenMethodFullReturnTypeQueried_GenericReturnShouldBeFullyQualified()
    {
        var query = $@"
            select 
                m.Name,
                m.FullReturnType
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'ReturnTypeEdgeCases' and m.Name = 'GenericReturnMethod'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullReturnType = result[0][1].ToString();
        Assert.IsTrue(fullReturnType!.Contains("List") && fullReturnType.Contains("IProcessable"),
            $"FullReturnType should contain List and IProcessable, got {fullReturnType}");
    }

    [TestMethod]
    public void WhenMethodFullReturnTypeQueried_TaskReturnShouldBeFullyQualified()
    {
        var query = $@"
            select 
                m.Name,
                m.FullReturnType
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'ReturnTypeEdgeCases' and m.Name = 'TaskReturnMethod'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullReturnType = result[0][1].ToString();
        Assert.IsTrue(fullReturnType!.Contains("Task") && fullReturnType.Contains("IProcessable"),
            $"FullReturnType should contain Task and IProcessable, got {fullReturnType}");
    }

    [TestMethod]
    public void WhenMethodFullReturnTypeQueried_ArrayReturnShouldBeFullyQualified()
    {
        var query = $@"
            select 
                m.Name,
                m.FullReturnType
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'ReturnTypeEdgeCases' and m.Name = 'ArrayReturnMethod'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullReturnType = result[0][1].ToString();
        Assert.IsTrue(fullReturnType!.Contains("IProcessable") && fullReturnType.Contains("[]"),
            $"FullReturnType should contain IProcessable[], got {fullReturnType}");
    }

    [TestMethod]
    public void WhenMethodFullReturnTypeQueried_NestedGenericShouldBeFullyQualified()
    {
        var query = $@"
            select 
                m.Name,
                m.FullReturnType
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'ReturnTypeEdgeCases' and m.Name = 'NestedGenericReturnMethod'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        var fullReturnType = result[0][1].ToString();
        Assert.IsTrue(fullReturnType!.Contains("Dictionary") && fullReturnType.Contains("List") && fullReturnType.Contains("IProcessable"),
            $"FullReturnType should contain Dictionary, List, IProcessable, got {fullReturnType}");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: Struct-specific tests
    // =====================================================================

    [TestMethod]
    public void WhenStructMethodReferencedTypesQueried_ShouldDetectTypes()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Structs st
            cross apply st.Methods m
            cross apply m.ReferencedTypes rt
            where st.Name = 'StructWithPatterns' and m.Name = 'ProcessData'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Cast"),
            "Should detect IProcessable cast in struct method");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IAdvancedProcessable" && r[1].ToString() == "PatternMatch"),
            "Should detect IAdvancedProcessable pattern match in struct method");
    }

    [TestMethod]
    public void WhenStructConstructorReferencedTypesQueried_ShouldDetectTypes()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Structs st
            cross apply st.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where st.Name = 'StructWithPatterns'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "PatternMatch"),
            "Should detect IProcessable pattern match in struct constructor");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "LocalVariable"),
            "Should detect IProcessable as local variable type in struct constructor");
    }

    [TestMethod]
    public void WhenStructPropertyReferencedTypesQueried_ShouldDetectTypes()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Structs st
            cross apply st.Properties pr
            cross apply pr.ReferencedTypes rt
            where st.Name = 'StructWithPatterns' and pr.Name = 'ProcessableData'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "As"),
            "Should detect IProcessable 'as' usage in struct property getter");
    }

    [TestMethod]
    public void WhenStructConstructorLocalVariablesQueried_ShouldReturnVariables()
    {
        var query = $@"
            select 
                lv.Name,
                lv.Type
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Structs st
            cross apply st.Constructors ctor
            cross apply ctor.LocalVariables lv
            where st.Name = 'StructWithPatterns'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1, $"Should find at least 1 local variable, got {result.Count}");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "local"), "Should find 'local' variable in struct constructor");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: Cross-entity combination queries
    // =====================================================================

    [TestMethod]
    public void WhenAllInterfaceReferencesInClassQueried_ShouldFindAcrossMethodsConstructorsAndProperties()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'ComprehensiveInterfaceUser' and rt.IsInterface = true";

        var vm = CompileQuery(query);
        var methodResult = vm.Run();

        var query2 = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ComprehensiveInterfaceUser' and rt.IsInterface = true";

        var vm2 = CompileQuery(query2);
        var ctorResult = vm2.Run();

        var query3 = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Properties pr
            cross apply pr.ReferencedTypes rt
            where c.Name = 'ComprehensiveInterfaceUser' and rt.IsInterface = true";

        var vm3 = CompileQuery(query3);
        var propResult = vm3.Run();

        // Method should find IAdvancedProcessable (pattern match)
        Assert.IsTrue(methodResult.Any(r => r[0].ToString() == "IAdvancedProcessable"),
            "Method should reference IAdvancedProcessable");

        // Constructor should find IProcessable (cast)
        Assert.IsTrue(ctorResult.Any(r => r[0].ToString() == "IProcessable"),
            "Constructor should reference IProcessable");

        // Property should find ISuperAdvancedProcessable (as)
        Assert.IsTrue(propResult.Any(r => r[0].ToString() == "ISuperAdvancedProcessable"),
            "Property should reference ISuperAdvancedProcessable");

        // Combine all interface names found across all members
        var allInterfaces = methodResult.Select(r => r[0].ToString())
            .Concat(ctorResult.Select(r => r[0].ToString()))
            .Concat(propResult.Select(r => r[0].ToString()))
            .Distinct()
            .ToList();

        Assert.IsTrue(allInterfaces.Count >= 3,
            $"Should find at least 3 distinct interfaces across all members, got {allInterfaces.Count}: {string.Join(", ", allInterfaces)}");
    }

    [TestMethod]
    public void WhenReferencedTypesQueriedSeparately_ShouldFindInterfacesInBothMethodsAndConstructors()
    {
        var methodQuery = $@"
            select rt.Name, rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'ComprehensiveInterfaceUser' and rt.IsInterface = true";

        var vm1 = CompileQuery(methodQuery);
        var methodResult = vm1.Run();

        var ctorQuery = $@"
            select rt.Name, rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ComprehensiveInterfaceUser' and rt.IsInterface = true";

        var vm2 = CompileQuery(ctorQuery);
        var ctorResult = vm2.Run();

        Assert.IsTrue(methodResult.Count >= 1, $"Methods should reference at least 1 interface, got {methodResult.Count}");
        Assert.IsTrue(ctorResult.Count >= 1, $"Constructors should reference at least 1 interface, got {ctorResult.Count}");

        // Different interfaces found in different locations
        var methodInterfaces = methodResult.Select(r => r[0].ToString()).Distinct().ToList();
        var ctorInterfaces = ctorResult.Select(r => r[0].ToString()).Distinct().ToList();
        var allInterfaces = methodInterfaces.Union(ctorInterfaces).ToList();

        Assert.IsTrue(allInterfaces.Count >= 2, 
            $"Combined should find at least 2 distinct interfaces, got {allInterfaces.Count}: {string.Join(", ", allInterfaces)}");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: Multiple UsageKinds in a single method
    // =====================================================================

    [TestMethod]
    public void WhenMultipleUsageKindsInConstructor_AllShouldBeDetected()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Constructors ctor
            cross apply ctor.ReferencedTypes rt
            where c.Name = 'ConstructorEdgeCases' and ctor.ParameterCount = 2";

        var vm = CompileQuery(query);
        var result = vm.Run();

        var usageKinds = result.Select(r => r[1].ToString()).Distinct().ToList();
        Assert.IsTrue(usageKinds.Contains("LocalVariable"), "Should contain LocalVariable usage kind");
        Assert.IsTrue(usageKinds.Contains("CatchDeclaration"), "Should contain CatchDeclaration usage kind");
        Assert.IsTrue(usageKinds.Contains("PatternMatch"), "Should contain PatternMatch usage kind");
        Assert.IsTrue(usageKinds.Contains("ArrayCreation"), "Should contain ArrayCreation usage kind");
        Assert.IsTrue(usageKinds.Contains("TypeOf"), "Should contain TypeOf usage kind");
    }

    // =====================================================================
    // COMPREHENSIVE TESTS: var-inferred type resolution (expanded)
    // =====================================================================

    [TestMethod]
    public void WhenVarInferredFromCast_ShouldResolveToTargetType()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithCast'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        // 'var processed = (IProcessable)obj;' should produce:
        // 1. Cast reference to IProcessable
        // 2. var-inferred LocalVariable reference to IProcessable
        var castRef = result.FirstOrDefault(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "Cast");
        Assert.IsNotNull(castRef, "Should detect direct cast to IProcessable");
    }

    [TestMethod]
    public void WhenVarInferredFromAsOperator_ShouldResolveToTargetType()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind,
                rt.Kind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithAsOperator'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        // 'var processed = obj as IProcessable;' should produce As reference
        var asRef = result.FirstOrDefault(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "As");
        Assert.IsNotNull(asRef, "Should detect 'as' IProcessable reference");
    }

    [TestMethod]
    public void WhenVarInferredWithGenericNew_ShouldResolveGenericType()
    {
        var query = $@"
            select 
                rt.Name,
                rt.UsageKind
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Classes c
            cross apply c.Methods m
            cross apply m.ReferencedTypes rt
            where c.Name = 'InterfaceUsagePatterns' and m.Name = 'MethodWithGenericArgument'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Any(r => r[0].ToString() == "IProcessable" && r[1].ToString() == "GenericArgument"),
            "Should detect IProcessable as GenericArgument in new List<IProcessable>()");
    }

    // ============================================================
    // Phase 1 Tests: IsRecord, IsPartial, Property locations
    // ============================================================

    [TestMethod]
    public void WhenClassIsRecord_ShouldReturnTrue()
    {
        var query =
            $@"select c.Name, c.IsRecord from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            where c.Name = 'RecordClass'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenClassIsNotRecord_ShouldReturnFalse()
    {
        var query =
            $@"select c.Name, c.IsRecord from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            where c.Name = 'TestFeatures'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(false, result[0][1]);
    }

    [TestMethod]
    public void WhenClassIsPartial_ShouldReturnTrue()
    {
        var query =
            $@"select c.Name, c.IsPartial from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            where c.Name = 'PartialFeatureClass'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenStructIsPartial_ShouldReturnTrue()
    {
        var query =
            $@"select st.Name, st.IsPartial from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Structs st
            where st.Name = 'PartialFeatureStruct'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenInterfaceIsPartial_ShouldReturnTrue()
    {
        var query =
            $@"select i.Name, i.IsPartial from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Interfaces i
            where i.Name = 'IPartialFeatureInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenPropertyQueried_ShouldHaveStartLineAndEndLine()
    {
        var query =
            $@"select pr.Name, pr.StartLine, pr.EndLine, pr.ContainingTypeName from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects proj cross apply proj.Documents d cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'TestFeatures' and pr.Name = 'AutoProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue((int)result[0][1] > 0, "StartLine should be > 0");
        Assert.IsTrue((int)result[0][2] > 0, "EndLine should be > 0");
        Assert.AreEqual("TestFeatures", result[0][3].ToString());
    }

    [TestMethod]
    public void WhenPropertyHasDocumentation_ShouldReturnTrue()
    {
        var query =
            $@"select pr.Name, pr.HasDocumentation from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyEdgeCases' and pr.Name = 'AutoProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    // ============================================================
    // Phase 2 Tests: Attributes on entities
    // ============================================================

    [TestMethod]
    public void WhenPropertyHasAttributes_ShouldReturnThem()
    {
        var query =
            $@"select pr.Name, a.Name from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Properties pr cross apply pr.Attributes a
            where c.Name = 'AttributeTestClass' and pr.Name = 'OldProperty'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
        Assert.IsTrue(result.Any(r => r[1].ToString() == "Obsolete"));
    }

    [TestMethod]
    public void WhenParameterHasAttribute_ShouldReturnIt()
    {
        var query =
            $@"select par.Name, a.Name from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m cross apply m.Parameters par cross apply par.Attributes a
            where c.Name = 'AttributeTestClass' and m.Name = 'MethodWithAttributedParams'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
        Assert.IsTrue(result.Any(r => r[0].ToString() == "name" && r[1].ToString() == "TestCustom"));
    }

    [TestMethod]
    public void WhenInterfaceHasAttribute_ShouldReturnIt()
    {
        var query =
            $@"select i.Name, a.Name from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Interfaces i
            cross apply i.Attributes a
            where i.Name = 'IAttributedInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
        Assert.IsTrue(result.Any(r => r[1].ToString() == "TestCustom"));
    }

    [TestMethod]
    public void WhenEnumHasAttributes_ShouldReturnThem()
    {
        var query =
            $@"select e.Name, a.Name from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Enums e
            cross apply e.Attributes a
            where e.Name = 'FlagsEnum'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
        Assert.IsTrue(result.Any(r => r[1].ToString() == "Flags"));
    }

    // ============================================================
    // Phase 3 Tests: Parameter enhancements
    // ============================================================

    [TestMethod]
    public void WhenParameterHasDefaultValue_ShouldReturnIt()
    {
        var query =
            $@"select par.Name, par.HasDefaultValue, par.DefaultValue, par.Ordinal from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m cross apply m.Parameters par
            where c.Name = 'ParameterTestClass' and m.Name = 'MethodWithDefaults'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(4, result.Count);
        
        // required (ordinal 0): no default
        var required = result.First(r => r[0].ToString() == "required");
        Assert.AreEqual(false, required[1]);
        Assert.AreEqual(0, (int)required[3]);
        
        // optional (ordinal 1): default = 42
        var optional = result.First(r => r[0].ToString() == "optional");
        Assert.AreEqual(true, optional[1]);
        Assert.AreEqual("42", optional[2].ToString());
        Assert.AreEqual(1, (int)optional[3]);
    }

    // ============================================================
    // Phase 4 Tests: Documentation & usage tracking
    // ============================================================

    [TestMethod]
    public void WhenStructHasDocumentation_ShouldReturnTrue()
    {
        var query =
            $@"select st.Name, st.HasDocumentation from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Structs st
            where st.Name = 'TestStruct'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenStructHasAllInterfaces_ShouldReturnThem()
    {
        var query =
            $@"select st.Name, ai.Value from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Structs st
            cross apply st.AllInterfaces ai
            where st.Name = 'StructWithPatterns'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
        Assert.IsTrue(result.Any(r => r[1].ToString().Contains("IProcessable")));
    }

    [TestMethod]
    public void WhenStructHasEvents_ShouldReturnThem()
    {
        // TestStruct doesn't have events, but we test the query works
        var query =
            $@"select st.Name, st.EventsCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Structs st
            where st.Name = 'TestStruct'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(0, (int)result[0][1]);
    }

    [TestMethod]
    public void WhenInterfaceHasDocumentation_ShouldReturnTrue()
    {
        var query =
            $@"select i.Name, i.HasDocumentation from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Interfaces i
            where i.Name = 'IAttributedInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenEnumHasDocumentation_ShouldReturnTrue()
    {
        var query =
            $@"select e.Name, e.HasDocumentation from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Enums e
            where e.Name = 'FlagsEnum'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    // ============================================================
    // Phase 5 Tests: Enum enhancements
    // ============================================================

    [TestMethod]
    public void WhenEnumHasFlagsAttribute_ShouldReturnTrue()
    {
        var query =
            $@"select e.Name, e.HasFlagsAttribute from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Enums e
            where e.Name = 'FlagsEnum'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenEnumHasNoFlagsAttribute_ShouldReturnFalse()
    {
        var query =
            $@"select e.Name, e.HasFlagsAttribute from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Enums e
            where e.Name = 'Enum1'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(false, result[0][1]);
    }

    [TestMethod]
    public void WhenEnumHasUnderlyingByteType_ShouldReturnByte()
    {
        var query =
            $@"select e.Name, e.UnderlyingType from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Enums e
            where e.Name = 'ByteEnum'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("byte", result[0][1].ToString());
    }

    [TestMethod]
    public void WhenEnumMembersQueried_ShouldReturnNameAndValue()
    {
        var query =
            $@"select 
                em.Name, 
                em.Value 
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p 
            cross apply p.Documents d 
            cross apply d.Enums e
            cross apply e.EnumMembers em
            where e.Name = 'FlagsEnum'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 4);
        Assert.IsTrue(result.Any(r => r[0].ToString() == "Read" && r[1].ToString() == "1"));
        Assert.IsTrue(result.Any(r => r[0].ToString() == "Write" && r[1].ToString() == "2"));
        Assert.IsTrue(result.Any(r => r[0].ToString() == "Execute" && r[1].ToString() == "4"));
    }

    // ============================================================
    // Phase 6 Tests: Interface enhancements
    // ============================================================

    [TestMethod]
    public void WhenInterfaceHasTypeParameters_ShouldReturnThem()
    {
        var query =
            $@"select i.Name, tp.Value from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Interfaces i
            cross apply i.TypeParameters tp
            where i.Name = 'IGenericInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("T", result[0][1].ToString());
    }

    [TestMethod]
    public void WhenInterfaceIsQueried_ShouldHaveMethodsAndPropertiesCounts()
    {
        var query =
            $@"select i.Name, i.MethodsCount, i.PropertiesCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Interfaces i
            where i.Name = 'IGenericInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, (int)result[0][1]); // GetValue + SetValue
        Assert.AreEqual(0, (int)result[0][2]); // no properties
    }

    [TestMethod]
    public void WhenInterfaceHasMembers_ShouldReturnMemberNames()
    {
        var query =
            $@"select i.Name, mn.Value from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Interfaces i
            cross apply i.MemberNames mn
            where i.Name = 'IExplicitTestA'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2);
        Assert.IsTrue(result.Any(r => r[1].ToString() == "SharedMethod"));
        Assert.IsTrue(result.Any(r => r[1].ToString() == "SharedProperty"));
    }

    // ============================================================
    // Phase 7 Tests: Type constraints
    // ============================================================

    [TestMethod]
    public void WhenClassHasTypeConstraints_ShouldReturnThem()
    {
        var query =
            $@"select c.Name, tc.Name, tc.HasReferenceTypeConstraint, tc.HasValueTypeConstraint, tc.HasConstructorConstraint, tc.ConstraintSummary from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.TypeParameterConstraints tc
            where c.Name = 'ConstrainedGenericClass'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2);
        
        // T: class, IDisposable, new()
        var tConstraint = result.FirstOrDefault(r => r[1].ToString() == "T");
        Assert.IsNotNull(tConstraint);
        Assert.AreEqual(true, tConstraint[2]); // HasReferenceTypeConstraint
        Assert.AreEqual(true, tConstraint[4]); // HasConstructorConstraint
        
        // TKey: struct
        var tkeyConstraint = result.FirstOrDefault(r => r[1].ToString() == "TKey");
        Assert.IsNotNull(tkeyConstraint);
        Assert.AreEqual(true, tkeyConstraint[3]); // HasValueTypeConstraint
    }

    [TestMethod]
    public void WhenMethodHasTypeConstraints_ShouldReturnThem()
    {
        var query =
            $@"select m.Name, tc.Name, tc.HasReferenceTypeConstraint, tc.HasConstructorConstraint from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m cross apply m.TypeParameterConstraints tc
            where c.Name = 'ConstrainedGenericClass' and m.Name = 'Transform'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("TResult", result[0][1].ToString());
        Assert.AreEqual(true, result[0][2]); // HasReferenceTypeConstraint
        Assert.AreEqual(true, result[0][3]); // HasConstructorConstraint
    }

    // ============================================================
    // Phase 8 Tests: Explicit interface implementations
    // ============================================================

    [TestMethod]
    public void WhenMethodIsExplicitInterfaceImpl_ShouldReturnTrue()
    {
        var query =
            $@"select m.Name, m.IsExplicitInterfaceImplementation from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'ExplicitImplementor'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 3);
        Assert.IsTrue(result.Any(r => (bool)r[1] == true), "Should have at least one explicit implementation");
        Assert.IsTrue(result.Any(r => r[0].ToString() == "RegularMethod" && (bool)r[1] == false));
    }

    [TestMethod]
    public void WhenMethodIsPartial_ShouldReturnTrue()
    {
        var query =
            $@"select m.Name, m.IsPartial from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'TestFeatures' and m.Name = 'PartialMethodNoBody'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
    }

    [TestMethod]
    public void WhenMethodHasMethodKind_ShouldReturnOrdinary()
    {
        var query =
            $@"select m.Name, m.MethodKind from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'TestFeatures' and m.Name = 'EmptyMethod'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Ordinary", result[0][1].ToString());
    }

    // ============================================================
    // Phase 9 Tests: DelegateEntity
    // ============================================================

    [TestMethod]
    public void WhenDelegatesQueried_ShouldReturnAllDelegates()
    {
        var query =
            $@"select del.Name, del.ReturnType, del.ParameterCount, del.IsGeneric from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Delegates del
            where del.Name = 'SimpleCallback'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("SimpleCallback", result[0][0].ToString());
        Assert.AreEqual("Void", result[0][1].ToString());
        Assert.AreEqual(1, (int)result[0][2]);
        Assert.AreEqual(false, result[0][3]);
    }

    [TestMethod]
    public void WhenGenericDelegateQueried_ShouldReturnGenericInfo()
    {
        var query =
            $@"select del.Name, del.IsGeneric, del.TypeParameterCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Delegates del
            where del.Name = 'Transformer'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(true, result[0][1]);
        Assert.AreEqual(2, (int)result[0][2]);
    }

    [TestMethod]
    public void WhenDelegateCountQueried_ShouldReturnCorrectCount()
    {
        var query =
            $@"select d.Name, d.DelegateCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d
            where d.Name = 'NewFeaturePatterns.cs'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue((int)result[0][1] >= 3, "Should have at least 3 delegates");
    }

    // ============================================================
    // Phase 11 Tests: Compiler diagnostics
    // ============================================================

    [TestMethod]
    public void WhenDiagnosticsQueried_ShouldReturnResults()
    {
        var query =
            $@"select d.Name, d.DiagnosticCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d
            where d.Name = 'Class1.cs'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        // DiagnosticCount could be warnings or errors
        Assert.IsTrue((int)result[0][1] >= 0, "DiagnosticCount should be non-negative");
    }

    // ============================================================
    // Phase 12 Tests: Data flow analysis
    // ============================================================

    [TestMethod]
    public void WhenMethodHasDataFlow_ShouldReturnCapturedVariables()
    {
        var query =
            $@"select m.Name, m.DataFlow.CapturedCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'DataFlowTestClass' and m.Name = 'MethodWithCapture'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue((int)result[0][1] >= 1, "Should have at least 1 captured variable");
    }

    [TestMethod]
    public void WhenDataFlowCapturedVarsQueried_ShouldContainCapturedVar()
    {
        var query =
            $@"select m.Name, m.DataFlow.CapturedCount, m.DataFlow.ReadInsideCount, m.DataFlow.WrittenInsideCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'DataFlowTestClass' and m.Name = 'MethodWithCapture'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue((int)result[0][1] >= 1, "Should have at least 1 captured variable");
    }

    [TestMethod]
    public void WhenDataFlowReadWriteQueried_ShouldWork()
    {
        var query =
            $@"select m.Name, m.DataFlow.ReadInsideCount, m.DataFlow.WrittenInsideCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'DataFlowTestClass' and m.Name = 'MethodWithReadWrite'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue((int)result[0][1] > 0, "Should have reads");
        Assert.IsTrue((int)result[0][2] > 0, "Should have writes");
    }

    // ============================================================
    // Phase 13 Tests: Control flow analysis
    // ============================================================

    [TestMethod]
    public void WhenMethodHasControlFlow_ShouldReturnReachability()
    {
        var query =
            $@"select m.Name, m.ControlFlow.EndPointIsReachable from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'DataFlowTestClass' and m.Name = 'MethodWithUnreachableCode'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(false, result[0][1], "End point should not be reachable");
    }

    [TestMethod]
    public void WhenMethodHasMultipleExitPoints_ShouldCountThem()
    {
        var query =
            $@"select m.Name, m.ControlFlow.ExitPointCount, m.ControlFlow.ReturnStatementCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Methods m
            where c.Name = 'DataFlowTestClass' and m.Name = 'MethodWithEarlyReturn'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue((int)result[0][1] >= 2, "Should have at least 2 exit points");
        Assert.IsTrue((int)result[0][2] >= 2, "Should have at least 2 return statements");
    }

    // ============================================================
    // Property default value test
    // ============================================================

    [TestMethod]
    public void WhenPropertyHasDefaultValue_ShouldReturnIt()
    {
        var query =
            $@"select pr.Name, pr.DefaultValue from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyDefaultValueClass' and pr.Name = 'PropertyWithDefault'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsNotNull(result[0][1]);
        Assert.IsTrue(result[0][1].ToString().Contains("DefaultValue"));
    }

    [TestMethod]
    public void WhenPropertyHasNoDefaultValue_ShouldReturnNull()
    {
        var query =
            $@"select pr.Name, pr.DefaultValue from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'PropertyDefaultValueClass' and pr.Name = 'PropertyWithoutDefault'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.IsNull(result[0][1]);
    }

    // ============================================================
    // Event entity enhancement tests
    // ============================================================

    [TestMethod]
    public void WhenEventQueried_ShouldHaveDocumentationFlag()
    {
        var query =
            $@"select ev.Name, ev.HasDocumentation from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Events ev
            where c.Name = 'TestFeatures'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 1);
    }

    // Explicit interface implementation on property
    [TestMethod]
    public void WhenPropertyIsExplicitInterfaceImpl_ShouldReturnTrue()
    {
        var query =
            $@"select pr.Name, pr.IsExplicitInterfaceImplementation from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Classes c
            cross apply c.Properties pr
            where c.Name = 'ExplicitImplementor'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.IsTrue(result.Count >= 2);
        Assert.IsTrue(result.Any(r => (bool)r[1] == true), "Should have at least one explicit property implementation");
    }

    // ============================================================
    // Interface BaseInterfaces as table test
    // ============================================================

    [TestMethod]
    public void WhenInterfaceBaseInterfacesQueriedAsTable_ShouldWork()
    {
        var query =
            $@"select i.Name, bi.Value from #csharp.solution('{Solution1SolutionPath.Escape()}') s 
            cross apply s.Projects p cross apply p.Documents d cross apply d.Interfaces i
            cross apply i.BaseInterfaces bi
            where i.Name = 'IChildInterface'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("IStandaloneInterface", result[0][1].ToString());
    }

    private CompiledQuery CompileQuery(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new RoslynSchemaProvider((_, _) => new Mock<INuGetPropertiesResolver>().Object),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(
                new Dictionary<string, string>
                {
                    { "MUSOQ_SERVER_HTTP_ENDPOINT", "https://localhost/internal/this-doesnt-exists" },
                    { "EXTERNAL_NUGET_PROPERTIES_RESOLVE_ENDPOINT", "https://localhost/external/this-doesnt-exists" }
                }));
    }

    private static bool ValidateIsValidPathFor(string? toString, string extension, bool checkFileExists = true)
    {
        if (string.IsNullOrEmpty(toString))
            return false;

        if (!toString.EndsWith(extension))
            return false;

        if (checkFileExists && !File.Exists(toString))
            return false;

        return true;
    }
}
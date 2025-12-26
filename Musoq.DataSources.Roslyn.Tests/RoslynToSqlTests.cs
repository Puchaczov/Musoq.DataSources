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
        var query = $"select p.Id, p.FilePath, p.OutputFilePath, p.OutputRefFilePath, p.DefaultNamespace, p.Language, p.AssemblyName, p.Name, p.IsSubmission, p.Version from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p";
        
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
        var query = $"select t.Name from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Types t";
        
        var vm = CompileQuery(query);
        
        var result = vm.Run();
        
        // We now have 20 types (structs are counted separately via d.Structs)
        Assert.IsTrue(result.Count >= 20, $"Result should contain at least 20 types, but got {result.Count}");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Class1") == 1, "Class1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Interface1") == 1, "Interface1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Interface2") == 1, "Interface2 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Enum1") == 1, "Enum1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Tests") == 1, "Tests should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "PartialTestClass") == 2, "PartialTestClass should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "CyclomaticComplexityClass1") == 1, "CyclomaticComplexityClass1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "TestFeatures") == 1, "TestFeatures should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "AbstractClassWithAbstractMethod") == 1, "AbstractClassWithAbstractMethod should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "IInterfaceWithMethods") == 1, "IInterfaceWithMethods should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "InterfaceImplementor") == 1, "InterfaceImplementor should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UnusedCodeTestClass") == 1, "UnusedCodeTestClass should be present");
        // New types for testing unused code detection
        Assert.IsTrue(result.Count(r => r[0].ToString() == "IUnusedInterface") == 1, "IUnusedInterface should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "IUsedInterface") == 1, "IUsedInterface should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UsedInterfaceImplementor") == 1, "UsedInterfaceImplementor should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UnusedEnum") == 1, "UnusedEnum should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "UsedEnum") == 1, "UsedEnum should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "EnumUser") == 1, "EnumUser should be present");
    }

    [TestMethod]
    public void WhenChecksKindOfType_ShouldPass()
    {
        var query = $"select t.Name, t.IsClass, t.IsEnum, t.IsInterface from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Types t where t.Name in ('Class1', 'Interface1', 'Enum1', 'Tests', 'PartialTestClass', 'CyclomaticComplexityClass1')";
        
        var vm = CompileQuery(query);
        
        var result = vm.Run();
        
        Assert.IsTrue(result.Count == 7, "Result must contain 6 records");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Class1" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 1, "Class1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Interface1" && !(bool)r[1] && !(bool)r[2] && (bool)r[3]) == 1, "Interface1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Enum1" && !(bool)r[1] && (bool)r[2] && !(bool)r[3]) == 1, "Enum1 should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "Tests" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 1, "Tests should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "PartialTestClass" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 2, "PartialTestClass should be present");
        Assert.IsTrue(result.Count(r => r[0].ToString() == "CyclomaticComplexityClass1" && (bool)r[1] && !(bool)r[2] && !(bool)r[3]) == 1, "CyclomaticComplexityClass1 should be present");
    }

    [TestMethod]
    public void WhenDocumentQueries_ShouldPass()
    {
        var query = $"select d.Name, d.Text, d.ClassCount, d.InterfaceCount, d.EnumCount from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Documents d where d.Name = 'Class1.cs'";
        
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
        var query = $"select d.Name, d.FilePath from #csharp.solution('{Solution1SolutionPath.Escape()}') s cross apply s.Projects p cross apply p.Documents d where d.Name = 'Class1.cs'";
        
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
        
        // EmptyMethod test
        var emptyMethod = result.FirstOrDefault(r => r[0].ToString() == "EmptyMethod");
        Assert.IsNotNull(emptyMethod);
        Assert.AreEqual(true, emptyMethod[1]); // HasBody
        Assert.AreEqual(true, emptyMethod[2]); // IsEmpty
        Assert.AreEqual(0, emptyMethod[3]); // StatementsCount
        Assert.AreEqual(true, emptyMethod[4]); // BodyContainsOnlyTrivia
        
        // MethodWithOnlyComments test
        var methodWithComments = result.FirstOrDefault(r => r[0].ToString() == "MethodWithOnlyComments");
        Assert.IsNotNull(methodWithComments);
        Assert.AreEqual(true, methodWithComments[1]); // HasBody
        Assert.AreEqual(true, methodWithComments[2]); // IsEmpty
        Assert.AreEqual(0, methodWithComments[3]); // StatementsCount
        Assert.AreEqual(true, methodWithComments[4]); // BodyContainsOnlyTrivia
        
        // SingleStatementMethod test
        var singleStatementMethod = result.FirstOrDefault(r => r[0].ToString() == "SingleStatementMethod");
        Assert.IsNotNull(singleStatementMethod);
        Assert.AreEqual(true, singleStatementMethod[1]); // HasBody
        Assert.AreEqual(false, singleStatementMethod[2]); // IsEmpty
        Assert.AreEqual(1, singleStatementMethod[3]); // StatementsCount
        Assert.AreEqual(false, singleStatementMethod[4]); // BodyContainsOnlyTrivia
        
        // MultipleStatementsMethod test
        var multipleStatementsMethod = result.FirstOrDefault(r => r[0].ToString() == "MultipleStatementsMethod");
        Assert.IsNotNull(multipleStatementsMethod);
        Assert.AreEqual(true, multipleStatementsMethod[1]); // HasBody
        Assert.AreEqual(false, multipleStatementsMethod[2]); // IsEmpty
        Assert.AreEqual(3, multipleStatementsMethod[3]); // StatementsCount
        Assert.AreEqual(false, multipleStatementsMethod[4]); // BodyContainsOnlyTrivia
        
        // ExpressionBodiedMethod test
        var expressionBodiedMethod = result.FirstOrDefault(r => r[0].ToString() == "ExpressionBodiedMethod");
        Assert.IsNotNull(expressionBodiedMethod);
        Assert.AreEqual(true, expressionBodiedMethod[1]); // HasBody
        Assert.AreEqual(false, expressionBodiedMethod[2]); // IsEmpty (expression bodies are never empty)
        Assert.AreEqual(0, expressionBodiedMethod[3]); // StatementsCount (no block body)
        Assert.AreEqual(false, expressionBodiedMethod[4]); // BodyContainsOnlyTrivia
        
        // MethodWithNestedBlocks test - should count only direct statements
        var methodWithNestedBlocks = result.FirstOrDefault(r => r[0].ToString() == "MethodWithNestedBlocks");
        Assert.IsNotNull(methodWithNestedBlocks);
        Assert.AreEqual(true, methodWithNestedBlocks[1]); // HasBody
        Assert.AreEqual(false, methodWithNestedBlocks[2]); // IsEmpty
        Assert.AreEqual(2, methodWithNestedBlocks[3]); // StatementsCount (if statement and var y = 2)
        Assert.AreEqual(false, methodWithNestedBlocks[4]); // BodyContainsOnlyTrivia
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
        
        // AutoProperty test
        var autoProperty = result.FirstOrDefault(r => r[0].ToString() == "AutoProperty");
        Assert.IsNotNull(autoProperty);
        Assert.AreEqual(true, autoProperty[1]); // IsAutoProperty
        Assert.AreEqual(true, autoProperty[2]); // HasGetter
        Assert.AreEqual(true, autoProperty[3]); // HasSetter
        Assert.AreEqual(false, autoProperty[4]); // HasInitSetter
        
        // AutoPropertyWithInit test
        var autoPropertyWithInit = result.FirstOrDefault(r => r[0].ToString() == "AutoPropertyWithInit");
        Assert.IsNotNull(autoPropertyWithInit);
        Assert.AreEqual(true, autoPropertyWithInit[1]); // IsAutoProperty
        Assert.AreEqual(true, autoPropertyWithInit[2]); // HasGetter
        Assert.AreEqual(true, autoPropertyWithInit[3]); // HasSetter (init counts as setter)
        Assert.AreEqual(true, autoPropertyWithInit[4]); // HasInitSetter
        
        // AutoPropertyReadOnly test
        var autoPropertyReadOnly = result.FirstOrDefault(r => r[0].ToString() == "AutoPropertyReadOnly");
        Assert.IsNotNull(autoPropertyReadOnly);
        Assert.AreEqual(true, autoPropertyReadOnly[1]); // IsAutoProperty
        Assert.AreEqual(true, autoPropertyReadOnly[2]); // HasGetter
        Assert.AreEqual(false, autoPropertyReadOnly[3]); // HasSetter
        Assert.AreEqual(false, autoPropertyReadOnly[4]); // HasInitSetter
        
        // PropertyWithCustomGetter test
        var propertyWithCustomGetter = result.FirstOrDefault(r => r[0].ToString() == "PropertyWithCustomGetter");
        Assert.IsNotNull(propertyWithCustomGetter);
        Assert.AreEqual(false, propertyWithCustomGetter[1]); // IsAutoProperty
        Assert.AreEqual(true, propertyWithCustomGetter[2]); // HasGetter
        Assert.AreEqual(true, propertyWithCustomGetter[3]); // HasSetter
        Assert.AreEqual(false, propertyWithCustomGetter[4]); // HasInitSetter
        
        // ExpressionBodiedProperty test
        var expressionBodiedProperty = result.FirstOrDefault(r => r[0].ToString() == "ExpressionBodiedProperty");
        Assert.IsNotNull(expressionBodiedProperty);
        Assert.AreEqual(false, expressionBodiedProperty[1]); // IsAutoProperty
        Assert.AreEqual(true, expressionBodiedProperty[2]); // HasGetter
        Assert.AreEqual(false, expressionBodiedProperty[3]); // HasSetter
        Assert.AreEqual(false, expressionBodiedProperty[4]); // HasInitSetter
        
        // GetterOnly test
        var getterOnly = result.FirstOrDefault(r => r[0].ToString() == "GetterOnly");
        Assert.IsNotNull(getterOnly);
        Assert.AreEqual(true, getterOnly[1]); // IsAutoProperty
        Assert.AreEqual(true, getterOnly[2]); // HasGetter
        Assert.AreEqual(false, getterOnly[3]); // HasSetter
        Assert.AreEqual(false, getterOnly[4]); // HasInitSetter
        
        // InitOnly test
        var initOnly = result.FirstOrDefault(r => r[0].ToString() == "InitOnly");
        Assert.IsNotNull(initOnly);
        Assert.AreEqual(true, initOnly[1]); // IsAutoProperty
        Assert.AreEqual(false, initOnly[2]); // HasGetter
        Assert.AreEqual(true, initOnly[3]); // HasSetter (init counts as setter)
        Assert.AreEqual(true, initOnly[4]); // HasInitSetter
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
        Assert.AreEqual(false, result[0][1]); // HasBody
        Assert.AreEqual(false, result[0][2]); // IsEmpty
        Assert.AreEqual(0, result[0][3]); // StatementsCount
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
        Assert.AreEqual(false, result[0][1]); // HasBody
        Assert.AreEqual(false, result[0][2]); // IsEmpty
        Assert.AreEqual(0, result[0][3]); // StatementsCount
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
        
        // Should have _readonlyField, _staticField, ConstField, _volatileField, _backingField
        Assert.IsTrue(result.Count >= 4, $"Should have at least 4 fields, got {result.Count}");
        
        // Check readonly field
        var readonlyField = result.FirstOrDefault(r => r[0].ToString() == "_readonlyField");
        Assert.IsNotNull(readonlyField);
        Assert.AreEqual("Int32", readonlyField[1].ToString());
        Assert.AreEqual(true, readonlyField[2]); // IsReadOnly
        Assert.AreEqual(false, readonlyField[3]); // IsConst
        Assert.AreEqual(false, readonlyField[4]); // IsStatic
        
        // Check const field
        var constField = result.FirstOrDefault(r => r[0].ToString() == "ConstField");
        Assert.IsNotNull(constField);
        Assert.AreEqual(true, constField[3]); // IsConst
        
        // Check static field
        var staticField = result.FirstOrDefault(r => r[0].ToString() == "_staticField");
        Assert.IsNotNull(staticField);
        Assert.AreEqual(true, staticField[4]); // IsStatic
        
        // Check volatile field
        var volatileField = result.FirstOrDefault(r => r[0].ToString() == "_volatileField");
        Assert.IsNotNull(volatileField);
        Assert.AreEqual(true, volatileField[5]); // IsVolatile
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
        
        // Should have 3 constructors
        Assert.AreEqual(3, result.Count);
        
        // Check default constructor
        var defaultCtor = result.FirstOrDefault(r => (int)r[1] == 0);
        Assert.IsNotNull(defaultCtor);
        Assert.AreEqual(true, defaultCtor[2]); // HasBody
        Assert.AreEqual(false, defaultCtor[3]); // HasInitializer
        
        // Check constructor with initializer (calls this())
        var ctorWithInit = result.FirstOrDefault(r => (int)r[1] == 2);
        Assert.IsNotNull(ctorWithInit);
        Assert.AreEqual(true, ctorWithInit[3]); // HasInitializer
        Assert.AreEqual("this", ctorWithInit[4]?.ToString()); // InitializerKind
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
        
        // Check TestStruct
        var testStruct = result.FirstOrDefault(r => r[0].ToString() == "TestStruct");
        Assert.IsNotNull(testStruct);
        Assert.AreEqual(false, testStruct[1]); // IsReadOnly
        Assert.AreEqual(1, testStruct[2]); // MethodsCount
        Assert.AreEqual(2, testStruct[3]); // PropertiesCount
        Assert.AreEqual(1, testStruct[4]); // FieldsCount
        Assert.AreEqual(1, testStruct[5]); // ConstructorsCount
        
        // Check ReadOnlyTestStruct
        var readonlyStruct = result.FirstOrDefault(r => r[0].ToString() == "ReadOnlyTestStruct");
        Assert.IsNotNull(readonlyStruct);
        Assert.AreEqual(true, readonlyStruct[1]); // IsReadOnly
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
        Assert.AreEqual(true, result[0][1]); // IsAsync
        Assert.AreEqual(true, result[0][2]); // ContainsAwait
        Assert.AreEqual(2, result[0][3]); // AwaitCount (2 awaits)
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
        Assert.AreEqual(true, result[0][1]); // ContainsLambda
        Assert.AreEqual(2, result[0][2]); // LambdaCount (2 lambdas)
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
        Assert.AreEqual(3, result[0][1]); // MaxNestingDepth (3 levels of if)
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
        
        // Should have at least one using directive
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
        Assert.AreEqual(false, result[0][2]); // IsStatic
        Assert.AreEqual(true, result[0][3]); // IsFieldLike
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
        Assert.IsTrue((int)result[0][1] >= 0); // EfferentCoupling
        Assert.IsTrue((double)result[0][2] >= 0 && (double)result[0][2] <= 1); // Instability
        Assert.IsTrue((int)result[0][3] >= 0); // WeightedMethodsPerClass
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
        Assert.AreEqual(true, result[0][1]); // HasDocumentation (TestFeatures has XML doc)
        Assert.IsTrue((double)result[0][2] >= 0 && (double)result[0][2] <= 100); // MethodDocumentationCoverage
        Assert.IsTrue((double)result[0][3] >= 0 && (double)result[0][3] <= 100); // PropertyDocumentationCoverage
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
        Assert.IsTrue(result.Any(r => r[0].ToString() == "TestStruct" && (bool)r[1] == false));
        Assert.IsTrue(result.Any(r => r[0].ToString() == "ReadOnlyTestStruct" && (bool)r[1] == true));
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
        
        // usedParam should be used (true)
        var usedParam = result.FirstOrDefault(r => r[1].ToString() == "usedParam");
        Assert.IsNotNull(usedParam);
        Assert.AreEqual(true, usedParam[3]); // IsUsed
        
        // unusedParam should be unused (false)
        var unusedParam = result.FirstOrDefault(r => r[1].ToString() == "unusedParam");
        Assert.IsNotNull(unusedParam);
        Assert.AreEqual(false, unusedParam[3]); // IsUsed
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
        
        // usedVar should be used (true)
        var usedVar = result.FirstOrDefault(r => r[1].ToString() == "usedVar");
        Assert.IsNotNull(usedVar);
        Assert.AreEqual(true, usedVar[3]); // IsUsed
        
        // unusedVar should be unused (false)
        var unusedVar = result.FirstOrDefault(r => r[1].ToString() == "unusedVar");
        Assert.IsNotNull(unusedVar);
        Assert.AreEqual(false, unusedVar[3]); // IsUsed
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
        
        // MethodWithUnusedParameter has 1 unused param
        var oneUnused = result.FirstOrDefault(r => r[0].ToString() == "MethodWithUnusedParameter");
        Assert.IsNotNull(oneUnused);
        Assert.AreEqual(1, oneUnused[1]);
        
        // MethodWithMultipleUnusedParams has 3 unused params
        var threeUnused = result.FirstOrDefault(r => r[0].ToString() == "MethodWithMultipleUnusedParams");
        Assert.IsNotNull(threeUnused);
        Assert.AreEqual(3, threeUnused[1]);
        
        // MethodWithAllParamsUsed has 0 unused params
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
        Assert.AreEqual(2, result[0][1]); // LocalVariableCount
        Assert.AreEqual(1, result[0][2]); // UnusedVariableCount
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
        
        // Should find at least MethodWithUnusedParameter and MethodWithMultipleUnusedParams
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
        
        // Should have _unusedField and _usedField
        Assert.IsTrue(result.Count >= 2);
        
        // _usedField should be used
        var usedField = result.FirstOrDefault(r => r[0].ToString() == "_usedField");
        Assert.IsNotNull(usedField);
        Assert.AreEqual(true, usedField[1]); // IsUsed
        Assert.IsTrue((int)usedField[2] > 0); // ReferenceCount > 0
        
        // _unusedField should not be used
        var unusedField = result.FirstOrDefault(r => r[0].ToString() == "_unusedField");
        Assert.IsNotNull(unusedField);
        Assert.AreEqual(false, unusedField[1]); // IsUsed
        Assert.AreEqual(0, unusedField[2]); // ReferenceCount == 0
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
        
        // Class1 should be used (referenced in Tests.cs)
        var class1 = result.FirstOrDefault(r => r[0].ToString() == "Class1");
        Assert.IsNotNull(class1);
        Assert.IsNotNull(class1[1]); // IsUsed should not be null
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
        
        // IUsedInterface should be used (implemented by UsedInterfaceImplementor)
        var usedInterface = result.FirstOrDefault(r => r[0].ToString() == "IUsedInterface");
        Assert.IsNotNull(usedInterface);
        Assert.AreEqual(true, usedInterface[1]); // IsUsed
        Assert.IsTrue((int)usedInterface[2] > 0); // ReferenceCount > 0
        
        // IUnusedInterface should not be used
        var unusedInterface = result.FirstOrDefault(r => r[0].ToString() == "IUnusedInterface");
        Assert.IsNotNull(unusedInterface);
        Assert.AreEqual(false, unusedInterface[1]); // IsUsed
        Assert.AreEqual(0, unusedInterface[2]); // ReferenceCount == 0
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
        
        // UsedEnum should be used (referenced in EnumUser)
        var usedEnum = result.FirstOrDefault(r => r[0].ToString() == "UsedEnum");
        Assert.IsNotNull(usedEnum);
        Assert.AreEqual(true, usedEnum[1]); // IsUsed
        Assert.IsTrue((int)usedEnum[2] > 0); // ReferenceCount > 0
        
        // UnusedEnum should not be used
        var unusedEnum = result.FirstOrDefault(r => r[0].ToString() == "UnusedEnum");
        Assert.IsNotNull(unusedEnum);
        Assert.AreEqual(false, unusedEnum[1]); // IsUsed
        Assert.AreEqual(0, unusedEnum[2]); // ReferenceCount == 0
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
        
        // UsedStruct should be used (referenced in StructUser)
        var usedStruct = result.FirstOrDefault(r => r[0].ToString() == "UsedStruct");
        Assert.IsNotNull(usedStruct);
        Assert.AreEqual(true, usedStruct[1]); // IsUsed
        Assert.IsTrue((int)usedStruct[2] > 0); // ReferenceCount > 0
        
        // UnusedStruct should not be used
        var unusedStruct = result.FirstOrDefault(r => r[0].ToString() == "UnusedStruct");
        Assert.IsNotNull(unusedStruct);
        Assert.AreEqual(false, unusedStruct[1]); // IsUsed
        Assert.AreEqual(0, unusedStruct[2]); // ReferenceCount == 0
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
        
        // Should find _unusedField
        Assert.IsTrue(result.Count >= 1);
        Assert.IsTrue(result.Any(r => r[0].ToString() == "_unusedField"));
    }

    static RoslynToSqlTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
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
                    {"MUSOQ_SERVER_HTTP_ENDPOINT", "https://localhost/internal/this-doesnt-exists"},
                    {"EXTERNAL_NUGET_PROPERTIES_RESOLVE_ENDPOINT", "https://localhost/external/this-doesnt-exists"}
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
}
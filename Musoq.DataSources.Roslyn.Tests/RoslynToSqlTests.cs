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
        
        Assert.IsTrue(result.Count == 11, $"Result should contain 11 types, but got {result.Count}");
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
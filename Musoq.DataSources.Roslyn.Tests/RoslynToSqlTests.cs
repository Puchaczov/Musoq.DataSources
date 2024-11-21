using System.Globalization;
using Musoq.Converter;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Roslyn.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class RoslynToSqlTests
{
    [TestMethod]
    public void WhenSolutionQueried_ShouldPass()
    {
        var query = $"select s.Id from #csharp.solution('{Solution1SolutionPath}') s";
        
        var vm = CreateAndRunVirtualMachine(query);

        var result = vm.Run();
        
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(Guid.TryParse(result[0][0].ToString(), out _));
    }
    
    [TestMethod]
    public void WhenProjectQueried_ShouldPass()
    {
        var query = $"select p.Id, p.FilePath, p.OutputFilePath, p.OutputRefFilePath, p.DefaultNamespace, p.Language, p.AssemblyName, p.Name, p.IsSubmission, p.Version from #csharp.solution('{Solution1SolutionPath}') s cross apply s.Projects p";
        
        var vm = CreateAndRunVirtualMachine(query);

        var result = vm.Run();
        
        Assert.AreEqual(2, result.Count);
        
        Assert.IsTrue(Guid.TryParse(result[0][0].ToString(), out _));
        Assert.IsTrue(ValidateIsValidPathFor(result[0][1].ToString(), ".csproj"));
        Assert.IsTrue(ValidateIsValidPathFor(result[0][2].ToString(), ".dll", false));
        Assert.IsTrue(ValidateIsValidPathFor(result[0][3].ToString(), ".dll", false));
        Assert.AreEqual("Solution1.ClassLibrary1", result[0][4].ToString());
        Assert.AreEqual("C#", result[0][5].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1", result[0][6].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1", result[0][7].ToString());
        Assert.IsNotNull(result[0][8]);
        
        Assert.IsTrue(Guid.TryParse(result[1][0].ToString(), out _));
        Assert.IsTrue(ValidateIsValidPathFor(result[1][1].ToString(), ".csproj"));
        Assert.IsTrue(ValidateIsValidPathFor(result[1][2].ToString(), ".dll", false));
        Assert.IsTrue(ValidateIsValidPathFor(result[1][3].ToString(), ".dll", false));
        Assert.AreEqual("Solution1.ClassLibrary1.Tests", result[1][4].ToString());
        Assert.AreEqual("C#", result[1][5].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1.Tests", result[1][6].ToString());
        Assert.AreEqual("Solution1.ClassLibrary1.Tests", result[1][7].ToString());
        Assert.IsNotNull(result[1][8]);
    }

    [TestMethod]
    public void WhenDocumentQueries_ShouldPass()
    {
        var query = $"select d.Name, d.Text, d.ClassCount, d.InterfaceCount, d.EnumCount from #csharp.solution('{Solution1SolutionPath}') s cross apply s.Projects p cross apply p.Documents d where d.Name = 'Class1.cs'";
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
""".Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var result = vm.Run();
        
        Assert.AreEqual(5, result.Count);
        
        Assert.AreEqual("Method1Async", result[0][0].ToString());
        Assert.AreEqual("Task", result[0][1].ToString());
        Assert.AreEqual(0, (result[0][2] as IEnumerable<ParameterEntity> ?? []).Count());
        Assert.AreEqual(1, (result[0][3] as IEnumerable<string> ?? []).Count());
        Assert.IsNotNull(result[0][4]);
        Assert.AreEqual(0, (result[0][5] as IEnumerable<AttributeEntity> ?? []).Count());
        
        Assert.AreEqual("Method2", result[1][0].ToString());
        Assert.AreEqual("Void", result[1][1].ToString());
        Assert.AreEqual(0, (result[1][2] as IEnumerable<ParameterEntity> ?? []).Count());
        Assert.AreEqual(1, (result[1][3] as IEnumerable<string> ?? []).Count());
        Assert.IsNotNull(result[1][4]);
        Assert.AreEqual(0, (result[1][5] as IEnumerable<AttributeEntity> ?? []).Count());
        
        Assert.AreEqual("Method3", result[2][0].ToString());
        Assert.AreEqual("Class1", result[2][1].ToString());
        Assert.AreEqual(0, (result[2][2] as IEnumerable<ParameterEntity> ?? []).Count());
        Assert.AreEqual(1, (result[2][3] as IEnumerable<string> ?? []).Count());
        Assert.IsNotNull(result[2][4]);
        Assert.AreEqual(0, (result[2][5] as IEnumerable<AttributeEntity> ?? []).Count());
        
        Assert.AreEqual("Method3", result[3][0].ToString());
        Assert.AreEqual("Class1", result[3][1].ToString());
        Assert.AreEqual(1, (result[3][2] as IEnumerable<ParameterEntity> ?? []).Count());
        Assert.AreEqual(1, (result[3][3] as IEnumerable<string> ?? []).Count());
        Assert.IsNotNull(result[3][4]);
        Assert.AreEqual(0, (result[3][5] as IEnumerable<AttributeEntity> ?? []).Count());
        
        Assert.AreEqual("Method4", result[4][0].ToString());
        Assert.AreEqual("Enum1", result[4][1].ToString());
        Assert.AreEqual(0, (result[4][2] as IEnumerable<ParameterEntity> ?? []).Count());
        Assert.AreEqual(1, (result[4][3] as IEnumerable<string> ?? []).Count());
        Assert.IsNotNull(result[4][4]);
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var result = vm.Run();
        
        Assert.AreEqual(2, result.Count);
        
        Assert.AreEqual("PartialTestClass_1.cs", result[0][0].ToString());
        Assert.AreEqual("PartialTestClass", result[0][1].ToString());
        Assert.AreEqual(1, result[0][2]);
        Assert.AreEqual("Method1", result[0][3].ToString());
        
        Assert.AreEqual("PartialTestClass_2.cs", result[1][0].ToString());
        Assert.AreEqual("PartialTestClass", result[1][1].ToString());
        Assert.AreEqual(1, result[1][2]);
        Assert.AreEqual("Method2", result[1][3].ToString());
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var result = vm.Run();
        
        Assert.AreEqual(2, result.Count);
        
        Assert.AreEqual("PartialTestClass_1.cs", result[0][0].ToString());
        Assert.AreEqual("PartialTestClass", result[0][1].ToString());
        Assert.AreEqual(1, result[0][2]);
        Assert.AreEqual("Property1", result[0][3].ToString());
        
        Assert.AreEqual("PartialTestClass_2.cs", result[1][0].ToString());
        Assert.AreEqual("PartialTestClass", result[1][1].ToString());
        Assert.AreEqual(1, result[1][2]);
        Assert.AreEqual("Property2", result[1][3].ToString());
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var result = vm.Run();
        
        Assert.AreEqual(2, result.Count);
        
        Assert.AreEqual("Class1", result[0][0].ToString());
        Assert.AreEqual(16, result[0][1]);
        Assert.AreEqual(11, result[0][2]);
        Assert.AreEqual(16, result[0][3]);
        Assert.AreEqual(17, result[0][4]);
            
        Assert.AreEqual("Class1", result[1][0].ToString());
        Assert.AreEqual(21, result[1][1]);
        Assert.AreEqual(11, result[1][2]);
        Assert.AreEqual(21, result[1][3]);
        Assert.AreEqual(17, result[1][4]);
    }
    
    [TestMethod]
    public void WhenLookingForReferenceToInterface_ShouldFind()
    {
        var query = """
                    select r.Name, rd.StartLine, rd.StartColumn, rd.EndLine, rd.EndColumn from #csharp.solution('{Solution1SolutionPath}') s
                    cross apply s.GetInterfacesByNames('Interface1') c
                    cross apply s.FindReferences(c.Self) rd
                    cross apply rd.ReferencedInterfaces r
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
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
                    """.Replace("{Solution1SolutionPath}", Solution1SolutionPath);
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var result = vm.Run();
        
        Assert.AreEqual(1, result.Count);
        
        Assert.AreEqual("Interface1", result[0][0].ToString());
        Assert.AreEqual(67, result[0][1]);
        Assert.AreEqual(11, result[0][2]);
        Assert.AreEqual(67, result[0][3]);
        Assert.AreEqual(16, result[0][4]);
    }

    static RoslynToSqlTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new RoslynSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
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
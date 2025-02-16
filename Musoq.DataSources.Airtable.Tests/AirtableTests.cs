using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AirtableApiClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Airtable.Components;
using Musoq.DataSources.Airtable.Helpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;
using AirtableBase = Musoq.DataSources.Airtable.Sources.Bases.AirtableBase;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Airtable.Tests;

[TestClass]
public class AirtableTests
{
    [TestMethod]
    public void WhenBasesDescriptionRequested_ShouldReturnColumnTypes()
    {
        var api = new Mock<IAirtableApi>();

        api.Setup(f => f.GetBases(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<List<AirtableBase>>
            {
                new()
                {
                    Capacity = 0
                }
            });
        
        var query = "desc #airtable.bases()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(9, table.Count);
        
        Assert.AreEqual("Id", table[0][0]);
        Assert.AreEqual(typeof(string).FullName, table[0][2]);
        
        Assert.AreEqual("Name", table[3][0]);
        Assert.AreEqual(typeof(string).FullName, table[3][2]);
        
        Assert.AreEqual("PermissionLevel", table[6][0]);
        Assert.AreEqual(typeof(string).FullName, table[6][2]);
    }
    
    [TestMethod]
    public void WhenBasesRequested_ShouldReturnBases()
    {
        var api = new Mock<IAirtableApi>();

        api.Setup(f => f.GetBases(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<List<AirtableBase>>
            {
                new()
                {
                    new AirtableBase( "base1", "Base 1", "read"),
                    new AirtableBase( "base2", "Base 2", "write")
                }
            });
        
        var query = "select Id, Name, PermissionLevel from #airtable.bases()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

        Assert.IsTrue(table.Any(r => 
                (string)r.Values[0] == "base1" && 
                (string)r.Values[1] == "Base 1" && 
                (string)r.Values[2] == "read"),
            "Missing base1 record");

        Assert.IsTrue(table.Any(r => 
                (string)r.Values[0] == "base2" && 
                (string)r.Values[1] == "Base 2" && 
                (string)r.Values[2] == "write"),
            "Missing base2 record");
    }
    
    [TestMethod]
    public void WhenBaseRequested_ShouldReturnBases()
    {
        var api = new Mock<IAirtableApi>();

        api.Setup(f => f.GetTables(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<List<AirtableTable>>
            {
                new()
                {
                    new AirtableTable( "table1", "Table 1", "pk1", "description1"),
                    new AirtableTable( "table2", "Table 2", "pk2", "description2")
                }
            });
        
        var query = "select Id, Name, PrimaryFieldId from #airtable.base()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

        Assert.IsTrue(table.Any(r => 
                (string)r.Values[0] == "table1" && 
                (string)r.Values[1] == "Table 1" && 
                (string)r.Values[2] == "pk1"),
            "Missing table1 record");

        Assert.IsTrue(table.Any(r => 
                (string)r.Values[0] == "table2" && 
                (string)r.Values[1] == "Table 2" && 
                (string)r.Values[2] == "pk2"),
            "Missing table2 record");
    }
    
    [TestMethod]
    public void WhenTableRequested_ShouldReturnBases()
    {
        var api = new Mock<IAirtableApi>();

        var fields = new AirtableField[]
        {
            new("field1", "Field1", "singleLineText", "description1"),
            new("field2", "Field2", "multilineText", "description2"),
            new("field3", "Field3", "number", "description3"),
            new("field4", "Field4", "checkbox", "description4"),
            new("field5", "Field5", "date", "description5"),
            new("field6", "Field6", "currency", "description6"),
            new("field7", "Field7", "percent", "description7"),
            new("field8", "Field8", "email", "description8")
        };

        api.Setup(f => f.GetColumns(It.IsAny<IEnumerable<string>>()))
            .Returns(fields);

        api.Setup(f => f.GetRecordsChunks(It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(new List<IReadOnlyList<AirtableRecord>>()
            {
                new []
                {
                    CreateAirtableRecord(fields,"\"single line text\"", "\"long text\"", "1", "true", "\"2020-01-01\"", "29", "0.1", "\"test@test.ok\"")
                }
            });
        
        var query = "select Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8 from #airtable.records('test')";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("single line text", table[0][0]);
        Assert.AreEqual(typeof(string), table[0][0].GetType());
        
        Assert.AreEqual("long text", table[0][1]);
        Assert.AreEqual(typeof(string), table[0][1].GetType());
        
        Assert.AreEqual(1m, table[0][2]);
        Assert.AreEqual(typeof(decimal), table[0][2].GetType());
        
        Assert.AreEqual(true, table[0][3]);
        Assert.AreEqual(typeof(bool), table[0][3].GetType());
        
        Assert.AreEqual("2020-01-01", table[0][4]);
        Assert.AreEqual(typeof(string), table[0][4].GetType());
        
        Assert.AreEqual(29m, table[0][5]);
        Assert.AreEqual(typeof(decimal), table[0][5].GetType());
        
        Assert.AreEqual(0.1m, table[0][6]);
        Assert.AreEqual(typeof(decimal), table[0][6].GetType());
        
        Assert.AreEqual("test@test.ok", table[0][7]);
        Assert.AreEqual(typeof(string), table[0][7].GetType());
    }

    private static AirtableRecord CreateAirtableRecord(AirtableField[] fields, params string[] values)
    {
        Assert.AreEqual(fields.Length, values.Length);

        var dictionary = fields.ToDictionary<AirtableField?, string, object>(field => field.Name, field =>
        {
            var jsonString = values[Array.IndexOf(fields, field)];
            
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement.Clone();

            return root;
        });

        foreach (var key in dictionary.Keys)
        {
            TypeMappingHelpers.MapFromJsonElement(dictionary, key, (JsonElement)dictionary[key]);
        }

        return new AirtableRecord
        {
            Fields = dictionary
        };
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script, IAirtableApi api)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();

        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new AirtableSchema(api));
        
        return InstanceCreatorHelpers.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            mockSchemaProvider.Object, 
            new Dictionary<uint, IReadOnlyDictionary<string, string>>()
            {
                {0, new Dictionary<string, string>
                {
                    {"MUSOQ_AIRTABLE_API_KEY", "NOPE"},
                    {"MUSOQ_AIRTABLE_BASE_ID", "NOPE x2"}
                }}
            });
    }

    static AirtableTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}
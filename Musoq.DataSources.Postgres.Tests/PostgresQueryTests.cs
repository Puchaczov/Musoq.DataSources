using System;
using System.Collections.Generic;
using System.Dynamic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Postgres.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Postgres.Tests;

[TestClass]
public class PostgresQueryTests
{
    [TestMethod]
    public void WhenDescTable_ShouldReturnAllColumns()
    {
        const string script = "desc #postgres.test_table('schema')";
        
        var vm = CreateAndRunVirtualMachineWithResponse<PostgresTestTable>(script);
        var table = vm.Run();
        
        Assert.AreEqual(5, table.Count);
        Assert.AreEqual("Key", table[0][0]);
        Assert.AreEqual("Key.Chars", table[1][0]);
        Assert.AreEqual("Key.Length", table[2][0]);
        Assert.AreEqual("Value", table[3][0]);
        Assert.AreEqual("Id", table[4][0]);
    }
    
    [TestMethod]
    public void WhenOnlyFilteredRowsSelected_ShouldReturnSingle()
    {
        const string script = "select Key, Value, Id from #postgres.test_table('schema')";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, [
            new PostgresTestTable()
            {
                Key = "1",
                Value = 1,
                Id = Guid.Empty
            }
        ]);
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("1", table[0][0]);
        Assert.AreEqual(1L, table[0][1]);
        Assert.AreEqual(Guid.Empty, table[0][2]);
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse<TEntity>(string script, params TEntity[] entities)
    {
        var columnsRows = new dynamic[typeof(TEntity).GetProperties().Length];
        var properties = typeof(TEntity).GetProperties();

        for (var index = 0; index < properties.Length; index++)
        {
            var property = properties[index];
            columnsRows[index] = new ExpandoObject();
            ((IDictionary<string, object>)columnsRows[index])["name"] = property.Name;
            ((IDictionary<string, object>)columnsRows[index])["type"] = MapClrTypeToPostgresType(property.PropertyType);
        }

        var rowsRows = new dynamic[entities.Length];
        
        for (var i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            
            if (entity is null)
                throw new InvalidOperationException("Entity cannot be null.");
            
            rowsRows[i] = new ExpandoObject();
            
            foreach (var property in properties)
            {
                ((IDictionary<string, object?>)rowsRows[i])[property.Name] = property.GetValue(entity);
            }
        }
        
        return InstanceCreator.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            new TestsPostgresSchemaProvider(columnsRows, rowsRows),
            new Dictionary<uint, IReadOnlyDictionary<string, string>>()
            {
                {0, new Dictionary<string, string>()}
            });
    }

    private static object MapClrTypeToPostgresType(Type clrType)
    {
        var typeName = clrType.Name.ToLowerInvariant();
        
        if (string.IsNullOrEmpty(typeName))
        {
            throw new ArgumentNullException(nameof(typeName), "CLR type name cannot be null or empty.");
        }
        
        return typeName switch
        {
            "boolean" => "boolean",
            "byte" => "smallint",
            "sbyte" => "smallint",
            "char" => "character(1)",
            "decimal" => "numeric",
            "double" => "double precision",
            "float" => "real",
            "int16" => "smallint",
            "int32" => "integer",
            "int64" => "bigint",
            "uint16" => "integer",
            "uint32" => "bigint",
            "uint64" => "numeric",
            "string" => "text",
            "datetime" => "timestamp",
            "datetimeoffset" => "timestamptz",
            "timespan" => "interval",
            "guid" => "uuid",
            _ => throw new ArgumentException($"Unsupported CLR type: {typeName}")
        };
    }

    static PostgresQueryTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }

    private class PostgresTestTable
    {
        public string Key { get; set; }
        
        public long Value { get; set; }
        
        public Guid Id { get; set; }
    }
}
# Schema Describe Feature Migration Guide

## ⚠️ Important Notice

**This migration guide is based on the `Musoq.DataSources.Os` plugin implementation.** The patterns, code examples, and test structures shown here are specific to the Os schema and serve as a reference template.

**When migrating other data sources, you MUST:**
- Adapt method names, table names, and entity types to your specific schema
- Adjust the number and types of data source methods based on your plugin's functionality
- Modify test assertions to match your entity's column structure
- Update parameter names and types according to your source class constructors
- Consider your plugin's specific requirements (e.g., API keys, external dependencies)

Do not copy-paste blindly. Use this guide as a pattern reference and adjust all specifics to match the plugin you are migrating.

## Overview

This document describes the implementation of the `GetRawConstructors` methods in schema classes to support the `desc #schema` and `desc #schema.method` query functionality. This allows users to introspect available data sources and their signatures at runtime.

## What We Implemented

The feature enables two types of introspection queries:

1. **`desc #schema`** - Lists all available data source methods in the schema with their parameters
2. **`desc #schema.method`** - Shows the signature of a specific data source method
3. **`desc #schema.method(...args)`** - Describes the table schema (columns) returned by the method with given arguments

## Implementation Pattern

### 1. Schema Class Changes (OsSchema.cs)

#### Required Using Statements

Add the following using statements to your schema class:

```csharp
using System.Collections.Generic;
using System.Linq;
using Musoq.Schema.Helpers;
using Musoq.Schema.Reflection;
```

#### Method 1: GetRawConstructors(string methodName, RuntimeContext)

Override this method to return constructor information for a specific data source method:

```csharp
public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
{
    return methodName.ToLowerInvariant() switch
    {
        FilesTable => [CreateFilesMethodInfo()],
        DirectoriesTable => [CreateDirectoriesMethodInfo()],
        ZipTable => [CreateZipMethodInfo()],
        ProcessesName => [CreateProcessesMethodInfo()],
        DllsTable => [CreateDllsMethodInfo()],
        DirsCompare => [CreateDirsCompareMethodInfo()],
        Metadata => CreateMetadataMethodInfos(), // Returns array for multiple overloads
        _ => throw new NotSupportedException(
            $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
            $"Available data sources: {string.Join(", ", FilesTable, DirectoriesTable, ZipTable, ProcessesName, DllsTable, DirsCompare, Metadata)}")
    };
}
```

**Key Points:**
- Use switch expression with `ToLowerInvariant()` for case-insensitive matching
- Return array of `SchemaMethodInfo` (use array syntax `[...]` for single items)
- Methods with multiple overloads should return multiple `SchemaMethodInfo` objects
- Throw `NotSupportedException` with helpful message listing available data sources

#### Method 2: GetRawConstructors(RuntimeContext)

Override this method to return constructor information for ALL data source methods:

```csharp
public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
{
    var constructors = new List<SchemaMethodInfo>
    {
        CreateFilesMethodInfo(),
        CreateDirectoriesMethodInfo(),
        CreateZipMethodInfo(),
        CreateProcessesMethodInfo(),
        CreateDllsMethodInfo(),
        CreateDirsCompareMethodInfo()
    };
    
    constructors.AddRange(CreateMetadataMethodInfos());
    
    return constructors.ToArray();
}
```

**Key Points:**
- Collect all `SchemaMethodInfo` objects from all data source methods
- Use `List<SchemaMethodInfo>` for easy collection
- Use `AddRange()` for methods with multiple overloads

#### Helper Methods: Creating SchemaMethodInfo

**For Simple Cases (Single Constructor):**

Use `TypeHelper.GetSchemaMethodInfosForType<T>()` from `Musoq.Schema.Helpers`:

```csharp
private static SchemaMethodInfo CreateFilesMethodInfo()
{
    return TypeHelper.GetSchemaMethodInfosForType<FilesSource>(FilesTable)[0];
}

private static SchemaMethodInfo CreateDirectoriesMethodInfo()
{
    return TypeHelper.GetSchemaMethodInfosForType<DirectoriesSource>(DirectoriesTable)[0];
}

private static SchemaMethodInfo CreateZipMethodInfo()
{
    return TypeHelper.GetSchemaMethodInfosForType<ZipSource>(ZipTable)[0];
}

private static SchemaMethodInfo CreateProcessesMethodInfo()
{
    return TypeHelper.GetSchemaMethodInfosForType<ProcessesSource>(ProcessesName)[0];
}

private static SchemaMethodInfo CreateDllsMethodInfo()
{
    return TypeHelper.GetSchemaMethodInfosForType<DllSource>(DllsTable)[0];
}

private static SchemaMethodInfo CreateDirsCompareMethodInfo()
{
    return TypeHelper.GetSchemaMethodInfosForType<CompareDirectoriesSource>(DirsCompare)[0];
}
```

**Key Points:**
- `TypeHelper.GetSchemaMethodInfosForType<T>()` uses reflection to extract constructor information
- Automatically filters out `RuntimeContext` parameter (framework-injected, not user-provided)
- Returns array (take `[0]` for single constructor)
- Uses actual constructor parameter names from source classes

**For Complex Cases (Multiple Overloads or Custom Parameter Mapping):**

Manually create `ConstructorInfo` objects when the user-facing API differs from actual constructor:

```csharp
private static SchemaMethodInfo[] CreateMetadataMethodInfos()
{
    var metadataInfo1 = new ConstructorInfo(
        originConstructorInfo: null!,
        supportsInterCommunicator: false,
        arguments:
        [
            ("directoryOrFile", typeof(string))
        ]
    );
    
    var metadataInfo2 = new ConstructorInfo(
        originConstructorInfo: null!,
        supportsInterCommunicator: false,
        arguments:
        [
            ("pathDirectoryOrFile", typeof(string)),
            ("throwOnMetadataReadError", typeof(bool))
        ]
    );
    
    var metadataInfo3 = new ConstructorInfo(
        originConstructorInfo: null!,
        supportsInterCommunicator: false,
        arguments:
        [
            ("directory", typeof(string)),
            ("useSubDirectories", typeof(bool)),
            ("throwOnMetadataReadError", typeof(bool))
        ]
    );
    
    return
    [
        new SchemaMethodInfo(Metadata, metadataInfo1),
        new SchemaMethodInfo(Metadata, metadataInfo2),
        new SchemaMethodInfo(Metadata, metadataInfo3)
    ];
}
```

**When to Use Manual Approach:**
- Multiple constructor overloads with different signatures
- User-facing API differs from actual source constructor
- Complex parameter transformation in `GetRowSource()`
- Constructor has internal parameters not exposed to users

### 2. Test Class Implementation (OsSchemaDescribeTests.cs)

#### Required Using Statements

```csharp
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Compare.Directories;
using Musoq.DataSources.Os.Dlls;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Os.Metadata;
using Musoq.DataSources.Os.Tests.Utils;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using System.IO;
using System.IO.Compression;
```

#### Test Structure Overview

Create tests for three scenarios:

1. **Schema Listing Tests** - Verify `desc #schema` returns all methods
2. **Method Signature Tests** - Verify `desc #schema.method` returns correct parameters
3. **Table Schema Tests** - Verify `desc #schema.method(...args)` returns correct columns

#### Test Helper Method

```csharp
private CompiledQuery CreateAndRunVirtualMachine(string script)
{
    return InstanceCreatorHelpers.CompileForExecution(
        script, 
        Guid.NewGuid().ToString(), 
        new OsSchemaProvider(), 
        EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
}

static OsSchemaDescribeTests()
{
    Culture.ApplyWithDefaultCulture();
}
```

#### Test Pattern 1: Schema Listing Test

```csharp
[TestMethod]
public void DescSchema_ShouldListAllAvailableMethods()
{
    var query = "desc #os";

    var vm = CreateAndRunVirtualMachine(query);
    var table = vm.Run();

    Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns: Name and up to 3 parameters");
    Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
    Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
    Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
    Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);

    Assert.AreEqual(9, table.Count, "Should have 9 rows (6 unique methods + 3 metadata overloads)");

    var methodNames = table.Select(row => (string)row[0]).ToList();
    
    Assert.AreEqual(1, methodNames.Count(m => m == "files"), "Should contain 'files' method once");
    Assert.AreEqual(1, methodNames.Count(m => m == "directories"), "Should contain 'directories' method once");
    Assert.AreEqual(1, methodNames.Count(m => m == "zip"), "Should contain 'zip' method once");
    Assert.AreEqual(1, methodNames.Count(m => m == "processes"), "Should contain 'processes' method once");
    Assert.AreEqual(1, methodNames.Count(m => m == "dlls"), "Should contain 'dlls' method once");
    Assert.AreEqual(1, methodNames.Count(m => m == "dirscompare"), "Should contain 'dirscompare' method once");
    Assert.AreEqual(3, methodNames.Count(m => m == "metadata"), "Should contain 'metadata' method 3 times (3 overloads)");

    var filesRow = table.First(row => (string)row[0] == "files");
    Assert.AreEqual("path: System.String", (string)filesRow[1]);
    Assert.AreEqual("useSubDirectories: System.Boolean", (string)filesRow[2]);
    Assert.IsNull(filesRow[3], "Third parameter should be null for files method");
}
```

**Key Assertions:**
- Column count and names (Name, Param 0, Param 1, Param 2, ...)
- Total row count (sum of all methods + overloads)
- Each unique method name appears correct number of times
- Verify specific method signatures with parameter types
- Empty parameter cells should be `null` (not empty strings)

#### Test Pattern 2: Method Signature Tests

```csharp
[TestMethod]
public void DescFiles_ShouldReturnMethodSignature()
{
    var query = "desc #os.files";

    var vm = CreateAndRunVirtualMachine(query);
    var table = vm.Run();

    Assert.AreEqual(3, table.Columns.Count(), "Should have 3 columns");
    Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
    Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
    Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);

    Assert.AreEqual(1, table.Count, "Should have exactly 1 row");
    
    var row = table.First();
    Assert.AreEqual("files", (string)row[0]);
    Assert.AreEqual("path: System.String", (string)row[1]);
    Assert.AreEqual("useSubDirectories: System.Boolean", (string)row[2]);
}
```

**Key Points:**
- Test each data source method individually
- Verify column count matches parameter count + 1 (for method name)
- Verify single row returned
- Check method name and parameter types
- Parameter format: `"parameterName: System.TypeName"`

**For Methods with No Parameters:**

```csharp
[TestMethod]
public void DescProcesses_ShouldReturnMethodSignatureWithNoParameters()
{
    var query = "desc #os.processes";

    var vm = CreateAndRunVirtualMachine(query);
    var table = vm.Run();

    Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
    Assert.AreEqual(1, table.Count);
    
    var row = table.First();
    Assert.AreEqual("processes", (string)row[0]);
}
```

**For Methods with Multiple Overloads:**

```csharp
[TestMethod]
public void DescMetadata_ShouldReturnAllOverloads()
{
    var query = "desc #os.metadata";

    var vm = CreateAndRunVirtualMachine(query);
    var table = vm.Run();

    Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns (Name, Param 0, Param 1, Param 2)");
    Assert.AreEqual(3, table.Count, "Should have 3 rows for 3 overloads");
    
    var methodNames = table.Select(row => (string)row[0]).ToList();
    Assert.IsTrue(methodNames.All(name => name == "metadata"), "All rows should be for metadata method");
    
    var overload1 = table.ElementAt(0);
    Assert.AreEqual("metadata", (string)overload1[0]);
    Assert.AreEqual("directoryOrFile: System.String", (string)overload1[1]);
    Assert.IsNull(overload1[2]);
    Assert.IsNull(overload1[3]);
    
    var overload2 = table.ElementAt(1);
    Assert.AreEqual("metadata", (string)overload2[0]);
    Assert.AreEqual("pathDirectoryOrFile: System.String", (string)overload2[1]);
    Assert.AreEqual("throwOnMetadataReadError: System.Boolean", (string)overload2[2]);
    Assert.IsNull(overload2[3]);
    
    var overload3 = table.ElementAt(2);
    Assert.AreEqual("metadata", (string)overload3[0]);
    Assert.AreEqual("directory: System.String", (string)overload3[1]);
    Assert.AreEqual("useSubDirectories: System.Boolean", (string)overload3[2]);
    Assert.AreEqual("throwOnMetadataReadError: System.Boolean", (string)overload3[3]);
}
```

#### Test Pattern 3: Table Schema Tests (With Arguments)

**Important Discovery:** When you provide arguments to `desc`, it returns the **table schema** (columns of the result set), not the method signature.

```csharp
[TestMethod]
public void DescFilesWithArgs_ShouldReturnTableSchema()
{
    var query = "desc #os.files('./Files', false)";

    var vm = CreateAndRunVirtualMachine(query);
    var table = vm.Run();

    Assert.AreEqual(3, table.Columns.Count());
    Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");
    
    var columnNames = table.Select(row => (string)row[0]).ToList();
    var expectedColumns = new[] 
    {
        nameof(FileEntity.Name),
        nameof(FileEntity.FileName),
        nameof(FileEntity.CreationTime),
        nameof(FileEntity.CreationTimeUtc),
        nameof(FileEntity.LastAccessTime),
        nameof(FileEntity.LastAccessTimeUtc),
        nameof(FileEntity.LastWriteTime),
        nameof(FileEntity.LastWriteTimeUtc),
        nameof(FileEntity.Extension),
        nameof(FileEntity.FullPath),
        nameof(FileEntity.DirectoryName),
        nameof(FileEntity.DirectoryPath),
        nameof(FileEntity.Exists),
        nameof(FileEntity.IsReadOnly),
        nameof(FileEntity.Length)
    };
    
    foreach (var expectedColumn in expectedColumns)
    {
        Assert.IsTrue(columnNames.Contains(expectedColumn), 
            $"Should have '{expectedColumn}' column");
    }
}
```

**Key Points:**
- Use `nameof()` for type-safe column name checking
- Import entity classes to use `nameof()`
- Verify all expected columns are present
- For system types (like `DirectoryInfo`, `Process`, `ZipArchiveEntry`), use their nameof
- For custom columns not directly accessible via nameof, use string literals

**For System Types:**

```csharp
[TestMethod]
public void DescDirectoriesWithArgs_ShouldReturnTableSchema()
{
    var query = "desc #os.directories('./Directories', false)";

    var vm = CreateAndRunVirtualMachine(query);
    var table = vm.Run();

    Assert.AreEqual(3, table.Columns.Count());
    Assert.IsTrue(table.Count > 0);
    
    var columnNames = table.Select(row => (string)row[0]).ToList();
    var expectedColumns = new[]
    {
        nameof(DirectoryInfo.FullName),
        nameof(DirectoryInfo.Attributes),
        nameof(DirectoryInfo.CreationTime),
        nameof(DirectoryInfo.CreationTimeUtc),
        nameof(DirectoryInfo.LastAccessTime),
        nameof(DirectoryInfo.LastAccessTimeUtc),
        nameof(DirectoryInfo.LastWriteTime),
        nameof(DirectoryInfo.LastWriteTimeUtc),
        nameof(DirectoryInfo.Exists),
        nameof(DirectoryInfo.Extension),
        nameof(DirectoryInfo.Name),
        nameof(DirectoryInfo.Parent),
        nameof(DirectoryInfo.Root),
        "DirectoryInfo" // Custom column, use string literal
    };
    
    foreach (var expectedColumn in expectedColumns)
    {
        Assert.IsTrue(columnNames.Contains(expectedColumn),
            $"Should have '{expectedColumn}' column");
    }
}
```

#### Additional Test Patterns

**Error Handling Test:**

```csharp
[TestMethod]
public void DescUnknownMethod_ShouldThrowException()
{
    var query = "desc #os.unknownmethod";

    try
    {
        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();
        Assert.Fail("Should have thrown an exception for unknown method");
    }
    catch (Exception ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        Assert.IsTrue(
            message.Contains("unknownmethod", StringComparison.OrdinalIgnoreCase),
            $"Error message should mention the unknown method. Got: {message}");
        Assert.IsTrue(
            message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Available data sources", StringComparison.OrdinalIgnoreCase),
            $"Error message should be helpful. Got: {message}");
    }
}
```

**Type Consistency Test:**

```csharp
[TestMethod]
public void DescSchema_ShouldHaveConsistentColumnTypes()
{
    var query = "desc #os";

    var vm = CreateAndRunVirtualMachine(query);
    var table = vm.Run();

    foreach (var column in table.Columns)
    {
        Assert.AreEqual(typeof(string), column.ColumnType, 
            $"Column '{column.ColumnName}' should be of type string");
    }
}
```

**Comparison Test (Signature vs Schema):**

```csharp
[TestMethod]
public void DescFilesNoArgs_VsWithArgs_ShouldReturnDifferentResults()
{
    var queryNoArgs = "desc #os.files";
    var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
    var tableNoArgs = vmNoArgs.Run();

    var queryWithArgs = "desc #os.files('./Files', false)";
    var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
    var tableWithArgs = vmWithArgs.Run();

    Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count, 
        "Method signature vs table schema should have different row counts");
    
    Assert.AreEqual(1, tableNoArgs.Count);
    Assert.AreEqual("files", (string)tableNoArgs.First()[0]);
    
    Assert.IsTrue(tableWithArgs.Count > 1);
    var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
    Assert.IsTrue(columnNames.Contains(nameof(FileEntity.Name)));
    Assert.IsTrue(columnNames.Contains(nameof(FileEntity.FullPath)));
}
```

## Migration Checklist

### For Schema Class

- [ ] Add using statements: `System.Collections.Generic`, `System.Linq`, `Musoq.Schema.Helpers`, `Musoq.Schema.Reflection`
- [ ] Override `GetRawConstructors(string methodName, RuntimeContext runtimeContext)`
  - [ ] Use switch expression with `ToLowerInvariant()`
  - [ ] Return arrays with helper method calls
  - [ ] Include helpful error message for unknown methods
- [ ] Override `GetRawConstructors(RuntimeContext runtimeContext)`
  - [ ] Collect all SchemaMethodInfo objects
  - [ ] Handle methods with multiple overloads
- [ ] Create helper methods for each data source
  - [ ] Use `TypeHelper.GetSchemaMethodInfosForType<T>()` for simple cases
  - [ ] Use manual `ConstructorInfo` creation for complex cases
- [ ] Remove method body comments (keep XML documentation)

### For Test Class

- [ ] Add using statements for entity types
- [ ] Create helper method `CreateAndRunVirtualMachine()`
- [ ] Create static constructor with culture setup
- [ ] Implement schema listing test
  - [ ] Verify column structure
  - [ ] Verify row count
  - [ ] Verify each method appears correct number of times
- [ ] Implement method signature tests for each data source
  - [ ] Test methods with no parameters
  - [ ] Test methods with parameters
  - [ ] Test methods with multiple overloads
- [ ] Implement table schema tests for each data source
  - [ ] Use `nameof()` for type safety
  - [ ] Verify all columns are present
- [ ] Implement error handling test
- [ ] Implement type consistency tests
- [ ] Implement comparison test (no args vs with args)
- [ ] Remove inline comments (keep assertion messages where helpful)

## Key Design Decisions

### 1. Using TypeHelper vs Custom Reflection

**When to use `TypeHelper.GetSchemaMethodInfosForType<T>()`:**
- Source constructor matches user-facing API
- Single constructor in source class
- Only `RuntimeContext` needs filtering

**When to use manual `ConstructorInfo`:**
- Multiple overloads with different user-facing signatures
- Complex parameter transformation in `GetRowSource()`
- User-facing API differs significantly from constructor

### 2. Parameter Name Accuracy

The reflection-based approach (`TypeHelper`) provides **actual constructor parameter names** from source classes:
- `DirectoriesSource` uses `recursive` not `useSubDirectories`
- `ZipSource` uses `zipPath` not `path`
- `CompareDirectoriesSource` uses `firstDirectory`/`secondDirectory` not `sourceDirectory`/`destinationDirectory`

This is **intentional** - the implementation shows the actual technical names, making it clear what the source code expects.

### 3. Null vs Empty String

Empty parameter cells in the result table are `null`, not empty strings. Always use `Assert.IsNull()` for checking empty parameters.

### 4. Two Query Modes

The `desc` command has two distinct modes:
- **`desc #schema.method`** - Returns method signature (1 row, parameter info)
- **`desc #schema.method(...args)`** - Returns table schema (multiple rows, column info)

These are different features and should be tested separately.

### 5. Using nameof() in Tests

Always use `nameof()` for column names when possible:
- Type-safe
- Refactoring-friendly
- Compiler-checked
- Better IntelliSense

Fall back to string literals only for:
- Custom columns not accessible via nameof
- Columns from complex mappings
- Columns added by framework

## Common Pitfalls

1. **Forgetting RuntimeContext filtering** - `TypeHelper` handles this automatically, but manual implementations must exclude it
2. **Using empty strings instead of null** - Empty parameter cells are null
3. **Not testing overloads** - Methods with multiple signatures need multiple `SchemaMethodInfo` objects
4. **Hardcoding parameter names** - They might differ from what you expect; use reflection or verify actual source code
5. **Not testing both query modes** - Test both `desc #schema.method` and `desc #schema.method(...args)`
6. **Missing error handling** - Always include helpful error messages for unknown methods
7. **Not using nameof()** - Miss out on type safety and refactoring support

## Testing Strategy

### Test Coverage Requirements

For each schema, implement:
1. ✅ 1 test for listing all methods (`desc #schema`)
2. ✅ N tests for individual method signatures (1 per unique method name)
3. ✅ N tests for table schemas (1 per data source that can be instantiated)
4. ✅ 1 test for error handling (unknown method)
5. ✅ 1-2 tests for type consistency
6. ✅ 1 test comparing signature vs schema modes

**Minimum:** ~10-15 tests per schema
**OsSchema example:** 22 tests (7 data sources, multiple overloads, comprehensive coverage)

## Example Migration Workflow

1. **Analyze the schema:**
   - Count data source methods
   - Identify methods with overloads
   - Check source class constructors

2. **Implement GetRawConstructors methods:**
   - Start with simple cases using `TypeHelper`
   - Handle complex cases manually
   - Test each helper method individually
   - Add missing xml doc comment for implemented GetRawConstructors methods like other methods have.
   - It's very important that Create*MethodInfo methods tries to use TypeHelper.GetSchemaMethodInfosForType<T>() first. Only if that is not possible, manual ConstructorInfo creation should be used.

3. **Create test file:**
   - Set up test infrastructure
   - Write schema listing test first
   - Add method signature tests
   - Add table schema tests
   - Add error/consistency tests

4. **Run and refine:**
   - Run tests, fix any failures
   - Verify parameter names match actual constructors
   - Ensure all columns are validated
   - Remove unnecessary comments

5. **Verify:**
   - All tests pass
   - Coverage is comprehensive
   - Error messages are helpful
   - Code is clean and maintainable

## Files Modified in Reference Implementation

- `Musoq.DataSources.Os/OsSchema.cs` - Schema implementation
- `Musoq.DataSources.Os.Tests/OsSchemaDescribeTests.cs` - Comprehensive test suite

Total implementation: ~200 lines in schema, ~400 lines in tests
Time to implement: 2-3 hours including testing and refinement
Test count: 22 tests, all passing

## Summary

This migration enables powerful introspection capabilities for Musoq schemas. By following this guide, other data sources can be migrated with consistency, comprehensive testing, and maintainable code. The pattern established here should be replicated across all schemas in the repository.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public sealed class SeparatedValuesQueryRowArchitectureTests
{
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue = CreateOpCodeMap();

    [TestMethod]
    public void QueryMaterializationHotPath_IlHasNoBoxObjectArrayDelegateOrDynamicDispatch()
    {
        var projectorMethod = typeof(SeparatedValuesQueryRowProjector<ArchitectureRow, ArchitectureMaterializer>)
            .GetMethod(nameof(SeparatedValuesQueryRowProjector<ArchitectureRow, ArchitectureMaterializer>.Materialize))!;
        var materializerMethod = typeof(ArchitectureMaterializer)
            .GetMethod(nameof(ArchitectureMaterializer.Materialize))!
            .MakeGenericMethod(typeof(ArchitectureReader));
        var typedReadMethod = typeof(SeparatedValuesTypedValueReader)
            .GetMethod(nameof(SeparatedValuesTypedValueReader.Read))!
            .MakeGenericMethod(typeof(long?));

        AssertHotPath(projectorMethod);
        AssertHotPath(materializerMethod);
        AssertHotPath(typedReadMethod);
    }

    [TestMethod]
    public void QueryMaterializationSources_KeepConcreteReaderAndObjectFreeCarrierPath()
    {
        var root = FindRepositoryRoot();
        var projector = File.ReadAllText(Path.Combine(
            root,
            "Musoq.DataSources.SeparatedValues",
            "SeparatedValuesQueryRowProjector.cs"));
        var source = File.ReadAllText(Path.Combine(
            root,
            "Musoq.DataSources.SeparatedValues",
            "SeparatedValuesQueryRowSource.cs"));
        var combined = projector + Environment.NewLine + source;

        StringAssert.Contains(projector, "private ref struct SeparatedValuesFieldReader");
        StringAssert.Contains(
            projector,
            "TMaterializer.Materialize<SeparatedValuesFieldReader>(ref reader)");
        Assert.IsFalse(combined.Contains("new object[", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("object?[]", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("System.Reflection", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("System.Linq.Expressions", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("Expression.Compile", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("CreateDelegate", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("DynamicInvoke", StringComparison.Ordinal));
        Assert.IsFalse(projector.Contains("Nullable.GetUnderlyingType(typeof(T))", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("Func<", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("Action<", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EnumDecodePlan_HasNoReflectionParsingBoxingOrPerRowConstruction()
    {
        var root = FindRepositoryRoot();
        var enumPlanPath = Path.Combine(
            root,
            "Musoq.DataSources.SeparatedValues",
            "SeparatedValuesEnumPlan.cs");
        var source = File.ReadAllText(enumPlanPath);
        foreach (var forbidden in new[]
                 {
                     "Enum.Parse",
                     "Enum.ToObject",
                     "Convert.ChangeType",
                     "System.Reflection",
                     "lock (",
                     "CreateDelegate",
                     "DynamicInvoke",
                     "new EnumTypeDescriptor"
                 })
        {
            Assert.IsFalse(source.Contains(forbidden, StringComparison.Ordinal),
                $"Enum decoding plan contains forbidden hot-path marker '{forbidden}'.");
        }

        var decode = typeof(SeparatedValuesEnumPlan).GetMethod(
            nameof(SeparatedValuesEnumPlan.TryDecode),
            BindingFlags.Instance | BindingFlags.Public)!;
        AssertHotPath(decode);
    }

    [TestMethod]
    public void EnumExecutionHotPaths_ContainNoDynamicConversionOrStringParsing()
    {
        var root = FindRepositoryRoot();
        var hotPathFiles = new[]
        {
            "SeparatedValuesEnumPlan.cs",
            "SeparatedValuesRowProcessor.cs",
            "SeparatedValuesQueryRowProjector.cs",
            "SeparatedValuesQueryShapeMapping.cs",
            "SeparatedValuesQueryRowSource.cs"
        };
        var forbidden = new[]
        {
            "Enum.Parse",
            "Enum.ToObject",
            "Convert.ChangeType",
            "System.Reflection",
            "System.Linq.Expressions",
            "CreateDelegate",
            "DynamicInvoke",
            "lock (",
            "new EnumTypeDescriptor"
        };

        foreach (var fileName in hotPathFiles)
        {
            var path = Path.Combine(root, "Musoq.DataSources.SeparatedValues", fileName);
            var source = File.ReadAllText(path);
            foreach (var marker in forbidden)
                Assert.IsFalse(source.Contains(marker, StringComparison.Ordinal), $"{fileName}: {marker}");
        }

        var processor = File.ReadAllText(Path.Combine(
            root,
            "Musoq.DataSources.SeparatedValues",
            "SeparatedValuesRowProcessor.cs"));
        var enumEvaluation = ExtractRegion(
            processor,
            "        private bool EvaluateEnum(",
            "        private static ulong GetEnumRawValue(");
        Assert.IsFalse(enumEvaluation.Contains("field.Decode()", StringComparison.Ordinal));
        Assert.IsFalse(enumEvaluation.Contains("Encoding.UTF8.GetBytes", StringComparison.Ordinal));
        Assert.IsFalse(enumEvaluation.Contains("Convert.", StringComparison.Ordinal));

        var predicateBinding = ExtractRegion(
            processor,
            "    private static void AddTerms(",
            "    private sealed class PredicateTerm");
        Assert.IsFalse(predicateBinding.Contains("new EnumTypeDescriptor", StringComparison.Ordinal));
        Assert.IsFalse(predicateBinding.Contains("Enum.Parse", StringComparison.Ordinal));
        Assert.IsFalse(predicateBinding.Contains("Enum.ToObject", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ProductionAssembly_ContainsNoLegacyRowExecutionArchitecture()
    {
        string[] forbiddenSymbols =
        [
            "SeparatedValuesFromFileRowsSource",
            "SeparatedValuesLegacyRowProjector",
            "SeparatedValuesLegacyProjectionPlan",
            "ISeparatedValuesScanPipeline",
            "ISeparatedValuesParallelScanPipeline",
            "RowSourceBase<object",
            "IChunkWriter<object"
        ];
        var projectDirectory = Path.Combine(FindRepositoryRoot(), "Musoq.DataSources.SeparatedValues");
        var violations = new List<string>();
        foreach (var path in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                     .Where(static path =>
                         !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                         !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            var relativePath = Path.GetRelativePath(projectDirectory, path);
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                foreach (var symbol in forbiddenSymbols)
                {
                    if (line.Contains(symbol, StringComparison.Ordinal))
                        violations.Add($"{relativePath}:{index + 1}: {symbol}");
                }

                if (!line.Contains("object[]", StringComparison.Ordinal) &&
                    !line.Contains("object?[]", StringComparison.Ordinal))
                    continue;

                var allowedNominalMetadata = relativePath == "SeparatedValuesSchema.cs" &&
                                             (line.Contains("params object?[]", StringComparison.Ordinal) ||
                                              line.Contains("object?[] parameters", StringComparison.Ordinal) ||
                                              line.Contains("RowType = typeof(object[])", StringComparison.Ordinal));
                var allowedTableMetadata = relativePath == "SeparatedValuesTable.cs" &&
                                           line.Contains("new(typeof(object[]))", StringComparison.Ordinal);
                if (!allowedNominalMetadata && !allowedTableMetadata)
                    violations.Add($"{relativePath}:{index + 1}: production object-array reference");
            }
        }

        var productionTypes = typeof(SeparatedValuesSchema).Assembly.GetTypes()
            .Where(static type => type.Namespace == typeof(SeparatedValuesSchema).Namespace)
            .ToArray();
        foreach (var type in productionTypes)
        {
            if (ContainsObjectArray(type.BaseType) || type.GetInterfaces().Any(ContainsObjectArray))
                violations.Add($"{type.FullName}: object-array base/interface");
            foreach (var field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (ContainsObjectArray(field.FieldType))
                    violations.Add($"{type.FullName}.{field.Name}: object-array field");
            }
        }

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    [DoNotParallelize]
    public void TypedNumericRead_AfterWarmup_AllocatesNothingPerField()
    {
        foreach (var fieldCount in new[] { 2, 8, 32, 64 })
        {
            var pool = new StructuredStringPool(fieldCount);
            _ = ReadNullableIntegers(fieldCount, 32, pool);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = ReadNullableIntegers(fieldCount, 2048, pool);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, $"{fieldCount} fields allocated on the typed numeric loop.");
            Assert.AreNotEqual(0L, checksum);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ReadNullableIntegers(int fieldCount, int rows, StructuredStringPool pool)
    {
        var checksum = 0L;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < fieldCount; column++)
            {
                ReadOnlySpan<byte> recordBytes = "123"u8;
                var location = new SeparatedValuesFieldLocation(
                    0,
                    recordBytes.Length,
                    false,
                    false,
                    SeparatedValuesEscapeMode.Double,
                    false,
                    (byte)'"',
                    default,
                    true);
                checksum += SeparatedValuesTypedValueReader.Read<int?>(
                    recordBytes,
                    in location,
                    column,
                    System.Globalization.CultureInfo.InvariantCulture,
                    pool).GetValueOrDefault();
            }
        }

        return checksum;
    }

    private static void AssertHotPath(MethodInfo method)
    {
        var instructions = ReadInstructions(method);
        var violations = new List<string>();

        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            if (instruction.OpCode == OpCodes.Box)
                violations.Add($"IL_{instruction.Offset:X4}: box {instruction.Operand}");
            if (instruction.OpCode == OpCodes.Newarr &&
                instruction.Operand is Type elementType &&
                elementType == typeof(object))
                violations.Add($"IL_{instruction.Offset:X4}: newarr object");
            if (instruction.OpCode is { } opcode &&
                (opcode == OpCodes.Calli || opcode == OpCodes.Ldftn || opcode == OpCodes.Ldvirtftn))
            {
                violations.Add($"IL_{instruction.Offset:X4}: {opcode.Name}");
            }

            if (instruction.Operand is not MethodBase called)
                continue;
            var declaringType = called.DeclaringType;
            if (instruction.OpCode == OpCodes.Newobj &&
                declaringType is not null &&
                typeof(Delegate).IsAssignableFrom(declaringType))
            {
                violations.Add($"IL_{instruction.Offset:X4}: delegate construction {called}");
            }

            var calledNamespace = declaringType?.Namespace ?? string.Empty;
            if (calledNamespace.StartsWith("System.Reflection", StringComparison.Ordinal) ||
                calledNamespace.StartsWith("System.Linq.Expressions", StringComparison.Ordinal) ||
                declaringType == typeof(Nullable) && called.Name == nameof(Nullable.GetUnderlyingType) ||
                called.Name is nameof(Delegate.DynamicInvoke) or "CreateDelegate")
            {
                violations.Add($"IL_{instruction.Offset:X4}: dynamic/reflection call {called}");
            }

            if (declaringType?.IsInterface == true &&
                !HasConstrainedPrefix(instructions, index))
            {
                violations.Add($"IL_{instruction.Offset:X4}: unconstrained interface dispatch {called}");
            }

            if (instruction.OpCode == OpCodes.Callvirt &&
                called is MethodInfo { IsVirtual: true, IsFinal: false } &&
                declaringType?.IsInterface != true)
            {
                violations.Add($"IL_{instruction.Offset:X4}: virtual dispatch {called}");
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            $"{method.DeclaringType?.FullName}.{method.Name}{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static bool HasConstrainedPrefix(IReadOnlyList<IlInstruction> instructions, int callIndex)
    {
        for (var index = callIndex - 1; index >= 0; index--)
        {
            var opcode = instructions[index].OpCode;
            if (opcode == OpCodes.Constrained)
                return true;
            if (opcode.OpCodeType != OpCodeType.Prefix)
                return false;
        }

        return false;
    }

    private static IReadOnlyList<IlInstruction> ReadInstructions(MethodInfo method)
    {
        var body = method.GetMethodBody() ??
                   throw new AssertFailedException($"Method '{method}' has no IL body.");
        var il = body.GetILAsByteArray()!;
        var instructions = new List<IlInstruction>();
        var position = 0;
        while (position < il.Length)
        {
            var offset = position;
            short value = il[position++];
            if (value == 0xfe)
                value = (short)(0xfe00 | il[position++]);
            var opcode = OpCodesByValue[value];
            object? operand = null;
            var operandStart = position;

            switch (opcode.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    position += 1;
                    break;
                case OperandType.InlineVar:
                    position += 2;
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.ShortInlineR:
                    position += 4;
                    break;
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                {
                    var token = BitConverter.ToInt32(il, position);
                    position += 4;
                    operand = ResolveToken(method, token);
                    break;
                }
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    position += 8;
                    break;
                case OperandType.InlineSwitch:
                {
                    var count = BitConverter.ToInt32(il, position);
                    position += 4 + count * 4;
                    break;
                }
                default:
                    throw new AssertFailedException(
                        $"Unsupported IL operand '{opcode.OperandType}' at IL_{offset:X4} in '{method}'.");
            }

            if (position > il.Length || position < operandStart)
                throw new AssertFailedException($"Invalid IL at IL_{offset:X4} in '{method}'.");
            instructions.Add(new IlInstruction(offset, opcode, operand));
        }

        return instructions;
    }

    private static string ExtractRegion(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Could not locate source marker '{startMarker}'.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.IsTrue(end > start, $"Could not locate source marker '{endMarker}'.");
        return source[start..end];
    }

    private static MemberInfo? ResolveToken(MethodInfo method, int token)
    {
        try
        {
            return method.Module.ResolveMember(
                token,
                method.DeclaringType?.GetGenericArguments(),
                method.IsGenericMethod ? method.GetGenericArguments() : null);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<short, OpCode> CreateOpCodeMap()
    {
        return typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(OpCode))
            .Select(static field => (OpCode)field.GetValue(null)!)
            .ToDictionary(static opcode => opcode.Value);
    }

    private static bool ContainsObjectArray(Type? type)
    {
        if (type is null)
            return false;
        if (type == typeof(object[]))
            return true;
        return type.IsGenericType && type.GetGenericArguments().Any(ContainsObjectArray);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Musoq.DataSources.sln")))
                return directory.FullName;
        }

        throw new AssertFailedException("Could not locate the Musoq.DataSources repository root.");
    }

    private readonly record struct IlInstruction(int Offset, OpCode OpCode, object? Operand);

    private readonly record struct ArchitectureRow(long? Value);

    private readonly struct ArchitectureMaterializer : IQueryRowMaterializer<ArchitectureRow>
    {
        public static ArchitectureRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(reader.Read<long?>(0));
    }

    private struct ArchitectureReader : IQuerySourceFieldReader
    {
        public T Read<T>(int slot) => default!;
    }
}

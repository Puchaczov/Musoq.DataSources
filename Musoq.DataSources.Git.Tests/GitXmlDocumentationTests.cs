using System.Reflection;
using System.Xml.Linq;
using Musoq.DataSources.Git;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class GitXmlDocumentationTests
{
    [TestMethod]
    public void GeneratedXmlDocumentation_IsWellFormedAndCoversPublicApi()
    {
        var assembly = typeof(GitSchema).Assembly;
        var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");

        Assert.IsTrue(File.Exists(xmlPath), $"Generated XML documentation was not found at '{xmlPath}'.");

        var document = XDocument.Load(xmlPath, LoadOptions.SetLineInfo);
        var members = document.Root?.Element("members")?.Elements("member")
            .ToDictionary(member => member.Attribute("name")!.Value, StringComparer.Ordinal)
            ?? throw new AssertFailedException("Generated XML documentation has no members element.");

        Assert.IsTrue(members.Count > 0, "Generated XML documentation contains no members.");

        var missing = new List<string>();
        foreach (var type in assembly.GetExportedTypes())
        {
            var typeName = XmlTypeName(type);
            var displayTypeName = type.FullName ?? type.Name;
            RequireEntry($"T:{typeName}", displayTypeName);

            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                RequirePrefix($"M:{typeName}.#ctor", constructor.ToString() ?? displayTypeName);

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                RequireEntry($"P:{typeName}.{property.Name}", property.ToString() ?? displayTypeName);

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                RequireEntry($"F:{typeName}.{field.Name}", field.ToString() ?? displayTypeName);

            foreach (var eventInfo in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                RequireEntry($"E:{typeName}.{eventInfo.Name}", eventInfo.ToString() ?? displayTypeName);

            foreach (var methodGroup in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(method => !method.IsSpecialName)
                         .GroupBy(method => method.Name, StringComparer.Ordinal))
            {
                var prefix = $"M:{typeName}.{methodGroup.Key}";
                var matchingCount = members.Keys.Count(memberName =>
                    memberName.StartsWith(prefix, StringComparison.Ordinal) &&
                    (memberName.Length == prefix.Length ||
                     memberName[prefix.Length] == '(' || memberName[prefix.Length] == '`'));
                if (matchingCount < methodGroup.Count())
                    missing.Add($"{prefix} ({methodGroup.Count()} public overloads, {matchingCount} XML entries)");
            }
        }

        if (missing.Count > 0)
            Assert.Fail("Missing public Git XML documentation entries:\n" + string.Join("\n", missing));

        foreach (var member in members.Values.Where(member =>
                     member.Attribute("name")?.Value.StartsWith("T:Musoq.DataSources.Git", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("M:Musoq.DataSources.Git", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("P:Musoq.DataSources.Git", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("F:Musoq.DataSources.Git", StringComparison.Ordinal) == true ||
                     member.Attribute("name")?.Value.StartsWith("E:Musoq.DataSources.Git", StringComparison.Ordinal) == true))
        {
            Assert.IsTrue(
                member.Element("summary") is not null || member.Element("inheritdoc") is not null,
                $"Git XML member '{member.Attribute("name")?.Value}' has neither a summary nor inheritdoc element.");
        }

        void RequireEntry(string exactName, string displayName)
        {
            if (!members.ContainsKey(exactName))
                missing.Add($"{exactName} ({displayName})");
        }

        void RequirePrefix(string prefix, string displayName)
        {
            if (!members.Keys.Any(memberName => memberName.StartsWith(prefix, StringComparison.Ordinal)))
                missing.Add($"{prefix} ({displayName})");
        }
    }

    private static string XmlTypeName(Type type) => (type.FullName ?? type.Name).Replace('+', '.');
}

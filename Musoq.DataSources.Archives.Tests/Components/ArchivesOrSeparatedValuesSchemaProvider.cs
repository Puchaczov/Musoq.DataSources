using System;
using Musoq.DataSources.SeparatedValues;
using Musoq.Schema;

namespace Musoq.DataSources.Archives.Tests.Components;

public class ArchivesOrSeparatedValuesSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        if (schema == "#separatedvalues")
            return new SeparatedValuesSchema();

        if (schema == "#archives")
            return new ArchivesSchema();
        
        throw new NotSupportedException($"There is no schema with name '{schema}'.");
    }
}
﻿using Musoq.Schema;

namespace Musoq.DataSources.Os.Tests
{
    internal class OsSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new OsSchema();
        }
    }
}
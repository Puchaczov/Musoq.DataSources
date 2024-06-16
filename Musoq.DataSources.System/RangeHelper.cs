using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.System
{
    internal static class RangeHelper
    {
        public static readonly IDictionary<string, int> RangeToIndexMap;
        public static readonly IDictionary<int, Func<RangeItemEntity, object>> RangeToMethodAccessMap;
        public static readonly ISchemaColumn[] RangeColumns;

        static RangeHelper()
        {
            RangeToIndexMap = new Dictionary<string, int>
            {
                {nameof(RangeItemEntity.Value), 0}
            };

            RangeToMethodAccessMap = new Dictionary<int, Func<RangeItemEntity, object>>
            {
                {0, info => info.Value}
            };

            RangeColumns =
            [
                new SchemaColumn(nameof(RangeItemEntity.Value), 0, typeof(long))
            ];
        }
    }
}
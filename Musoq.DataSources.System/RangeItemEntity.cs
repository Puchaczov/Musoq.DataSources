using Musoq.Schema.Attributes;

namespace Musoq.DataSources.System
{
    internal class RangeItemEntity
    {
        [EntityProperty]
        public long Value { get; set; }
    }
}
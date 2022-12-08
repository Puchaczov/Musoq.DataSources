using Musoq.Schema.Attributes;

namespace Musoq.DataSources.System
{
    public class RangeItemEntity
    {
        [EntityProperty]
        public long Value { get; set; }
    }
}
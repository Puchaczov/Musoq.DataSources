using Musoq.Schema.Attributes;

namespace Musoq.DataSources.FlatFile
{
    public class FlatFileEntity
    {
        [EntityProperty]
        public string Line { get; set; }

        [EntityProperty]
        public int LineNumber { get; set; }
    }
}
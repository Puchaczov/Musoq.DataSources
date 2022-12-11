using Musoq.Schema.Attributes;

namespace Musoq.DataSources.FlatFile
{
    /// <summary>
    /// Flat file entity that represents a row in a flat file
    /// </summary>
    public class FlatFileEntity
    {
        /// <summary>
        /// Line text
        /// </summary>
        [EntityProperty]
        public string Line { get; set; }

        /// <summary>
        /// Line number
        /// </summary>
        [EntityProperty]
        public int LineNumber { get; set; }
    }
}
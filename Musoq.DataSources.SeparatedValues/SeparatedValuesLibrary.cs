using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Musoq.DataSources.SeparatedValues
{
    /// <summary>
    /// Separated values schema helper methods
    /// </summary>
    public class SeparatedValuesLibrary : LibraryBase
    {
        private readonly IDictionary<string, IDictionary<string, string>> _fileNameToClusteredWordsMapDictionary =
            new Dictionary<string, IDictionary<string, string>>();

        /// <summary>
        /// Categorize values based on provided file
        /// </summary>
        /// <param name="dictionaryFilePath">Dictionary file path</param>
        /// <param name="value">Value to be categorized</param>
        /// <returns>Category</returns>
        [BindableMethod]
        public string ClusteredByContainsKey(string dictionaryFilePath, string value)
        {
            if (!_fileNameToClusteredWordsMapDictionary.ContainsKey(dictionaryFilePath))
            {
                _fileNameToClusteredWordsMapDictionary.Add(dictionaryFilePath, new Dictionary<string, string>());

                using var stream = File.OpenRead(dictionaryFilePath);
                var map = _fileNameToClusteredWordsMapDictionary[dictionaryFilePath];
                var currentKey = string.Empty;
                using var reader = new StreamReader(stream);
                
                while (!reader.EndOfStream)
                {
                    var line = reader
                        .ReadLine()
                        ?.ToLowerInvariant()
                        .Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.EndsWith(":"))
                        currentKey = line.Substring(0, line.Length - 1);
                    else
                        map.Add(line, currentKey);
                }
            }

            value = value.ToLowerInvariant();

            var dict = _fileNameToClusteredWordsMapDictionary[dictionaryFilePath];
            var newValue = dict.FirstOrDefault(f => value.Contains(f.Key)).Value;

            return newValue ?? "other";
        }
    }
}
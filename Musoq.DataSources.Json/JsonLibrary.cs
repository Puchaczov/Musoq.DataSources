using System.Text;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Newtonsoft.Json.Linq;

namespace Musoq.DataSources.Json
{
    /// <summary>
    /// Json helper methods
    /// </summary>
    public class JsonLibrary : LibraryBase
    {
        /// <summary>
        /// Gets the length of the array
        /// </summary>
        /// <param name="array">Json array</param>
        /// <returns>Length of json</returns>
        [BindableMethod]
        public int Length(JArray array)
        {
            return array.Count;
        }

        /// <summary>
        /// Flattening the array
        /// </summary>
        /// <param name="array">Json array</param>
        /// <returns>Flattened array</returns>
        [BindableMethod]
        public string MakeFlat(JArray array)
        {
            var cnt = array.Count;

            if (cnt == 0)
                return string.Empty;

            var flattedArray = new StringBuilder();

            for (var i = 0; i < cnt - 1; i++)
            {
                flattedArray.Append(array[i]);
                flattedArray.Append(", ");
            }

            var last = array.Count - 1;
            flattedArray.Append(array[last]);

            return flattedArray.ToString();
        }
    }
}
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Helpers
{
    internal class SpdxLicenseExpressionEvaluator
    {
        public static async Task<List<string>> GetLicenseIdentifiersAsync(string expression)
        {
            var licenseIds = new List<string>();

            // Basic splitting by OR/AND (can be improved with a proper parser)
            var parts = Regex.Split(expression, @"\s+(?:OR|AND)\s+", RegexOptions.IgnoreCase);

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim('(', ')', ' ');
                if (!string.IsNullOrWhiteSpace(trimmedPart))
                {
                    licenseIds.Add(trimmedPart);
                }
            }

            return licenseIds;
        }

        public async Task<string> GetLicenseContentAsync(string licenseId)
        {
            return $"Resolved content for license: {licenseId}";
        }
    }
}

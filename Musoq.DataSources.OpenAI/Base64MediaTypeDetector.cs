using System.Text;
using System.Text.RegularExpressions;

namespace Musoq.DataSources.OpenAI;

internal class Base64MediaTypeDetector
{
    public static string DetectMimeType(string base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String)) throw new ArgumentNullException(nameof(base64String));

        var base64 = Regex.Replace(base64String, @"^data:.*,", "");
        var bytes = Convert.FromBase64String(base64);

        if (bytes.Length >= 2)
        {
            if (bytes is [0xFF, 0xD8, 0xFF, ..]) return "image/jpeg";

            if (bytes is [0x89, 0x50, 0x4E, 0x47, ..]) return "image/png";

            if (bytes is [0x47, 0x49, 0x46, ..]) return "image/gif";

            if (bytes[0] == 0x42 && bytes[1] == 0x4D) return "image/bmp";

            if ((bytes[0] == 0x49 && bytes[1] == 0x49) || (bytes[0] == 0x4D && bytes[1] == 0x4D)) return "image/tiff";

            if (bytes is [0x25, 0x50, 0x44, 0x46, ..]) return "application/pdf";

            if (bytes is [0x52, 0x49, 0x46, 0x46, ..]) return "image/webp";
        }


        var decodedStart = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 256)).ToArray());
        if (decodedStart.Contains("<?xml", StringComparison.OrdinalIgnoreCase) &&
            decodedStart.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            return "image/svg+xml";

        throw new NotSupportedException("Unknown media type.");
    }
}
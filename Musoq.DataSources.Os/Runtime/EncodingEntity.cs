using System.Text;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class EncodingEntity(EncodingInfo encodingInfo)
{
    private readonly Encoding _encoding = encodingInfo.GetEncoding();

    public string Name => encodingInfo.Name;
    public string WebName => _encoding.WebName;
    public int CodePage => encodingInfo.CodePage;
    public string EncodingName => _encoding.EncodingName;
    public string BodyName => _encoding.BodyName;
    public string HeaderName => _encoding.HeaderName;
    public bool IsSingleByte => _encoding.IsSingleByte;
}

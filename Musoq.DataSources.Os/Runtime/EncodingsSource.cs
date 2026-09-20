using System.Collections.Generic;
using System.Linq;
using System.Text;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class EncodingsSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<EncodingEntity>(executionContext, "encodings")
{
    static EncodingsSource()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override IEnumerable<EncodingEntity> GetRows()
    {
        return Encoding.GetEncodings()
            .OrderBy(static encoding => encoding.CodePage)
            .Select(static encoding => new EncodingEntity(encoding));
    }
}

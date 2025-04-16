using System;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal interface IPackageVersionConcurrencyManager
{
    Task<IDisposable> AcquireLockAsync(string packageId, string version, CancellationToken cancellationToken);
}
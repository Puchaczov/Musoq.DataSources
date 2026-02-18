using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal interface INuGetResourceVisitor
{
    Task VisitLicensesAsync(CancellationToken cancellationToken);

    Task VisitProjectUrlAsync(CancellationToken cancellationToken);

    Task VisitTitleAsync(CancellationToken cancellationToken);

    Task VisitAuthorsAsync(CancellationToken cancellationToken);

    Task VisitOwnersAsync(CancellationToken cancellationToken);

    Task VisitRequireLicenseAcceptanceAsync(CancellationToken cancellationToken);

    Task VisitDescriptionAsync(CancellationToken cancellationToken);

    Task VisitSummaryAsync(CancellationToken cancellationToken);

    Task VisitReleaseNotesAsync(CancellationToken cancellationToken);

    Task VisitCopyrightAsync(CancellationToken cancellationToken);

    Task VisitLanguageAsync(CancellationToken cancellationToken);

    Task VisitTagsAsync(CancellationToken cancellationToken);
}
namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Collects the set of public URLs (Content + Media) that should be scanned.
/// </summary>
public interface IUrlCollectorService
{
    /// <summary>
    /// Enumerates all published Content URLs (per culture) and Media URLs of the site.
    /// Nodes without a public/routable URL are skipped. Per-node failures are logged and skipped.
    /// </summary>
    IReadOnlyList<ScanTarget> CollectTargets(CancellationToken cancellationToken = default);
}

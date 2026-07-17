namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Orchestrates URL collection, fetching/analysis and report persistence.
/// </summary>
public interface ISiteScanService
{
    /// <summary><c>true</c> while a full scan or error re-scan is in progress.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Runs a full scan of all collected Content/Media URLs. When <paramref name="trigger"/> is
    /// <see cref="ScanTrigger.Scheduled"/>, URLs already in the error list are skipped.
    /// Returns the created run id, or <c>null</c> if the feature is disabled or a scan is already running.
    /// </summary>
    Task<Guid?> RunScanAsync(ScanTrigger trigger, int? userId, CancellationToken cancellationToken = default);

    /// <summary>Re-scans only the URLs currently in the error list. Returns the created run id.</summary>
    Task<Guid?> RescanErrorsAsync(int? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-scans a single URL and updates the error list accordingly (removed on success, upserted on failure).
    /// </summary>
    Task<ScanResultRow> RescanUrlAsync(string url, int? userId, CancellationToken cancellationToken = default);
}

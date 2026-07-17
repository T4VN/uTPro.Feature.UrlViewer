namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Filter for querying scan results.
/// </summary>
public enum ScanResultFilter
{
    All = 0,
    IssuesOnly = 1,
    FailuresOnly = 2,
    SpamOnly = 3,
    CloakingOnly = 4
}

/// <summary>
/// Durable persistence for scan runs, results and the standing error-URL list.
/// All access uses short-lived scopes so no long-running transaction is held.
/// </summary>
public interface IScanReportStore
{
    /// <summary>Inserts a new run header in the <see cref="ScanRunState.Running"/> state.</summary>
    void CreateRun(ScanRunSummary run);

    /// <summary>Updates a run header on completion (state + counts + end time).</summary>
    void CompleteRun(ScanRunSummary run);

    /// <summary>Persists a single result row (one short scope).</summary>
    void AddResult(ScanResultRow row);

    /// <summary>Returns the most recent runs, newest first.</summary>
    IReadOnlyList<ScanRunSummary> GetRuns(int limit);

    /// <summary>Returns a single run header, or <c>null</c>.</summary>
    ScanRunSummary? GetRun(Guid runId);

    /// <summary>Returns the results of a run, optionally filtered.</summary>
    IReadOnlyList<ScanResultRow> GetResults(Guid runId, ScanResultFilter filter);

    /// <summary>Returns the current standing error-URL list, worst (most failures) first.</summary>
    IReadOnlyList<ErrorUrlEntry> GetErrors();

    /// <summary>Adds or updates an error entry for a failed URL.</summary>
    void UpsertError(ScanResultRow failure);

    /// <summary>Removes an URL from the error list (e.g. after a successful re-scan).</summary>
    void RemoveError(string url);

    /// <summary>Deletes run headers/results beyond the configured retention limit.</summary>
    void PruneRuns(int maxRunHistory);
}

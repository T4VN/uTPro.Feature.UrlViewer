namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>Origin of a scan target.</summary>
public enum ScanTargetType
{
    Content = 0,
    Media = 1
}

/// <summary>How a scan run was triggered.</summary>
public enum ScanTrigger
{
    Scheduled = 0,
    Manual = 1,
    ErrorRescan = 2
}

/// <summary>Lifecycle state of a scan run.</summary>
public enum ScanRunState
{
    Running = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3
}

/// <summary>
/// A single URL to scan plus the metadata describing where it came from.
/// </summary>
public sealed class ScanTarget
{
    public required string Url { get; init; }
    public required ScanTargetType Type { get; init; }
    public required Guid NodeKey { get; init; }
    public string? Culture { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// Summary outcome of scanning a single target (persisted, no full HTML).
/// </summary>
public sealed class ScanResultRow
{
    public Guid RunId { get; init; }
    public required string Url { get; init; }
    public ScanTargetType Type { get; init; }
    public Guid NodeKey { get; init; }
    public string? Culture { get; init; }
    public string? Name { get; init; }

    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? FinalUrl { get; init; }
    public int RedirectCount { get; init; }
    public bool HasSpam { get; init; }
    public bool HasCloaking { get; init; }
    public int JsErrorCount { get; init; }
    public long ElapsedMs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime ScannedUtc { get; init; }

    /// <summary>True when this result represents a problem worth surfacing (computed at scan time).</summary>
    public bool IsIssue { get; init; }

    /// <summary>Evaluates whether a result is a problem, given the redirect warning threshold.</summary>
    public static bool Evaluate(bool success, int statusCode, bool hasSpam, bool hasCloaking, int jsErrorCount, int redirectCount, int redirectWarningThreshold) =>
        !success
        || statusCode is >= 400 or 0
        || hasSpam
        || hasCloaking
        || jsErrorCount > 0
        || redirectCount >= redirectWarningThreshold;
}

/// <summary>
/// Aggregated view of a completed (or running) scan run.
/// </summary>
public sealed class ScanRunSummary
{
    public Guid RunId { get; init; }
    public ScanTrigger Trigger { get; init; }
    public int? InitiatingUserId { get; init; }
    public DateTime StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
    public ScanRunState State { get; init; }
    public int TotalTargets { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public int IssueCount { get; init; }
}

/// <summary>
/// An URL currently in the error list (survives across runs, re-scanned on demand).
/// </summary>
public sealed class ErrorUrlEntry
{
    public required string Url { get; init; }
    public ScanTargetType Type { get; init; }
    public Guid NodeKey { get; init; }
    public string? Culture { get; init; }
    public string? Name { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int FailureCount { get; init; }
    public DateTime FirstFailedUtc { get; init; }
    public DateTime LastFailedUtc { get; init; }
}

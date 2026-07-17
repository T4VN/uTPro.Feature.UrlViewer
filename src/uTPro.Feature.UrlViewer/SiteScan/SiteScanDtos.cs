using NPoco;

namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>NPoco runtime DTO for a scan run header (<c>uTProUrlScanRun</c>).</summary>
[TableName("uTProUrlScanRun")]
[PrimaryKey("id", AutoIncrement = true)]
public class ScanRunDto
{
    [Column("id")]
    public int Id { get; set; }

    [Column("RunId")]
    public string RunId { get; set; } = string.Empty;

    [Column("Trigger")]
    public string Trigger { get; set; } = string.Empty;

    [Column("InitiatingUserId")]
    public int? InitiatingUserId { get; set; }

    [Column("StartUtc")]
    public DateTime StartUtc { get; set; }

    [Column("EndUtc")]
    public DateTime? EndUtc { get; set; }

    [Column("State")]
    public string State { get; set; } = string.Empty;

    [Column("TotalTargets")]
    public int TotalTargets { get; set; }

    [Column("SuccessCount")]
    public int SuccessCount { get; set; }

    [Column("FailureCount")]
    public int FailureCount { get; set; }

    [Column("IssueCount")]
    public int IssueCount { get; set; }
}

/// <summary>NPoco runtime DTO for a single scanned URL result (<c>uTProUrlScanResult</c>).</summary>
[TableName("uTProUrlScanResult")]
[PrimaryKey("id", AutoIncrement = true)]
public class ScanResultDto
{
    [Column("id")]
    public int Id { get; set; }

    [Column("RunId")]
    public string RunId { get; set; } = string.Empty;

    [Column("Url")]
    public string Url { get; set; } = string.Empty;

    [Column("TargetType")]
    public string TargetType { get; set; } = string.Empty;

    [Column("NodeKey")]
    public string NodeKey { get; set; } = string.Empty;

    [Column("Culture")]
    public string? Culture { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("Success")]
    public bool Success { get; set; }

    [Column("StatusCode")]
    public int StatusCode { get; set; }

    [Column("FinalUrl")]
    public string? FinalUrl { get; set; }

    [Column("RedirectCount")]
    public int RedirectCount { get; set; }

    [Column("HasSpam")]
    public bool HasSpam { get; set; }

    [Column("HasCloaking")]
    public bool HasCloaking { get; set; }

    [Column("JsErrorCount")]
    public int JsErrorCount { get; set; }

    [Column("ElapsedMs")]
    public long ElapsedMs { get; set; }

    [Column("HasIssue")]
    public bool HasIssue { get; set; }

    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [Column("ScannedUtc")]
    public DateTime ScannedUtc { get; set; }
}

/// <summary>NPoco runtime DTO for the standing error-URL list (<c>uTProUrlScanError</c>).</summary>
[TableName("uTProUrlScanError")]
[PrimaryKey("id", AutoIncrement = true)]
public class ScanErrorDto
{
    [Column("id")]
    public int Id { get; set; }

    [Column("UrlHash")]
    public string UrlHash { get; set; } = string.Empty;

    [Column("Url")]
    public string Url { get; set; } = string.Empty;

    [Column("TargetType")]
    public string TargetType { get; set; } = string.Empty;

    [Column("NodeKey")]
    public string NodeKey { get; set; } = string.Empty;

    [Column("Culture")]
    public string? Culture { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("StatusCode")]
    public int StatusCode { get; set; }

    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [Column("FailureCount")]
    public int FailureCount { get; set; }

    [Column("FirstFailedUtc")]
    public DateTime FirstFailedUtc { get; set; }

    [Column("LastFailedUtc")]
    public DateTime LastFailedUtc { get; set; }
}

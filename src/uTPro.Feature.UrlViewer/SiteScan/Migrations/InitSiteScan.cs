using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace uTPro.Feature.UrlViewer.SiteScan.Migrations;

/// <summary>
/// Creates the Site URL Scan tables (<c>uTProUrlScanRun</c>, <c>uTProUrlScanResult</c>,
/// <c>uTProUrlScanError</c>). Schemas are built from immutable snapshots so column types
/// resolve per database provider (SQL Server, SQLite, PostgreSQL).
/// </summary>
public class InitSiteScan : AsyncMigrationBase
{
    public InitSiteScan(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (!TableExists("uTProUrlScanRun"))
        {
            Create.Table<ScanRunSchema>().Do();
        }

        if (!TableExists("uTProUrlScanResult"))
        {
            Create.Table<ScanResultSchema>().Do();
        }

        if (!TableExists("uTProUrlScanError"))
        {
            Create.Table<ScanErrorSchema>().Do();
        }

        return Task.CompletedTask;
    }

    // ── Immutable schema snapshots (do not change after release; add new steps for changes) ──

    [TableName("uTProUrlScanRun")]
    [PrimaryKey("id", AutoIncrement = true)]
    [ExplicitColumns]
    public class ScanRunSchema
    {
        [Column("id")]
        [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
        public int Id { get; set; }

        [Column("RunId")]
        [Length(36)]
        [Index(IndexTypes.UniqueNonClustered, Name = "IX_uTProUrlScanRun_RunId")]
        public string RunId { get; set; } = string.Empty;

        [Column("Trigger")]
        [Length(20)]
        public string Trigger { get; set; } = string.Empty;

        [Column("InitiatingUserId")]
        [NullSetting(NullSetting = NullSettings.Null)]
        public int? InitiatingUserId { get; set; }

        [Column("StartUtc")]
        [Index(IndexTypes.NonClustered, Name = "IX_uTProUrlScanRun_StartUtc")]
        public DateTime StartUtc { get; set; }

        [Column("EndUtc")]
        [NullSetting(NullSetting = NullSettings.Null)]
        public DateTime? EndUtc { get; set; }

        [Column("State")]
        [Length(20)]
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

    [TableName("uTProUrlScanResult")]
    [PrimaryKey("id", AutoIncrement = true)]
    [ExplicitColumns]
    public class ScanResultSchema
    {
        [Column("id")]
        [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
        public int Id { get; set; }

        [Column("RunId")]
        [Length(36)]
        [Index(IndexTypes.NonClustered, Name = "IX_uTProUrlScanResult_RunId")]
        public string RunId { get; set; } = string.Empty;

        [Column("Url")]
        [Length(2048)]
        public string Url { get; set; } = string.Empty;

        [Column("TargetType")]
        [Length(20)]
        public string TargetType { get; set; } = string.Empty;

        [Column("NodeKey")]
        [Length(36)]
        public string NodeKey { get; set; } = string.Empty;

        [Column("Culture")]
        [Length(20)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string? Culture { get; set; }

        [Column("Name")]
        [Length(512)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string? Name { get; set; }

        [Column("Success")]
        public bool Success { get; set; }

        [Column("StatusCode")]
        public int StatusCode { get; set; }

        [Column("FinalUrl")]
        [Length(2048)]
        [NullSetting(NullSetting = NullSettings.Null)]
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
        [Index(IndexTypes.NonClustered, Name = "IX_uTProUrlScanResult_HasIssue")]
        public bool HasIssue { get; set; }

        [Column("ErrorMessage")]
        [Length(1024)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string? ErrorMessage { get; set; }

        [Column("ScannedUtc")]
        public DateTime ScannedUtc { get; set; }
    }

    [TableName("uTProUrlScanError")]
    [PrimaryKey("id", AutoIncrement = true)]
    [ExplicitColumns]
    public class ScanErrorSchema
    {
        [Column("id")]
        [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
        public int Id { get; set; }

        [Column("UrlHash")]
        [Length(64)]
        [Index(IndexTypes.UniqueNonClustered, Name = "IX_uTProUrlScanError_UrlHash")]
        public string UrlHash { get; set; } = string.Empty;

        [Column("Url")]
        [Length(2048)]
        public string Url { get; set; } = string.Empty;

        [Column("TargetType")]
        [Length(20)]
        public string TargetType { get; set; } = string.Empty;

        [Column("NodeKey")]
        [Length(36)]
        public string NodeKey { get; set; } = string.Empty;

        [Column("Culture")]
        [Length(20)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string? Culture { get; set; }

        [Column("Name")]
        [Length(512)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string? Name { get; set; }

        [Column("StatusCode")]
        public int StatusCode { get; set; }

        [Column("ErrorMessage")]
        [Length(1024)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string? ErrorMessage { get; set; }

        [Column("FailureCount")]
        public int FailureCount { get; set; }

        [Column("FirstFailedUtc")]
        public DateTime FirstFailedUtc { get; set; }

        [Column("LastFailedUtc")]
        public DateTime LastFailedUtc { get; set; }
    }
}

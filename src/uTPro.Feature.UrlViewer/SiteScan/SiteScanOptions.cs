namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Configuration for the Site URL Scan feature, bound from the
/// <c>uTPro:Feature:UrlViewer:SiteScan</c> section.
/// </summary>
public sealed class SiteScanOptions
{
    public const string SectionName = "uTPro:Feature:UrlViewer:SiteScan";

    /// <summary>Master switch. When <c>false</c> the recurring job performs no work.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the recurring scan runs. Default 24 hours.</summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Delay before the first run after startup. Default 5 minutes.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum concurrent HTTP fetches. Clamped to 1..20. Default 4.</summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>Delay applied after each fetch to throttle load (milliseconds). Default 150ms.</summary>
    public int ThrottleDelayMs { get; set; } = 150;

    /// <summary>When <c>true</c> the expensive bot-vs-Chrome cloaking check is skipped during bulk scans.</summary>
    public bool SkipCloakingCheck { get; set; } = true;

    /// <summary>
    /// When <c>true</c> the SSRF guard is relaxed so the scan can fetch the site's own
    /// private/local addresses (needed when the site runs internally). Default <c>false</c>.
    /// </summary>
    public bool AllowInternalHosts { get; set; }

    /// <summary>Number of redirect hops above which a result is flagged as "long redirect chain".</summary>
    public int RedirectWarningThreshold { get; set; } = 3;

    /// <summary>Maximum number of scan runs retained in the database before old runs are pruned.</summary>
    public int MaxRunHistory { get; set; } = 20;

    // ── Normalized accessors (never invalid) ──

    public TimeSpan EffectivePeriod => Period > TimeSpan.Zero ? Period : TimeSpan.FromHours(24);
    public TimeSpan EffectiveDelay => Delay >= TimeSpan.Zero ? Delay : TimeSpan.FromMinutes(5);
    public int EffectiveConcurrency => Math.Clamp(MaxConcurrency, 1, 20);
    public int EffectiveThrottleMs => ThrottleDelayMs < 0 ? 0 : ThrottleDelayMs;
    public int EffectiveMaxRunHistory => MaxRunHistory > 0 ? MaxRunHistory : 20;
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Recurring background job that periodically scans every Content/Media URL of the site.
/// Auto-discovered and monitored by <c>uTPro.Feature.JobMonitor</c>. Runs on a single node only
/// (scheduling publisher / single) to avoid duplicate scans in a load-balanced setup.
/// </summary>
public sealed class SiteUrlScanJob : IRecurringBackgroundJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SiteScanOptions> _optionsMonitor;
    private readonly ILogger<SiteUrlScanJob> _logger;
    private readonly IDisposable? _optionsListener;

    private TimeSpan _period;

    public SiteUrlScanJob(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SiteScanOptions> optionsMonitor,
        ILogger<SiteUrlScanJob> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _period = optionsMonitor.CurrentValue.EffectivePeriod;

        // Re-raise the wrapper's schedule when the configured period changes.
        _optionsListener = optionsMonitor.OnChange(opts =>
        {
            var newPeriod = opts.EffectivePeriod;
            if (newPeriod != _period)
            {
                _period = newPeriod;
                PeriodChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    public TimeSpan Period => _period;

    public TimeSpan Delay => _optionsMonitor.CurrentValue.EffectiveDelay;

    public ServerRole[] ServerRoles => [ServerRole.SchedulingPublisher, ServerRole.Single];

    public event EventHandler? PeriodChanged;

    public async Task RunJobAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scanService = scope.ServiceProvider.GetRequiredService<ISiteScanService>();
            await scanService.RunScanAsync(ScanTrigger.Scheduled, userId: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Site URL scan job failed.");
        }
    }
}

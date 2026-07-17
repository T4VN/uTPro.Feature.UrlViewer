using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;
using uTPro.Feature.UrlViewer.Services;
using uTPro.Feature.UrlViewer.SiteScan;
using uTPro.Feature.UrlViewer.SiteScan.Migrations;

namespace uTPro.Feature.UrlViewer;

/// <summary>
/// Registers UrlViewer + Site URL Scan services into the DI container via the Umbraco Composer pattern.
/// </summary>
public class UrlViewerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Single no-redirect client for all fetches: every hop (initial URL, each redirect and the
        // body read) is followed manually so the DNS-based SSRF guard runs on every host. An
        // auto-redirect client would bypass that guard, so it is intentionally not registered.
        builder.Services.AddHttpClient("UrlViewerNoRedirect")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false
            });

        builder.Services.AddScoped<IUrlViewerService, UrlViewerService>();

        // ── Site URL Scan ──
        builder.Services.Configure<SiteScanOptions>(builder.Config.GetSection(SiteScanOptions.SectionName));

        builder.Services.AddSingleton<IScanReportStore, ScanReportStore>();
        builder.Services.AddScoped<IUrlCollectorService, UrlCollectorService>();
        builder.Services.AddScoped<ISiteScanService, SiteScanService>();

        // Schema migration runs on application start.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SiteScanMigrationHandler>();

        // Recurring background job (auto-discovered by uTPro.Feature.JobMonitor).
        builder.Services.AddRecurringBackgroundJob<SiteUrlScanJob>();
    }
}

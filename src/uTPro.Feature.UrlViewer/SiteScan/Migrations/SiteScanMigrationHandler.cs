using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace uTPro.Feature.UrlViewer.SiteScan.Migrations;

/// <summary>
/// Runs the Site URL Scan migration plan on application start. The plan is state-keyed, so each
/// step runs at most once per database.
/// </summary>
public class SiteScanMigrationHandler(
    ICoreScopeProvider coreScopeProvider,
    IMigrationPlanExecutor migrationPlanExecutor,
    IKeyValueService keyValueService,
    IRuntimeState runtimeState)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run)
        {
            return;
        }

        var plan = new MigrationPlan("uTPro.UrlViewer.SiteScan");
        plan.From(string.Empty)
            .To<InitSiteScan>("sitescan-init");

        var upgrader = new Upgrader(plan);
        await upgrader.ExecuteAsync(migrationPlanExecutor, coreScopeProvider, keyValueService);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using uTPro.Feature.UrlViewer.SiteScan;

namespace uTPro.Feature.UrlViewer.Controllers;

/// <summary>
/// Versioned, authenticated Management API for the Site URL Scan reports.
/// Routed under /umbraco and gated to the Settings section.
/// </summary>
[VersionedApiBackOfficeRoute("utpro/url-scan")]
[ApiExplorerSettings(GroupName = "uTPro URL Viewer")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public class SiteScanApiController(
    IScanReportStore store,
    IServiceScopeFactory scopeFactory,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor) : ManagementApiControllerBase
{
    /// <summary>Reports whether a scan is currently running.</summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        // Resolve a scoped service just to read the process-wide running flag.
        using var scope = scopeFactory.CreateScope();
        var scanService = scope.ServiceProvider.GetRequiredService<ISiteScanService>();
        return Ok(new { isRunning = scanService.IsRunning });
    }

    /// <summary>Starts a full manual scan in the background.</summary>
    [HttpPost("run")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Run()
    {
        var userId = CurrentUserId();
        StartBackground(svc => svc.RunScanAsync(ScanTrigger.Manual, userId));
        return Accepted(new { status = "started" });
    }

    /// <summary>Re-scans only the URLs currently in the error list, in the background.</summary>
    [HttpPost("errors/rescan")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult RescanErrors()
    {
        var userId = CurrentUserId();
        StartBackground(svc => svc.RescanErrorsAsync(userId));
        return Accepted(new { status = "started" });
    }

    /// <summary>Re-scans a single URL synchronously and returns the fresh result.</summary>
    [HttpPost("errors/rescan-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RescanUrl([FromBody] RescanUrlRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { error = "URL is required." });
        }

        using var scope = scopeFactory.CreateScope();
        var scanService = scope.ServiceProvider.GetRequiredService<ISiteScanService>();
        var row = await scanService.RescanUrlAsync(request.Url, CurrentUserId(), cancellationToken);
        return Ok(MapResult(row));
    }

    /// <summary>Lists recent scan runs.</summary>
    [HttpGet("runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetRuns([FromQuery] int limit = 20)
    {
        var runs = store.GetRuns(limit).Select(MapRun);
        return Ok(runs);
    }

    /// <summary>Returns the results of a run, optionally filtered.</summary>
    [HttpGet("runs/{runId:guid}/results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetResults(Guid runId, [FromQuery] string? filter = null)
    {
        if (store.GetRun(runId) is null)
        {
            return NotFound();
        }

        var parsedFilter = Enum.TryParse<ScanResultFilter>(filter, ignoreCase: true, out var f)
            ? f
            : ScanResultFilter.All;

        var results = store.GetResults(runId, parsedFilter).Select(MapResult);
        return Ok(results);
    }

    /// <summary>Returns the current standing error-URL list.</summary>
    [HttpGet("errors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetErrors()
    {
        var errors = store.GetErrors().Select(e => new
        {
            url = e.Url,
            type = e.Type.ToString(),
            nodeKey = e.NodeKey,
            culture = e.Culture,
            name = e.Name,
            statusCode = e.StatusCode,
            errorMessage = e.ErrorMessage,
            failureCount = e.FailureCount,
            firstFailedUtc = e.FirstFailedUtc.ToString("o"),
            lastFailedUtc = e.LastFailedUtc.ToString("o")
        });
        return Ok(errors);
    }

    // ── Helpers ──

    private int? CurrentUserId()
    {
        var id = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Id;
        return id;
    }

    private void StartBackground(Func<ISiteScanService, Task> work)
    {
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var scanService = scope.ServiceProvider.GetRequiredService<ISiteScanService>();
            await work(scanService);
        });
    }

    private static object MapRun(ScanRunSummary run) => new
    {
        runId = run.RunId,
        trigger = run.Trigger.ToString(),
        initiatingUserId = run.InitiatingUserId,
        startUtc = run.StartUtc.ToString("o"),
        endUtc = run.EndUtc?.ToString("o"),
        state = run.State.ToString(),
        totalTargets = run.TotalTargets,
        successCount = run.SuccessCount,
        failureCount = run.FailureCount,
        issueCount = run.IssueCount
    };

    private static object MapResult(ScanResultRow row) => new
    {
        url = row.Url,
        type = row.Type.ToString(),
        nodeKey = row.NodeKey,
        culture = row.Culture,
        name = row.Name,
        success = row.Success,
        statusCode = row.StatusCode,
        finalUrl = row.FinalUrl,
        redirectCount = row.RedirectCount,
        hasSpam = row.HasSpam,
        hasCloaking = row.HasCloaking,
        jsErrorCount = row.JsErrorCount,
        elapsedMs = row.ElapsedMs,
        isIssue = row.IsIssue,
        errorMessage = row.ErrorMessage,
        scannedUtc = row.ScannedUtc.ToString("o")
    };
}

/// <summary>Body for the single-URL re-scan endpoint.</summary>
public sealed class RescanUrlRequest
{
    public string Url { get; set; } = string.Empty;
}

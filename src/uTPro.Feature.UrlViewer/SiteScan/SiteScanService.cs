using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using uTPro.Feature.UrlViewer.Models;
using uTPro.Feature.UrlViewer.Services;

namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Coordinates a scan: collect targets → fetch/analyse each URL with bounded concurrency and
/// throttling → persist a summary row per URL → maintain the standing error-URL list.
/// A process-wide gate prevents overlapping full scans / error re-scans.
/// </summary>
public sealed class SiteScanService(
    IUrlCollectorService collector,
    IUrlViewerService viewer,
    IScanReportStore store,
    IOptions<SiteScanOptions> options,
    ILogger<SiteScanService> logger) : ISiteScanService
{
    private static readonly SemaphoreSlim RunGate = new(1, 1);

    private readonly SiteScanOptions _options = options.Value;

    public bool IsRunning => RunGate.CurrentCount == 0;

    public async Task<Guid?> RunScanAsync(ScanTrigger trigger, int? userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Site URL Scan is disabled; skipping {Trigger} run.", trigger);
            return null;
        }

        if (!await RunGate.WaitAsync(0, cancellationToken))
        {
            logger.LogInformation("A site URL scan is already running; skipping {Trigger} run.", trigger);
            return null;
        }

        try
        {
            var targets = collector.CollectTargets(cancellationToken);

            // Scheduled runs skip URLs already known to be failing (re-scanned on demand only).
            if (trigger == ScanTrigger.Scheduled)
            {
                var errorUrls = store.GetErrors()
                    .Select(e => e.Url)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (errorUrls.Count > 0)
                {
                    targets = targets.Where(t => !errorUrls.Contains(t.Url)).ToList();
                }
            }

            var runId = await ExecuteScanAsync(trigger, userId, targets, updateErrorList: true, cancellationToken);
            store.PruneRuns(_options.EffectiveMaxRunHistory);
            return runId;
        }
        finally
        {
            RunGate.Release();
        }
    }

    public async Task<Guid?> RescanErrorsAsync(int? userId, CancellationToken cancellationToken = default)
    {
        if (!await RunGate.WaitAsync(0, cancellationToken))
        {
            logger.LogInformation("A site URL scan is already running; skipping error re-scan.");
            return null;
        }

        try
        {
            var targets = store.GetErrors()
                .Select(e => new ScanTarget
                {
                    Url = e.Url,
                    Type = e.Type,
                    NodeKey = e.NodeKey,
                    Culture = e.Culture,
                    Name = e.Name
                })
                .ToList();

            return await ExecuteScanAsync(ScanTrigger.ErrorRescan, userId, targets, updateErrorList: true, cancellationToken);
        }
        finally
        {
            RunGate.Release();
        }
    }

    public async Task<ScanResultRow> RescanUrlAsync(string url, int? userId, CancellationToken cancellationToken = default)
    {
        // Try to recover metadata from the existing error entry (falls back to bare URL).
        var existing = store.GetErrors().FirstOrDefault(e =>
            string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase));

        var target = new ScanTarget
        {
            Url = url,
            Type = existing?.Type ?? ScanTargetType.Content,
            NodeKey = existing?.NodeKey ?? Guid.Empty,
            Culture = existing?.Culture,
            Name = existing?.Name
        };

        var row = await ScanTargetAsync(Guid.Empty, target, cancellationToken);

        if (row.Success)
        {
            store.RemoveError(url);
        }
        else
        {
            store.UpsertError(row);
        }

        return row;
    }

    private async Task<Guid> ExecuteScanAsync(
        ScanTrigger trigger, int? userId, IReadOnlyList<ScanTarget> targets,
        bool updateErrorList, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        var startUtc = DateTime.UtcNow;

        store.CreateRun(new ScanRunSummary
        {
            RunId = runId,
            Trigger = trigger,
            InitiatingUserId = userId,
            StartUtc = startUtc,
            State = ScanRunState.Running,
            TotalTargets = targets.Count
        });

        int success = 0, failure = 0, issues = 0;
        var state = ScanRunState.Completed;

        using var concurrency = new SemaphoreSlim(_options.EffectiveConcurrency);

        // Phase 1 — fetch every target with bounded concurrency. Only HTTP work runs in parallel;
        // NO database access happens here on purpose. Persisting from these parallel tasks would
        // have multiple threads share the run's ambient Umbraco scope (a single DB
        // connection/transaction), which NPoco/Npgsql does not support — it corrupts the
        // transaction ("concurrent threads accessing the same Scope" / "Transaction is already
        // completed" / disposed NpgsqlTransaction / buffer-overflow). DB writes happen in phase 2.
        var fetchTasks = targets.Select(async target =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                var row = await ScanTargetAsync(runId, target, cancellationToken);
                if (_options.EffectiveThrottleMs > 0)
                {
                    await Task.Delay(_options.EffectiveThrottleMs, cancellationToken);
                }
                return row;
            }
            finally
            {
                concurrency.Release();
            }
        });

        ScanResultRow[] rows = [];
        try
        {
            rows = await Task.WhenAll(fetchTasks);
        }
        catch (OperationCanceledException)
        {
            state = ScanRunState.Cancelled;
            logger.LogWarning("Site URL scan {RunId} was cancelled.", runId);
        }
        catch (Exception ex)
        {
            state = ScanRunState.Failed;
            logger.LogError(ex, "Site URL scan {RunId} failed.", runId);
        }

        // Phase 2 — persist sequentially (single-threaded) so DB writes never run on concurrent
        // threads sharing the same scope. These calls are cheap relative to the HTTP fetches.
        foreach (var row in rows)
        {
            store.AddResult(row);

            if (updateErrorList)
            {
                if (row.Success)
                {
                    store.RemoveError(row.Url);
                }
                else
                {
                    store.UpsertError(row);
                }
            }

            if (row.Success) success++;
            else failure++;
            if (row.IsIssue) issues++;
        }

        store.CompleteRun(new ScanRunSummary
        {
            RunId = runId,
            Trigger = trigger,
            InitiatingUserId = userId,
            StartUtc = startUtc,
            EndUtc = DateTime.UtcNow,
            State = state,
            TotalTargets = targets.Count,
            SuccessCount = success,
            FailureCount = failure,
            IssueCount = issues
        });

        logger.LogInformation(
            "Site URL scan {RunId} finished: {Total} targets, {Success} ok, {Failure} failed, {Issues} issues.",
            runId, targets.Count, success, failure, issues);

        return runId;
    }

    private async Task<ScanResultRow> ScanTargetAsync(Guid runId, ScanTarget target, CancellationToken ct)
    {
        var scannedUtc = DateTime.UtcNow;
        try
        {
            var request = BuildRequest(target.Url);
            var resp = await viewer.FetchUrlAsync(request, ct);

            var redirectCount = Math.Max(0, resp.RedirectChain.Count - 1);
            var hasSpam = resp.Analysis?.SpamWords.Count > 0;
            var hasCloaking = resp.Analysis?.Cloaking?.IsCloaked == true;
            var jsErrorCount = resp.Analysis?.JsIssues.Count(i =>
                string.Equals(i.Severity, "error", StringComparison.OrdinalIgnoreCase)) ?? 0;

            var httpOk = resp.StatusCode is >= 200 and < 400;
            var success = resp.Success && httpOk;

            string? errorMessage = null;
            if (!resp.Success)
            {
                errorMessage = resp.ErrorMessage ?? "Fetch failed.";
            }
            else if (!httpOk)
            {
                errorMessage = $"HTTP {resp.StatusCode} {resp.StatusDescription}".Trim();
            }

            var isIssue = ScanResultRow.Evaluate(
                success, resp.StatusCode, hasSpam, hasCloaking, jsErrorCount, redirectCount,
                _options.RedirectWarningThreshold);

            return new ScanResultRow
            {
                RunId = runId,
                Url = target.Url,
                Type = target.Type,
                NodeKey = target.NodeKey,
                Culture = target.Culture,
                Name = target.Name,
                Success = success,
                StatusCode = resp.StatusCode,
                FinalUrl = resp.FinalUrl,
                RedirectCount = redirectCount,
                HasSpam = hasSpam,
                HasCloaking = hasCloaking,
                JsErrorCount = jsErrorCount,
                ElapsedMs = resp.ElapsedMilliseconds,
                IsIssue = isIssue,
                ErrorMessage = errorMessage,
                ScannedUtc = scannedUtc
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to scan target {Url}.", target.Url);
            return new ScanResultRow
            {
                RunId = runId,
                Url = target.Url,
                Type = target.Type,
                NodeKey = target.NodeKey,
                Culture = target.Culture,
                Name = target.Name,
                Success = false,
                StatusCode = 0,
                RedirectCount = 0,
                ElapsedMs = 0,
                IsIssue = true,
                ErrorMessage = ex.Message,
                ScannedUtc = scannedUtc
            };
        }
    }

    private UrlViewerRequest BuildRequest(string absoluteUrl)
    {
        var scheme = "https";
        var rest = absoluteUrl.Trim();

        if (rest.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "http";
            rest = rest["http://".Length..];
        }
        else if (rest.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "https";
            rest = rest["https://".Length..];
        }

        return new UrlViewerRequest
        {
            Url = rest,
            Scheme = scheme,
            UserAgent = "googlebot-smartphone",
            Referrer = "google",
            SkipCloakingCheck = _options.SkipCloakingCheck,
            AllowInternalHosts = _options.AllowInternalHosts
        };
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Durable report store backed by the Umbraco database. Uses short-lived NPoco scopes for every
/// operation so no long transaction is held (avoids DB locking during long scans). Failures are
/// logged and degrade gracefully — telemetry never breaks a running scan.
/// </summary>
public sealed class ScanReportStore(
    IScopeProvider scopeProvider,
    ILogger<ScanReportStore> logger) : IScanReportStore
{
    public void CreateRun(ScanRunSummary run)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            scope.Database.Insert(ToDto(run));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create scan run {RunId}.", run.RunId);
        }
    }

    public void CompleteRun(ScanRunSummary run)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var runId = run.RunId.ToString();
            var sql = scope.SqlContext.Sql()
                .SelectAll().From<ScanRunDto>()
                .Where<ScanRunDto>(x => x.RunId == runId);

            var dto = scope.Database.SingleOrDefault<ScanRunDto>(sql);
            if (dto is null)
            {
                scope.Database.Insert(ToDto(run));
                return;
            }

            dto.EndUtc = run.EndUtc;
            dto.State = run.State.ToString();
            dto.TotalTargets = run.TotalTargets;
            dto.SuccessCount = run.SuccessCount;
            dto.FailureCount = run.FailureCount;
            dto.IssueCount = run.IssueCount;
            scope.Database.Update(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete scan run {RunId}.", run.RunId);
        }
    }

    public void AddResult(ScanResultRow row)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            scope.Database.Insert(ToDto(row));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist scan result for {Url}.", row.Url);
        }
    }

    public void AddResults(IReadOnlyList<ScanResultRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        try
        {
            // Single short scope wraps all inserts in one transaction: N per-row commits become
            // one commit. Pure inserts (no reads), so this does not lengthen lock scope the way a
            // read-modify-write batch would, keeping the store's short-transaction design intact.
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            foreach (var row in rows)
            {
                scope.Database.Insert(ToDto(row));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist {Count} scan results.", rows.Count);
        }
    }

    public IReadOnlyList<ScanRunSummary> GetRuns(int limit)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var sql = scope.SqlContext.Sql()
                .SelectAll().From<ScanRunDto>()
                .OrderByDescending<ScanRunDto>(x => x.StartUtc);

            return scope.Database.Fetch<ScanRunDto>(1, Math.Max(1, limit), sql)
                .Select(ToSummary)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read scan runs.");
            return [];
        }
    }

    public ScanRunSummary? GetRun(Guid runId)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var id = runId.ToString();
            var sql = scope.SqlContext.Sql()
                .SelectAll().From<ScanRunDto>()
                .Where<ScanRunDto>(x => x.RunId == id);

            var dto = scope.Database.SingleOrDefault<ScanRunDto>(sql);
            return dto is null ? null : ToSummary(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read scan run {RunId}.", runId);
            return null;
        }
    }

    public IReadOnlyList<ScanResultRow> GetResults(Guid runId, ScanResultFilter filter)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var id = runId.ToString();
            var sql = scope.SqlContext.Sql()
                .SelectAll().From<ScanResultDto>()
                .Where<ScanResultDto>(x => x.RunId == id);

            switch (filter)
            {
                case ScanResultFilter.IssuesOnly:
                    sql = sql.Where<ScanResultDto>(x => x.HasIssue);
                    break;
                case ScanResultFilter.FailuresOnly:
                    sql = sql.Where<ScanResultDto>(x => !x.Success);
                    break;
                case ScanResultFilter.SpamOnly:
                    sql = sql.Where<ScanResultDto>(x => x.HasSpam);
                    break;
                case ScanResultFilter.CloakingOnly:
                    sql = sql.Where<ScanResultDto>(x => x.HasCloaking);
                    break;
            }

            sql = sql.OrderByDescending<ScanResultDto>(x => x.HasIssue)
                     .OrderBy<ScanResultDto>(x => x.Url);

            return scope.Database.Fetch<ScanResultDto>(sql)
                .Select(ToRow)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read results for run {RunId}.", runId);
            return [];
        }
    }

    public IReadOnlyList<ErrorUrlEntry> GetErrors()
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var sql = scope.SqlContext.Sql()
                .SelectAll().From<ScanErrorDto>()
                .OrderByDescending<ScanErrorDto>(x => x.FailureCount)
                .OrderByDescending<ScanErrorDto>(x => x.LastFailedUtc);

            return scope.Database.Fetch<ScanErrorDto>(sql)
                .Select(ToErrorEntry)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read error URL list.");
            return [];
        }
    }

    public void UpsertError(ScanResultRow failure)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var hash = Hash(failure.Url);
            var sql = scope.SqlContext.Sql()
                .SelectAll().From<ScanErrorDto>()
                .Where<ScanErrorDto>(x => x.UrlHash == hash);

            var dto = scope.Database.SingleOrDefault<ScanErrorDto>(sql);
            var now = failure.ScannedUtc == default ? DateTime.UtcNow : failure.ScannedUtc;

            if (dto is null)
            {
                scope.Database.Insert(new ScanErrorDto
                {
                    UrlHash = hash,
                    Url = failure.Url,
                    TargetType = failure.Type.ToString(),
                    NodeKey = failure.NodeKey.ToString(),
                    Culture = failure.Culture,
                    Name = failure.Name,
                    StatusCode = failure.StatusCode,
                    ErrorMessage = failure.ErrorMessage,
                    FailureCount = 1,
                    FirstFailedUtc = now,
                    LastFailedUtc = now
                });
            }
            else
            {
                dto.TargetType = failure.Type.ToString();
                dto.NodeKey = failure.NodeKey.ToString();
                dto.Culture = failure.Culture;
                dto.Name = failure.Name;
                dto.StatusCode = failure.StatusCode;
                dto.ErrorMessage = failure.ErrorMessage;
                dto.FailureCount += 1;
                dto.LastFailedUtc = now;
                scope.Database.Update(dto);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upsert error URL {Url}.", failure.Url);
        }
    }

    public void RemoveError(string url)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var hash = Hash(url);
            scope.Database.Execute(
                scope.SqlContext.Sql()
                    .Delete<ScanErrorDto>()
                    .Where<ScanErrorDto>(x => x.UrlHash == hash));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove error URL {Url}.", url);
        }
    }

    public void PruneRuns(int maxRunHistory)
    {
        try
        {
            using var scope = scopeProvider.CreateScope(autoComplete: true);
            var keep = Math.Max(1, maxRunHistory);

            var idsToKeep = scope.Database.Fetch<ScanRunDto>(1, keep,
                    scope.SqlContext.Sql().SelectAll().From<ScanRunDto>()
                        .OrderByDescending<ScanRunDto>(x => x.StartUtc))
                .Select(x => x.RunId)
                .ToList();

            var allRuns = scope.Database.Fetch<ScanRunDto>(
                scope.SqlContext.Sql().SelectAll().From<ScanRunDto>());

            var toDelete = allRuns.Where(r => !idsToKeep.Contains(r.RunId)).ToList();
            foreach (var run in toDelete)
            {
                var runId = run.RunId;
                scope.Database.Execute(
                    scope.SqlContext.Sql().Delete<ScanResultDto>()
                        .Where<ScanResultDto>(x => x.RunId == runId));
                scope.Database.Execute(
                    scope.SqlContext.Sql().Delete<ScanRunDto>()
                        .Where<ScanRunDto>(x => x.RunId == runId));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prune old scan runs.");
        }
    }

    // ── Mapping helpers ──

    private static ScanRunDto ToDto(ScanRunSummary run) => new()
    {
        RunId = run.RunId.ToString(),
        Trigger = run.Trigger.ToString(),
        InitiatingUserId = run.InitiatingUserId,
        StartUtc = run.StartUtc,
        EndUtc = run.EndUtc,
        State = run.State.ToString(),
        TotalTargets = run.TotalTargets,
        SuccessCount = run.SuccessCount,
        FailureCount = run.FailureCount,
        IssueCount = run.IssueCount
    };

    private static ScanRunSummary ToSummary(ScanRunDto dto) => new()
    {
        RunId = Guid.TryParse(dto.RunId, out var id) ? id : Guid.Empty,
        Trigger = Enum.TryParse<ScanTrigger>(dto.Trigger, out var t) ? t : ScanTrigger.Scheduled,
        InitiatingUserId = dto.InitiatingUserId,
        StartUtc = DateTime.SpecifyKind(dto.StartUtc, DateTimeKind.Utc),
        EndUtc = dto.EndUtc.HasValue ? DateTime.SpecifyKind(dto.EndUtc.Value, DateTimeKind.Utc) : null,
        State = Enum.TryParse<ScanRunState>(dto.State, out var s) ? s : ScanRunState.Completed,
        TotalTargets = dto.TotalTargets,
        SuccessCount = dto.SuccessCount,
        FailureCount = dto.FailureCount,
        IssueCount = dto.IssueCount
    };

    private static ScanResultDto ToDto(ScanResultRow row) => new()
    {
        RunId = row.RunId.ToString(),
        Url = Truncate(row.Url, 2048),
        TargetType = row.Type.ToString(),
        NodeKey = row.NodeKey.ToString(),
        Culture = row.Culture,
        Name = Truncate(row.Name, 512),
        Success = row.Success,
        StatusCode = row.StatusCode,
        FinalUrl = Truncate(row.FinalUrl, 2048),
        RedirectCount = row.RedirectCount,
        HasSpam = row.HasSpam,
        HasCloaking = row.HasCloaking,
        JsErrorCount = row.JsErrorCount,
        ElapsedMs = row.ElapsedMs,
        HasIssue = row.IsIssue,
        ErrorMessage = Truncate(row.ErrorMessage, 1024),
        ScannedUtc = row.ScannedUtc == default ? DateTime.UtcNow : row.ScannedUtc
    };

    private static ScanResultRow ToRow(ScanResultDto dto) => new()
    {
        RunId = Guid.TryParse(dto.RunId, out var id) ? id : Guid.Empty,
        Url = dto.Url,
        Type = Enum.TryParse<ScanTargetType>(dto.TargetType, out var t) ? t : ScanTargetType.Content,
        NodeKey = Guid.TryParse(dto.NodeKey, out var k) ? k : Guid.Empty,
        Culture = dto.Culture,
        Name = dto.Name,
        Success = dto.Success,
        StatusCode = dto.StatusCode,
        FinalUrl = dto.FinalUrl,
        RedirectCount = dto.RedirectCount,
        HasSpam = dto.HasSpam,
        HasCloaking = dto.HasCloaking,
        JsErrorCount = dto.JsErrorCount,
        ElapsedMs = dto.ElapsedMs,
        IsIssue = dto.HasIssue,
        ErrorMessage = dto.ErrorMessage,
        ScannedUtc = DateTime.SpecifyKind(dto.ScannedUtc, DateTimeKind.Utc)
    };

    private static ErrorUrlEntry ToErrorEntry(ScanErrorDto dto) => new()
    {
        Url = dto.Url,
        Type = Enum.TryParse<ScanTargetType>(dto.TargetType, out var t) ? t : ScanTargetType.Content,
        NodeKey = Guid.TryParse(dto.NodeKey, out var k) ? k : Guid.Empty,
        Culture = dto.Culture,
        Name = dto.Name,
        StatusCode = dto.StatusCode,
        ErrorMessage = dto.ErrorMessage,
        FailureCount = dto.FailureCount,
        FirstFailedUtc = DateTime.SpecifyKind(dto.FirstFailedUtc, DateTimeKind.Utc),
        LastFailedUtc = DateTime.SpecifyKind(dto.LastFailedUtc, DateTimeKind.Utc)
    };

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}

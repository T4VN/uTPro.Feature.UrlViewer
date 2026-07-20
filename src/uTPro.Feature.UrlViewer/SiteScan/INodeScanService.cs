using uTPro.Feature.UrlViewer.Models;

namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Scans the public URL(s) of a single Content or Media node on demand, from the node's own
/// workspace, reusing <see cref="Services.IUrlViewerService"/> for the fetch/analysis. Unlike the
/// site-wide scan this does not touch the report database or the standing error list — it just
/// returns a live report for the node the editor is currently looking at.
/// </summary>
public interface INodeScanService
{
    /// <summary>
    /// Resolves the public URL(s) of the node identified by <paramref name="key"/> and
    /// <paramref name="entityType"/> ("document" or "media") and fetches/analyses each one.
    /// </summary>
    Task<NodeScanResponse> ScanNodeAsync(Guid key, string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves (but does not fetch) the public URL(s) of a node. Used by the backoffice condition
    /// to decide whether the URL Scan tab should be shown at all.
    /// </summary>
    NodeUrlSet ResolveUrls(Guid key, string entityType);
}

/// <summary>A resolved public URL for a node plus its culture (null when invariant/media).</summary>
public sealed class NodeUrl
{
    public required string Url { get; init; }
    public string? Culture { get; init; }
}

/// <summary>The set of public URLs resolved for a single node.</summary>
public sealed class NodeUrlSet
{
    public bool Found { get; init; }
    public string? Name { get; init; }
    public string Type { get; init; } = "Content";
    public IReadOnlyList<NodeUrl> Urls { get; init; } = [];
}

/// <summary>Body for the node-scan endpoint.</summary>
public sealed class NodeScanRequest
{
    /// <summary>The node's unique GUID key.</summary>
    public Guid Key { get; set; }

    /// <summary>Backoffice entity type: "document" (content) or "media".</summary>
    public string? EntityType { get; set; }
}

/// <summary>Outcome of scanning a single node's URL(s).</summary>
public sealed class NodeScanResponse
{
    /// <summary><c>true</c> when the node itself was found (even if it has no public URL).</summary>
    public bool Found { get; init; }

    /// <summary>The node's name (for display).</summary>
    public string? Name { get; init; }

    /// <summary>"Content" or "Media".</summary>
    public string Type { get; init; } = "Content";

    /// <summary>Optional human-readable note (e.g. why there are no URLs to scan).</summary>
    public string? Message { get; init; }

    /// <summary>One entry per resolved public URL (multiple when a Content node varies by culture).</summary>
    public List<NodeScanUrlResult> Results { get; init; } = [];
}

/// <summary>A single resolved URL for the node plus its full fetch report.</summary>
public sealed class NodeScanUrlResult
{
    /// <summary>The absolute URL that was scanned.</summary>
    public required string Url { get; init; }

    /// <summary>The culture this URL belongs to (null for invariant content / media).</summary>
    public string? Culture { get; init; }

    /// <summary>The full fetch + analysis report (same shape as the manual URL Viewer).</summary>
    public required UrlViewerResponse Report { get; init; }
}

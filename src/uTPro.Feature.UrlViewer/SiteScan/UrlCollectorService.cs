using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Collects Content and Media URLs using the stable management services
/// (<see cref="IContentService"/> / <see cref="IMediaService"/>) combined with
/// <see cref="IPublishedUrlProvider"/> for absolute URL resolution. An ambient Umbraco context
/// is ensured so the URL provider can resolve configured domains from a background thread.
/// </summary>
public sealed class UrlCollectorService(
    IContentService contentService,
    IMediaService mediaService,
    IPublishedUrlProvider publishedUrlProvider,
    IUmbracoContextFactory umbracoContextFactory,
    ILogger<UrlCollectorService> logger) : IUrlCollectorService
{
    private const int PageSize = 200;

    public IReadOnlyList<ScanTarget> CollectTargets(CancellationToken cancellationToken = default)
    {
        var targets = new List<ScanTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The URL provider needs an ambient Umbraco context to resolve absolute URLs / domains.
        using var contextReference = umbracoContextFactory.EnsureUmbracoContext();

        CollectContent(targets, seen, cancellationToken);
        CollectMedia(targets, seen, cancellationToken);

        return targets;
    }

    private void CollectContent(List<ScanTarget> targets, HashSet<string> seen, CancellationToken ct)
    {
        foreach (var content in EnumerateAll<IContent>(
            (long page, out long total) => contentService.GetPagedDescendants(Constants.System.Root, page, PageSize, out total), ct))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (content.Trashed)
                {
                    continue;
                }

                if (content.ContentType.Variations.VariesByCulture())
                {
                    foreach (var culture in content.PublishedCultures)
                    {
                        AddContentUrl(targets, seen, content, culture);
                    }
                }
                else if (content.Published)
                {
                    AddContentUrl(targets, seen, content, null);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to collect URL for content {Key}.", content.Key);
            }
        }
    }

    private void CollectMedia(List<ScanTarget> targets, HashSet<string> seen, CancellationToken ct)
    {
        foreach (var media in EnumerateAll<IMedia>(
            (long page, out long total) => mediaService.GetPagedDescendants(Constants.System.Root, page, PageSize, out total), ct))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (media.Trashed)
                {
                    continue;
                }

                var url = publishedUrlProvider.GetMediaUrl(
                    media.Key, UrlMode.Absolute, null, Constants.Conventions.Media.File);

                AddTarget(targets, seen, url, ScanTargetType.Media, media.Key, null, media.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to collect URL for media {Key}.", media.Key);
            }
        }
    }

    private void AddContentUrl(List<ScanTarget> targets, HashSet<string> seen, IContent content, string? culture)
    {
        var url = publishedUrlProvider.GetUrl(content.Key, UrlMode.Absolute, culture);
        AddTarget(targets, seen, url, ScanTargetType.Content, content.Key, culture, content.Name);
    }

    private static void AddTarget(
        List<ScanTarget> targets, HashSet<string> seen,
        string? url, ScanTargetType type, Guid nodeKey, string? culture, string? name)
    {
        if (string.IsNullOrWhiteSpace(url) || url == "#")
        {
            return;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!seen.Add(url))
        {
            return;
        }

        targets.Add(new ScanTarget
        {
            Url = url,
            Type = type,
            NodeKey = nodeKey,
            Culture = culture,
            Name = name
        });
    }

    // ── Paged enumeration helper (short reads, never the whole tree in memory at once) ──

    private delegate IEnumerable<T> Pager<T>(long pageIndex, out long total);

    private static IEnumerable<T> EnumerateAll<T>(Pager<T> pager, CancellationToken ct)
    {
        long pageIndex = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var items = pager(pageIndex, out var total).ToList();
            foreach (var item in items)
            {
                yield return item;
            }

            var seenSoFar = (pageIndex + 1) * PageSize;
            if (items.Count == 0 || seenSoFar >= total)
            {
                yield break;
            }

            pageIndex++;
        }
    }
}

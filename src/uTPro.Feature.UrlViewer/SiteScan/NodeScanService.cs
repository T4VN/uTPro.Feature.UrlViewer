using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using uTPro.Feature.UrlViewer.Models;
using uTPro.Feature.UrlViewer.Services;

namespace uTPro.Feature.UrlViewer.SiteScan;

/// <summary>
/// Resolves and scans the public URL(s) of a single Content/Media node on demand. Reuses the same
/// URL-resolution approach as <see cref="UrlCollectorService"/> (management services +
/// <see cref="IPublishedUrlProvider"/> under an ensured Umbraco context) and delegates the actual
/// fetch/analysis to <see cref="IUrlViewerService"/>. Nothing is persisted.
/// </summary>
public sealed class NodeScanService(
    IContentService contentService,
    IMediaService mediaService,
    IPublishedUrlProvider publishedUrlProvider,
    IUmbracoContextFactory umbracoContextFactory,
    IUrlViewerService viewer,
    IOptions<SiteScanOptions> options,
    ILogger<NodeScanService> logger) : INodeScanService
{
    private readonly SiteScanOptions _options = options.Value;

    public NodeUrlSet ResolveUrls(Guid key, string entityType)
    {
        var isMedia = string.Equals(entityType, "media", StringComparison.OrdinalIgnoreCase);
        var type = isMedia ? "Media" : "Content";
        var urls = new List<NodeUrl>();

        // The URL provider needs an ambient Umbraco context to resolve absolute URLs / domains.
        using (umbracoContextFactory.EnsureUmbracoContext())
        {
            if (isMedia)
            {
                var media = mediaService.GetById(key);
                if (media is null)
                {
                    return new NodeUrlSet { Found = false, Type = type };
                }

                if (!media.Trashed)
                {
                    var url = publishedUrlProvider.GetMediaUrl(
                        media.Key, UrlMode.Absolute, null, Constants.Conventions.Media.File);
                    AddTarget(urls, url, null);
                }

                return new NodeUrlSet { Found = true, Name = media.Name, Type = type, Urls = urls };
            }

            var content = contentService.GetById(key);
            if (content is null)
            {
                return new NodeUrlSet { Found = false, Type = type };
            }

            if (!content.Trashed)
            {
                if (content.ContentType.Variations.VariesByCulture())
                {
                    foreach (var culture in content.PublishedCultures)
                    {
                        var url = publishedUrlProvider.GetUrl(content.Key, UrlMode.Absolute, culture);
                        AddTarget(urls, url, culture);
                    }
                }
                else if (content.Published)
                {
                    var url = publishedUrlProvider.GetUrl(content.Key, UrlMode.Absolute);
                    AddTarget(urls, url, null);
                }
            }

            return new NodeUrlSet { Found = true, Name = content.Name, Type = type, Urls = urls };
        }
    }

    public async Task<NodeScanResponse> ScanNodeAsync(Guid key, string entityType, CancellationToken cancellationToken = default)
    {
        var set = ResolveUrls(key, entityType);

        if (!set.Found)
        {
            return new NodeScanResponse
            {
                Found = false,
                Type = set.Type,
                Message = set.Type == "Media" ? "Media item not found." : "Content node not found."
            };
        }

        if (set.Urls.Count == 0)
        {
            return new NodeScanResponse
            {
                Found = true,
                Name = set.Name,
                Type = set.Type,
                Message = set.Type == "Media"
                    ? "This media item has no public file URL to scan."
                    : "This node has no published public URL to scan. Publish it first."
            };
        }

        var results = new List<NodeScanUrlResult>();
        foreach (var target in set.Urls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var report = await viewer.FetchUrlAsync(BuildRequest(target.Url), cancellationToken);
                results.Add(new NodeScanUrlResult { Url = target.Url, Culture = target.Culture, Report = report });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Node scan failed for {Url}.", target.Url);
                results.Add(new NodeScanUrlResult
                {
                    Url = target.Url,
                    Culture = target.Culture,
                    Report = new UrlViewerResponse { Success = false, RequestedUrl = target.Url, ErrorMessage = ex.Message }
                });
            }
        }

        return new NodeScanResponse { Found = true, Name = set.Name, Type = set.Type, Results = results };
    }

    private static void AddTarget(List<NodeUrl> urls, string? url, string? culture)
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

        urls.Add(new NodeUrl { Url = url, Culture = culture });
    }

    // Splits the absolute URL into scheme + rest, as expected by the viewer request. Cloaking
    // detection is kept ON here (single node, low cost) to give the fullest report.
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
            SkipCloakingCheck = _options.SkipCloakingCheck
        };
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using uTPro.Feature.UrlViewer.Models;
using uTPro.Feature.UrlViewer.SiteScan;

namespace uTPro.Feature.UrlViewer.Services;

/// <summary>
/// Fetches a URL as a chosen user-agent, follows the redirect chain manually (so the DNS-based
/// SSRF guard runs on every hop), reads the body and runs content analysis / cloaking detection.
///
/// The implementation is split across partial files for readability:
/// <list type="bullet">
///   <item><c>UrlViewerService.cs</c> — public <see cref="FetchUrlAsync"/> orchestration.</item>
///   <item><c>UrlViewerService.Fetch.cs</c> — single-hop / body fetch and redirect following.</item>
///   <item><c>UrlViewerService.Ssrf.cs</c> — the DNS-based private/local host guard.</item>
///   <item><c>UrlViewerService.Parsing.cs</c> — regex/HTML analysis and UA/referrer resolution.</item>
/// </list>
/// </summary>
public partial class UrlViewerService : IUrlViewerService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UrlViewerService> _logger;
    private readonly SiteScanOptions _siteScanOptions;

    private const int MaxContentSize = 10 * 1024 * 1024;
    private const int MaxRedirects = 15;

    // Buffer size used to read the response body in chunks. Small enough that tiny responses
    // never force a large allocation, large enough to keep the read loop cheap.
    private const int BodyReadChunkSize = 81920;

    public UrlViewerService(
        IHttpClientFactory httpClientFactory,
        ILogger<UrlViewerService> logger,
        IOptions<SiteScanOptions> siteScanOptions)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _siteScanOptions = siteScanOptions.Value;
    }

    /// <summary>
    /// Whether private/local hosts may be fetched. Sourced from server-side configuration ONLY
    /// (<c>uTPro:Feature:UrlViewer:SiteScan:AllowInternalHosts</c>) and never from the request body.
    /// Defaults to <c>false</c> (deny) in production.
    /// </summary>
    private bool AllowInternalHosts => _siteScanOptions.AllowInternalHosts;

    public async Task<UrlViewerResponse> FetchUrlAsync(UrlViewerRequest request, CancellationToken cancellationToken = default)
    {
        var response = new UrlViewerResponse();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the full URL
            string scheme = request.Scheme?.ToLowerInvariant() == "http" ? "http" : "https";
            string url = request.Url.Trim();

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                url = url["http://".Length..];
            else if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = url["https://".Length..];

            string fullUrl = $"{scheme}://{url}";
            response.RequestedUrl = fullUrl;

            if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri))
            {
                response.Success = false;
                response.ErrorMessage = $"Invalid URL: {fullUrl}";
                return response;
            }

            if (!AllowInternalHosts && await IsPrivateOrLocalHostAsync(uri, cancellationToken))
            {
                response.Success = false;
                response.ErrorMessage = "Fetching private or local addresses is not allowed.";
                return response;
            }

            string userAgent = ResolveUserAgent(request.UserAgent);
            response.UserAgentUsed = userAgent;

            string referrer = ResolveReferrer(request.Referrer);
            response.ReferrerUsed = referrer;

            // VirusTotal URL
            response.VirusTotalUrl = $"https://www.virustotal.com/gui/url/{uri.Host}/detection";

            // Follow redirects manually to capture each hop
            var currentUri = uri;
            int redirectCount = 0;

            while (redirectCount < MaxRedirects)
            {
                var hop = await FetchSingleHopAsync(currentUri, userAgent, referrer, cancellationToken);
                response.RedirectChain.Add(hop);

                bool isRedirect = hop.StatusCode is >= 300 and < 400;
                if (!isRedirect)
                {
                    // Final response - read content
                    response.StatusCode = hop.StatusCode;
                    response.StatusDescription = hop.StatusDescription;
                    response.FinalUrl = currentUri.ToString();
                    response.ContentType = hop.Headers.GetValueOrDefault("Content-Type", string.Empty);

                    // Read body content
                    response.HtmlContent = await FetchBodyAsync(currentUri, userAgent, referrer, cancellationToken);
                    response.ContentLength = response.HtmlContent.Length;
                    break;
                }

                // Follow redirect
                string? location = hop.Headers.GetValueOrDefault("Location");
                if (string.IsNullOrEmpty(location))
                {
                    response.StatusCode = hop.StatusCode;
                    response.StatusDescription = hop.StatusDescription;
                    response.FinalUrl = currentUri.ToString();
                    response.ErrorMessage = "Redirect without Location header.";
                    break;
                }

                // Resolve relative URLs
                if (Uri.TryCreate(currentUri, location, out var nextUri))
                {
                    if (!AllowInternalHosts && await IsPrivateOrLocalHostAsync(nextUri, cancellationToken))
                    {
                        response.Success = false;
                        response.ErrorMessage = "Redirect leads to a private/local address.";
                        return response;
                    }
                    currentUri = nextUri;
                }
                else
                {
                    response.ErrorMessage = $"Invalid redirect location: {location}";
                    break;
                }

                redirectCount++;
            }

            if (redirectCount >= MaxRedirects)
            {
                response.Success = false;
                response.ErrorMessage = $"Too many redirects (>{MaxRedirects}).";
                return response;
            }

            // Perform content analysis
            response.Analysis = AnalyzeContent(response.HtmlContent);

            // Cloaking detection: fetch BOTH bot and Chrome at the ORIGINAL URL
            // WITHOUT following redirects, then compare status + title + content.
            // Skipped for bulk scans (request.SkipCloakingCheck) to reduce HTTP requests.
            bool isBotUA = request.UserAgent is "googlebot-smartphone" or "googlebot-desktop" or "bingbot";
            if (isBotUA && !request.SkipCloakingCheck)
            {
                try
                {
                    var targetUri = new Uri(fullUrl);
                    string chromeUA = UserAgentPresets.Agents["chrome"];

                    // Fetch bot response at original URL (no redirect follow) — single request
                    var (botBody, botStatus) = await FetchNoRedirectAsync(targetUri, userAgent, referrer, cancellationToken);

                    // Fetch Chrome response at original URL (no redirect follow) — single request
                    var (chromeBody, chromeStatus) = await FetchNoRedirectAsync(targetUri, chromeUA, string.Empty, cancellationToken);

                    response.Analysis.Cloaking = CompareBotVsChrome(
                        botBody, chromeBody, "Googlebot", botStatus, chromeStatus);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cloaking check failed for {Url}", response.RequestedUrl);
                    response.Analysis.Cloaking = new CloakingResult
                    {
                        IsCloaked = false,
                        Messages = [$"Cloaking check error: {ex.Message}"]
                    };
                }
            }
            else
            {
                response.Analysis.Cloaking = new CloakingResult { IsCloaked = false };
            }

            response.ScannedAtUtc = DateTime.UtcNow;
            response.Success = true;
        }
        catch (TaskCanceledException)
        {
            response.Success = false;
            response.ErrorMessage = "Request timed out after 30 seconds.";
        }
        catch (HttpRequestException ex)
        {
            response.Success = false;
            response.ErrorMessage = $"HTTP request failed: {ex.Message}";
            _logger.LogWarning(ex, "Failed to fetch URL: {Url}", response.RequestedUrl);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.ErrorMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error fetching URL: {Url}", response.RequestedUrl);
        }
        finally
        {
            stopwatch.Stop();
            response.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        }

        return response;
    }
}

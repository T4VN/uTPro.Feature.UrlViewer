using System.Buffers;
using System.Text;
using uTPro.Feature.UrlViewer.Models;

namespace uTPro.Feature.UrlViewer.Services;

/// <summary>
/// Low-level HTTP fetching for <see cref="UrlViewerService"/>. Auto-redirect is always disabled on
/// the client ("UrlViewerNoRedirect") so the DNS-based SSRF guard can be applied to every hop.
/// </summary>
public partial class UrlViewerService
{
    /// <summary>
    /// Fetch a single hop (headers only, no auto-redirect).
    /// </summary>
    private async Task<RedirectHop> FetchSingleHopAsync(Uri uri, string userAgent, string referrer, CancellationToken ct)
    {
        var hop = new RedirectHop { Url = uri.ToString() };

        var client = _httpClientFactory.CreateClient("UrlViewerNoRedirect");
        client.Timeout = TimeSpan.FromSeconds(30);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        httpRequest.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (!string.IsNullOrEmpty(referrer))
            httpRequest.Headers.TryAddWithoutValidation("Referer", referrer);
        httpRequest.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        httpRequest.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
        httpRequest.Headers.TryAddWithoutValidation("Cache-Control", "no-cache, no-store");
        httpRequest.Headers.TryAddWithoutValidation("Pragma", "no-cache");

        using var httpResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        hop.StatusCode = (int)httpResponse.StatusCode;
        hop.StatusDescription = httpResponse.StatusCode.ToString();

        // Build raw headers string (like Hugo's tool shows)
        var rawHeaders = new StringBuilder();
        rawHeaders.AppendLine($"HTTP/{httpResponse.Version} {hop.StatusCode} {hop.StatusDescription}");

        foreach (var header in httpResponse.Headers)
        {
            string val = string.Join(", ", header.Value);
            hop.Headers[header.Key] = val;
            rawHeaders.AppendLine($"{header.Key}: {val}");
        }
        foreach (var header in httpResponse.Content.Headers)
        {
            string val = string.Join(", ", header.Value);
            hop.Headers[header.Key] = val;
            rawHeaders.AppendLine($"{header.Key}: {val}");
        }

        hop.RawHeaders = rawHeaders.ToString();
        return hop;
    }

    /// <summary>
    /// Fetch the body content of the final URL.
    /// Auto-redirect is disabled; any redirects are followed manually so the DNS-based SSRF guard
    /// can be applied to every hop (unless <see cref="AllowInternalHosts"/> is enabled server-side).
    /// </summary>
    private async Task<string> FetchBodyAsync(Uri uri, string userAgent, string referrer, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("UrlViewerNoRedirect");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.MaxResponseContentBufferSize = MaxContentSize;

        var currentUri = uri;
        int redirectCount = 0;

        while (true)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, currentUri);
            httpRequest.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            if (!string.IsNullOrEmpty(referrer))
                httpRequest.Headers.TryAddWithoutValidation("Referer", referrer);
            httpRequest.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            httpRequest.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
            httpRequest.Headers.TryAddWithoutValidation("Cache-Control", "no-cache, no-store");
            httpRequest.Headers.TryAddWithoutValidation("Pragma", "no-cache");

            using var httpResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            bool isRedirect = (int)httpResponse.StatusCode is >= 300 and < 400;
            if (isRedirect && httpResponse.Headers.Location is not null && redirectCount < MaxRedirects)
            {
                if (!Uri.TryCreate(currentUri, httpResponse.Headers.Location, out var nextUri))
                    return string.Empty;

                // Re-apply the SSRF guard to every redirect hop before following it.
                if (!AllowInternalHosts && await IsPrivateOrLocalHostAsync(nextUri, ct))
                    return string.Empty;

                currentUri = nextUri;
                redirectCount++;
                continue;
            }

            // Read the body in bounded chunks so a small response no longer allocates a full
            // MaxContentSize (20 MB) char buffer up-front. A pooled chunk buffer feeds a
            // StringBuilder that grows only as far as the actual content requires. The 10 MB
            // content cap is still enforced: reading stops once MaxContentSize chars are captured,
            // so anything beyond the cap is truncated exactly as before.
            var contentStream = await httpResponse.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(contentStream);

            char[] chunk = ArrayPool<char>.Shared.Rent(BodyReadChunkSize);
            try
            {
                var builder = new StringBuilder();
                int remaining = MaxContentSize;
                int read;
                while (remaining > 0 &&
                       (read = await reader.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining)), ct)) > 0)
                {
                    builder.Append(chunk, 0, read);
                    remaining -= read;
                }
                return builder.ToString();
            }
            finally
            {
                ArrayPool<char>.Shared.Return(chunk);
            }
        }
    }

    /// <summary>
    /// Fetch body + status code WITHOUT following redirects — single request per UA.
    /// </summary>
    private async Task<(string body, int statusCode)> FetchNoRedirectAsync(
        Uri uri, string userAgent, string referrer, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("UrlViewerNoRedirect");
        client.Timeout = TimeSpan.FromSeconds(30);

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (!string.IsNullOrEmpty(referrer))
            req.Headers.TryAddWithoutValidation("Referer", referrer);
        req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
        req.Headers.TryAddWithoutValidation("Cache-Control", "no-cache, no-store");

        using var resp = await client.SendAsync(req, ct);
        string body = await resp.Content.ReadAsStringAsync(ct);
        return (body, (int)resp.StatusCode);
    }
}

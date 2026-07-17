using System.Net;
using System.Net.Sockets;

namespace uTPro.Feature.UrlViewer.Services;

/// <summary>
/// SSRF protection for <see cref="UrlViewerService"/>. The guard is DNS-based: public hostnames
/// that resolve to internal addresses are still rejected. Only http/https are permitted. Whether
/// internal hosts are allowed is decided by server-side configuration only (never the request).
/// </summary>
public partial class UrlViewerService
{
    /// <summary>
    /// SSRF guard: rejects any URI whose host resolves to a loopback / private / link-local /
    /// unique-local / CGNAT / multicast / reserved address. When the host is not an IP literal it is
    /// resolved via DNS and every returned address is validated, so a public hostname that resolves
    /// to an internal address is still rejected. Only http/https schemes are permitted.
    /// </summary>
    private async Task<bool> IsPrivateOrLocalHostAsync(Uri uri, CancellationToken ct)
    {
        // Reject anything that is not http(s) (e.g. file://, ftp://, gopher://).
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return true;

        var host = uri.Host;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, ct);
            }
            catch (SocketException)
            {
                // Host cannot be resolved — treat as unsafe to fetch.
                return true;
            }
        }

        // No addresses, or ANY resolved address is private/reserved => reject.
        return addresses.Length == 0 || addresses.Any(IsPrivateOrReserved);
    }

    /// <summary>
    /// Returns <c>true</c> if the supplied address is loopback, private, link-local, unique-local,
    /// CGNAT, multicast or otherwise reserved.
    /// </summary>
    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork) // IPv4
        {
            return bytes[0] switch
            {
                0 => true,                                          // 0.0.0.0/8
                10 => true,                                         // 10.0.0.0/8
                127 => true,                                        // 127.0.0.0/8 (loopback)
                100 when bytes[1] >= 64 && bytes[1] <= 127 => true, // 100.64.0.0/10 (CGNAT)
                169 when bytes[1] == 254 => true,                   // 169.254.0.0/16 (link-local / metadata)
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,  // 172.16.0.0/12
                192 when bytes[1] == 168 => true,                   // 192.168.0.0/16
                _ => bytes[0] >= 224                                // 224.0.0.0/4 multicast + reserved
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6) // IPv6
        {
            return address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal   // fc00::/7
                || address.IsIPv6Multicast;
        }

        return true;
    }
}

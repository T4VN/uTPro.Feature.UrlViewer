using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;
using uTPro.Feature.UrlViewer.Models;
using uTPro.Feature.UrlViewer.Services;

namespace uTPro.Feature.UrlViewer.Controllers;

/// <summary>
/// Versioned, authenticated Management API for the manual URL Viewer tool.
/// Every action requires access to the Settings section and is routed under /umbraco.
/// </summary>
[VersionedApiBackOfficeRoute("utpro/url-viewer")]
[ApiExplorerSettings(GroupName = "uTPro URL Viewer")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public class UrlViewerApiController(IUrlViewerService urlViewerService) : ManagementApiControllerBase
{
    /// <summary>
    /// Fetch a URL with custom User-Agent and Referrer settings.
    /// </summary>
    [HttpPost("fetch")]
    [ProducesResponseType(typeof(UrlViewerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Fetch([FromBody] UrlViewerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { error = "URL is required." });
        }

        var result = await urlViewerService.FetchUrlAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get available User-Agent presets.
    /// </summary>
    [HttpGet("user-agents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetUserAgents()
    {
        var agents = UserAgentPresets.Agents.Select(a => new { key = a.Key, value = a.Value });
        return Ok(agents);
    }

    /// <summary>
    /// Get available Referrer presets.
    /// </summary>
    [HttpGet("referrers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetReferrers()
    {
        var referrers = ReferrerPresets.Referrers.Select(r => new { key = r.Key, value = r.Value });
        return Ok(referrers);
    }
}

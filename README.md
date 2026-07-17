# uTPro URL Viewer for Umbraco

Fetch and inspect any URL as **Googlebot**, **Bingbot** or a real **browser**, directly from your Umbraco site. Built for SEO debugging, diagnosing hacked/cloaked pages and inspecting redirects.

Supports **Umbraco 16, 17 and 18**.

[![NuGet](https://img.shields.io/nuget/v/uTPro.Feature.UrlViewer.svg)](https://www.nuget.org/packages/uTPro.Feature.UrlViewer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/uTPro.Feature.UrlViewer.svg)](https://www.nuget.org/packages/uTPro.Feature.UrlViewer)
[![Umbraco Marketplace](https://img.shields.io/badge/Umbraco-Marketplace-blue)](https://marketplace.umbraco.com/package/utpro.feature.urlviewer)
[![Umbraco 16+](https://img.shields.io/badge/Umbraco-16%2B-3544B1)](https://umbraco.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

![uTPro URL Viewer - fetch and inspect any URL as Googlebot from the Umbraco backoffice](https://raw.githubusercontent.com/T4VN/uTPro.Feature.UrlViewer/refs/heads/main/Images/Screenshots/1.0.0/ScanUrl.png)

## Features

- **Fetch as any user agent** - Googlebot (smartphone/desktop), Bingbot, Chrome, Firefox, Edge.
- **Full redirect chain** - every hop with its status code and raw response headers.
- **HTML source viewer** - line numbers, wrap toggle, copy to clipboard, spam-word highlighting.
- **Content analysis**:
  - Meta tags (including `<title>`).
  - Spam / hack keyword detection.
  - Hidden CSS rules (`display:none`, `visibility:hidden`, ...), a common cloaking technique.
  - JavaScript issues - `eval`, `document.write`, `innerHTML`, base64 obfuscation, mixed content, mismatched `<script>` tags and more.
- **Cloaking detection** - compares what a bot sees versus what Chrome sees (title, status code and content-size differences).
- **VirusTotal link** for the fetched domain.
- **Safe by default** - the fetch runs server-side (SSRF guard) and blocks private/local addresses (localhost, RFC-1918 ranges, link-local `169.254.x.x`, IPv6 loopback/ULA, `.local`, ...). The guard resolves DNS and re-checks every redirect hop.

### Site URL Scan

- Recurring background job that scans every **Content** and **Media** URL on the site.
- Stores a **summary report per run** in the database (status code, redirect count, spam/cloaking flags, JS error count, timing).
- Maintains a standing **Error URLs** list; re-scan a single URL or all failing URLs on demand.
- Auto-discovered and controllable via **uTPro Job Monitor** (optional companion package).

## Installation

```bash
dotnet add package uTPro.Feature.UrlViewer
```

## Usage

The tool lives entirely in the Umbraco **backoffice** and requires **Settings** section access.

Open the backoffice, go to the **Settings** section, and find **URL Viewer** under the **Advanced** menu. It has three views:

- **URL Viewer** - enter a URL, pick the scheme, user agent and referrer, then run the fetch to see the redirect chain, headers, HTML source and analysis.
- **Site URL Scan** - trigger/inspect scans of all Content & Media URLs and browse the latest report.
- **Error URLs** - the standing list of failing URLs, with one-click re-scan (single or all).

All calls go through the authenticated Umbraco Management API under
`/umbraco/management/api/v1/utpro/url-viewer/...` (never a public endpoint).

## Screenshots

### URL Viewer — fetch a URL as any bot/browser and inspect the redirect chain, headers, HTML source and analysis
![URL Viewer tool](https://raw.githubusercontent.com/T4VN/uTPro.Feature.UrlViewer/refs/heads/main/Images/Screenshots/1.0.0/ScanUrl.png)

### Site URL Scan — scan every Content & Media URL and browse the latest report
![Site URL Scan view](https://raw.githubusercontent.com/T4VN/uTPro.Feature.UrlViewer/refs/heads/main/Images/Screenshots/1.0.0/SiteScan.png)

### Error URLs — the standing list of failing URLs, with one-click re-scan (single or all)
![Error URLs view](https://raw.githubusercontent.com/T4VN/uTPro.Feature.UrlViewer/refs/heads/main/Images/Screenshots/1.0.0/Error.png)

### Auto-discovered by uTPro Job Monitor — the recurring Site URL Scan background job
![Site URL Scan job listed in uTPro Job Monitor](https://raw.githubusercontent.com/T4VN/uTPro.Feature.UrlViewer/refs/heads/main/Images/Screenshots/1.0.0/uTPro.Feature.UrlViewer.SiteScan.SiteUrlScanJob.png)

## Configuration

The manual URL Viewer needs no configuration. The recurring **Site URL Scan** is configured under
`uTPro:Feature:UrlViewer:SiteScan` in `appsettings.json` (all keys optional — defaults shown):

```json
{
  "uTPro": {
    "Feature": {
      "UrlViewer": {
        "SiteScan": {
          "Enabled": true,
          "Period": "24:00:00",
          "Delay": "00:05:00",
          "MaxConcurrency": 4,
          "ThrottleDelayMs": 150,
          "SkipCloakingCheck": true,
          "AllowInternalHosts": false,
          "RedirectWarningThreshold": 3,
          "MaxRunHistory": 20
        }
      }
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `true` | Master switch for the recurring scan job. |
| `Period` | `24:00:00` | How often the scan runs (`d.hh:mm:ss`). |
| `Delay` | `00:05:00` | Delay before the first run after startup. |
| `MaxConcurrency` | `4` | Max concurrent HTTP fetches (clamped 1–20). |
| `ThrottleDelayMs` | `150` | Delay after each fetch, in ms. |
| `SkipCloakingCheck` | `true` | Skip the bot-vs-Chrome cloaking check during bulk scans (faster). |
| `AllowInternalHosts` | `false` | **Security:** relax the SSRF guard to allow scanning private/local addresses. Only enable when the site runs on an internal host. |
| `RedirectWarningThreshold` | `3` | Redirect-hop count above which a result is flagged as a long chain. |
| `MaxRunHistory` | `20` | Scan runs retained in the DB before old runs are pruned. |

## Repository layout

```
uTPro.Feature.UrlViewer/
├─ src/
│  ├─ uTPro.Feature.UrlViewer/          # the NuGet package (ships to the Umbraco Marketplace)
│  └─ uTPro.Feature.UrlViewer.TestSite/ # a demo Umbraco site that references the package
├─ pack.ps1                             # deterministic clean pack -> Build/
├─ umbraco-marketplace.json
└─ uTPro.Feature.UrlViewer.sln
```

## Building the package

```powershell
pwsh ./pack.ps1
```

This wipes `bin`/`obj`, does a clean `Release` pack into `Build/`, and evicts the version from the local NuGet cache so consumers on the same machine re-extract the fresh bits.

## License

MIT

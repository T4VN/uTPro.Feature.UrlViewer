# uTPro URL Viewer for Umbraco

Fetch and inspect any URL as **Googlebot**, **Bingbot** or a real **browser**, directly from your Umbraco site. Built for SEO debugging, diagnosing hacked/cloaked pages and inspecting redirects.

Supports **Umbraco 16, 17 and 18**.

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
- **Safe by default** - the fetch runs server-side and blocks private/local addresses (localhost, RFC-1918 ranges, `.local`, ...).

## Installation

```bash
dotnet add package uTPro.Feature.UrlViewer
```

## Usage

Once installed, browse to:

```
/url-viewer/index.html
```

Enter a URL, pick the scheme, user agent and referrer, then click **View**.

You can also pre-fill and auto-run via query string, for example:

```
/url-viewer/index.html?url=example.com&scheme=https&ua=googlebot-smartphone&ref=google
```

The page calls the server endpoint `POST /api/UrlViewerApi/fetch`.

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

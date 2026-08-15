# uTPro URL Viewer for Umbraco

Fetch any URL from inside the Umbraco backoffice and see **exactly** what a search engine or browser sees — redirect chain, response headers, HTML source, and a full static analysis including a **composite SEO score**.

Supports **Umbraco 16, 17 and 18**.

[![NuGet](https://img.shields.io/nuget/v/uTPro.Feature.UrlViewer.svg)](https://www.nuget.org/packages/uTPro.Feature.UrlViewer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/uTPro.Feature.UrlViewer.svg)](https://www.nuget.org/packages/uTPro.Feature.UrlViewer)
[![Umbraco Marketplace](https://img.shields.io/badge/Umbraco-Marketplace-blue)](https://marketplace.umbraco.com/package/utpro.feature.urlviewer)
[![Umbraco 16+](https://img.shields.io/badge/Umbraco-16%2B-3544B1)](https://umbraco.com)
[![License: Free (proprietary)](https://img.shields.io/badge/License-Free%20(proprietary)-green.svg)](LICENSE.txt)

![uTPro URL Viewer for Umbraco](https://raw.githubusercontent.com/T4VN/uTPro.Feature.UrlViewer/refs/heads/main/Images/Screenshots/3.0.0/ScanUrl.png)

---

## Features

- Full redirect chain with status codes and raw headers
- HTML source viewer with line numbers and copy-to-clipboard
- Composite SEO score (0–100) with per-category sub-scores
- On-page SEO, social tags, technical SEO, content metrics
- Accessibility checks, carbon estimate (A–F rating)
- Cloaking detection (bot vs Chrome comparison)
- VirusTotal link for domain check
- REST API for CI pipelines

---

## Quick Start

```bash
dotnet add package uTPro.Feature.UrlViewer
```

| Umbraco | .NET | Target |
|---|---|---|
| 16 | .NET 9 | `net9.0` |
| 17 & 18 | .NET 10 | `net10.0` |

---

## Configuration

No configuration required. For internal/dev hosts:

| Key | Default | Description |
|-----|---------|-------------|
| `AllowInternalHosts` | `false` | Relax SSRF guard for dev sites |
| `AllowInvalidCertificates` | `false` | Accept self-signed TLS |

---

## 📖 Full Documentation

**[docs.utpro.dev/uTPro.Feature.UrlViewer](https://docs.utpro.dev/uTPro.Feature.UrlViewer/)**

---

## License

Free to use (including commercially) under a proprietary [End User License Agreement](LICENSE.txt).

---

> 📦 [NuGet](https://www.nuget.org/packages/uTPro.Feature.UrlViewer) · [GitHub](https://github.com/T4VN/uTPro.Feature.UrlViewer) · [Umbraco Marketplace](https://marketplace.umbraco.com/package/utpro.feature.urlviewer)

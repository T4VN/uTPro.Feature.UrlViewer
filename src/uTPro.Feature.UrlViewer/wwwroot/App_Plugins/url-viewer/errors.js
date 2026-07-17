import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';

import { SCAN_BASE, SCAN_ENDPOINTS, VIEWER_BASE, VIEWER_ENDPOINTS } from './config.js';
import { getJson, postJson } from './api.js';

// Splits "https://host/path" into { scheme, rest } for the viewer fetch endpoint,
// which expects the scheme and the scheme-less URL separately.
function splitScheme(url) {
    const value = (url ?? '').trim();
    if (value.toLowerCase().startsWith('http://')) return { scheme: 'http', rest: value.slice('http://'.length) };
    if (value.toLowerCase().startsWith('https://')) return { scheme: 'https', rest: value.slice('https://'.length) };
    return { scheme: 'https', rest: value };
}

/**
 * Error URLs view: the standing list of failing URLs with per-URL and bulk re-scan.
 * Each row expands into a full fetch report (status, redirect chain, analysis) so you can
 * see exactly why a URL failed without leaving the list. Re-scanning only the error set
 * keeps load low compared to a full site scan.
 */
export class UtproErrorUrlsView extends UmbLitElement {

    static properties = {
        loading: { state: true },
        errors: { state: true },
        busyUrls: { state: true },
        openUrl: { state: true },
        reportBusy: { state: true },
        reports: { state: true }
    };

    static styles = css`
        :host { display: block; padding: var(--uui-size-layout-1, 24px); }
        .toolbar { display: flex; gap: var(--uui-size-space-3, 12px); align-items: center; margin-bottom: var(--uui-size-space-4, 16px); }
        table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
        th, td { text-align: left; padding: 6px 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); vertical-align: top; }
        .issue { color: var(--uui-color-danger, #d42054); font-weight: 600; }
        .url-btn { background: none; border: none; padding: 0; font: inherit; color: var(--uui-color-interactive, #3544b1); cursor: pointer; text-align: left; text-decoration: underline; }
        .detail-cell { background: var(--uui-color-surface-alt, #f7f7f9); padding: 12px; }
        .detail-cell table { font-size: 0.8rem; }
        .detail-cell th { width: 140px; color: var(--uui-color-text-alt, #68676b); }
        .status-ok { color: var(--uui-color-positive, #2bc37c); font-weight: 600; }
        .status-bad { color: var(--uui-color-danger, #d42054); font-weight: 600; }
        .chip { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 0.75rem; background: var(--uui-color-surface, #fff); margin: 2px; }
        .chip.warn { background: #fde8ec; color: #d42054; }
        .detail-section { margin-top: 10px; }
        .detail-section-title { font-weight: 700; margin-bottom: 4px; }
    `;

    #authContext;
    #notificationContext;

    constructor() {
        super();
        this.loading = true;
        this.errors = [];
        this.busyUrls = new Set();
        this.openUrl = null;
        this.reportBusy = new Set();
        this.reports = {};

        this.consumeContext(UMB_AUTH_CONTEXT, (ctx) => { this.#authContext = ctx; });
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx; });
    }

    async connectedCallback() {
        super.connectedCallback();
        await this.#reload();
    }

    async #reload() {
        this.loading = true;
        try {
            this.errors = await getJson(this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.errors);
        } catch (e) {
            console.error(e);
            this.#notify('danger', 'Failed to load error URLs.');
        }
        this.loading = false;
    }

    async #rescanAll() {
        try {
            const res = await postJson(this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.rescanErrors, {});
            if (res.ok) {
                this.#notify('positive', 'Re-scan of error URLs started.');
                setTimeout(() => this.#reload(), 2000);
            } else {
                this.#notify('warning', 'Could not start re-scan.');
            }
        } catch (e) {
            console.error(e);
            this.#notify('danger', 'Re-scan request failed.');
        }
    }

    async #rescanUrl(url) {
        this.busyUrls = new Set(this.busyUrls).add(url);
        try {
            const res = await postJson(this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.rescanUrl, { url });
            if (res.ok && res.body?.success) {
                this.#notify('positive', 'URL is healthy again and was removed from the error list.');
            } else {
                this.#notify('warning', `Still failing: ${res.body?.errorMessage ?? res.body?.statusCode ?? 'error'}`);
            }
            await this.#reload();
        } catch (e) {
            console.error(e);
            this.#notify('danger', 'Re-scan request failed.');
        } finally {
            const next = new Set(this.busyUrls);
            next.delete(url);
            this.busyUrls = next;
        }
    }

    // Toggles the inline detail report for a URL, fetching it on first open.
    async #toggleReport(url) {
        if (this.openUrl === url) {
            this.openUrl = null;
            return;
        }
        this.openUrl = url;
        if (!this.reports[url]) {
            await this.#loadReport(url);
        }
    }

    // Fetches the full viewer report for a URL. Whether the site's own private/local addresses
    // may be fetched is decided server-side via configuration (SiteScan:AllowInternalHosts); the
    // client can no longer request it, which closes the SSRF bypass.
    async #loadReport(url) {
        this.reportBusy = new Set(this.reportBusy).add(url);
        try {
            const { scheme, rest } = splitScheme(url);
            const res = await postJson(this.#authContext, VIEWER_BASE, VIEWER_ENDPOINTS.fetch, {
                url: rest,
                scheme,
                userAgent: 'googlebot-smartphone',
                referrer: 'google'
            });
            this.reports = { ...this.reports, [url]: res.body ?? { success: false, errorMessage: 'No response.' } };
        } catch (e) {
            console.error(e);
            this.reports = { ...this.reports, [url]: { success: false, errorMessage: 'Failed to fetch report.' } };
        } finally {
            const next = new Set(this.reportBusy);
            next.delete(url);
            this.reportBusy = next;
        }
    }

    #notify(color, message) {
        this.#notificationContext?.peek(color, { data: { message } });
    }

    render() {
        return html`
            <uui-box headline="Error URLs">
                <div class="toolbar">
                    <uui-button look="primary" label="Scan all errors"
                        ?disabled=${this.errors.length === 0} @click=${this.#rescanAll}>
                        Scan all errors
                    </uui-button>
                    <uui-button look="secondary" label="Refresh" @click=${this.#reload}>Refresh</uui-button>
                </div>
                ${this.errors.length === 0
                    ? html`<p>No error URLs. 🎉</p>`
                    : html`
                        <table>
                            <tr><th>URL</th><th>Type</th><th>Status</th><th>Error</th><th>Fails</th><th>Last failed</th><th></th></tr>
                            ${this.errors.map(e => this.#renderRow(e))}
                        </table>`}
            </uui-box>
        `;
    }

    #renderRow(e) {
        const isOpen = this.openUrl === e.url;
        return html`
            <tr>
                <td>
                    <button class="url-btn" title="Show report" @click=${() => this.#toggleReport(e.url)}>${e.url}</button>
                </td>
                <td>${e.type}</td>
                <td class="issue">${e.statusCode || 'ERR'}</td>
                <td>${e.errorMessage ?? ''}</td>
                <td>${e.failureCount}</td>
                <td>${new Date(e.lastFailedUtc).toLocaleString()}</td>
                <td>
                    <uui-button look="secondary" label="Re-scan"
                        ?disabled=${this.busyUrls.has(e.url)}
                        @click=${() => this.#rescanUrl(e.url)}>
                        ${this.busyUrls.has(e.url) ? '…' : 'Re-scan'}
                    </uui-button>
                </td>
            </tr>
            ${isOpen ? html`
                <tr>
                    <td class="detail-cell" colspan="7">${this.#renderReport(e.url)}</td>
                </tr>` : nothing}
        `;
    }

    #renderReport(url) {
        if (this.reportBusy.has(url)) return html`<div>Loading report…</div>`;
        const r = this.reports[url];
        if (!r) return html`<div>No report loaded.</div>`;
        if (!r.success && r.errorMessage) {
            return html`<div class="status-bad">${r.errorMessage}</div>`;
        }
        const a = r.analysis ?? {};
        return html`
            <table>
                <tr><th>Status</th><td class=${r.statusCode >= 400 || r.statusCode === 0 ? 'status-bad' : 'status-ok'}>${r.statusCode} ${r.statusDescription ?? ''}</td></tr>
                <tr><th>Requested</th><td>${r.requestedUrl}</td></tr>
                <tr><th>Final URL</th><td>${r.finalUrl}</td></tr>
                <tr><th>Content-Type</th><td>${r.contentType}</td></tr>
                <tr><th>Elapsed</th><td>${r.elapsedMilliseconds} ms</td></tr>
                <tr><th>User-Agent</th><td>${r.userAgentUsed}</td></tr>
            </table>

            ${(r.redirectChain?.length ?? 0) > 0 ? html`
                <div class="detail-section">
                    <div class="detail-section-title">Redirect chain (${r.redirectChain.length})</div>
                    <table>
                        <tr><th>#</th><th>Status</th><th>URL</th></tr>
                        ${r.redirectChain.map((h, i) => html`<tr><td>${i + 1}</td><td>${h.statusCode}</td><td>${h.url}</td></tr>`)}
                    </table>
                </div>` : nothing}

            ${a.cloaking?.isCloaked ? html`
                <div class="detail-section">
                    <div class="detail-section-title">⚠ Cloaking detected</div>
                    ${(a.cloaking.messages ?? []).map(m => html`<span class="chip warn">${m}</span>`)}
                </div>` : nothing}

            ${(a.spamWords?.length ?? 0) > 0 ? html`
                <div class="detail-section">
                    <div class="detail-section-title">Spam/hack keywords (${a.spamWords.length})</div>
                    ${a.spamWords.map(s => html`<span class="chip warn">${s.word} (${s.count})</span>`)}
                </div>` : nothing}

            ${(a.jsIssues?.length ?? 0) > 0 ? html`
                <div class="detail-section">
                    <div class="detail-section-title">JavaScript issues (${a.jsIssues.length})</div>
                    <table>
                        <tr><th>Severity</th><th>Category</th><th>Message</th></tr>
                        ${a.jsIssues.map(j => html`<tr><td>${j.severity}</td><td>${j.category}</td><td>${j.message}</td></tr>`)}
                    </table>
                </div>` : nothing}

            ${(a.metaTags?.length ?? 0) > 0 ? html`
                <div class="detail-section">
                    <div class="detail-section-title">Meta tags (${a.metaTags.length})</div>
                    <table>
                        ${a.metaTags.map(m => html`<tr><th>${m.name}</th><td>${m.content}</td></tr>`)}
                    </table>
                </div>` : nothing}
        `;
    }
}

customElements.define('utpro-error-urls-view', UtproErrorUrlsView);
export default UtproErrorUrlsView;

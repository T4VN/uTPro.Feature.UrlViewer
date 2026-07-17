import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';

import { VIEWER_BASE, VIEWER_ENDPOINTS } from './config.js';
import { getJson, postJson } from './api.js';

/**
 * URL Viewer tool, rendered fully inside the backoffice and talking to the
 * authenticated Management API (no public static page / anonymous endpoint).
 */
export class UtproUrlViewerDashboard extends UmbLitElement {

    static properties = {
        url: { state: true },
        scheme: { state: true },
        userAgent: { state: true },
        referrer: { state: true },
        userAgents: { state: true },
        referrers: { state: true },
        loading: { state: true },
        result: { state: true }
    };

    static styles = css`
        :host { display: block; padding: var(--uui-size-layout-1, 24px); }
        .form { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4, 16px); align-items: flex-end; margin-bottom: var(--uui-size-space-5, 20px); }
        .field { display: flex; flex-direction: column; gap: 4px; }
        .field label { font-size: 0.8rem; color: var(--uui-color-text-alt, #68676b); }
        .grow { flex: 1 1 320px; }
        uui-box { margin-bottom: var(--uui-size-space-4, 16px); }
        table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
        th, td { text-align: left; padding: 6px 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); vertical-align: top; }
        .status-ok { color: var(--uui-color-positive, #2bc37c); font-weight: 600; }
        .status-bad { color: var(--uui-color-danger, #d42054); font-weight: 600; }
        .chip { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 0.75rem; background: var(--uui-color-surface-alt, #f3f3f5); margin: 2px; }
        .chip.warn { background: #fde8ec; color: #d42054; }
        pre { white-space: pre-wrap; word-break: break-all; font-size: 0.78rem; background: var(--uui-color-surface-alt, #f7f7f9); padding: 8px; border-radius: 4px; max-height: 240px; overflow: auto; }
    `;

    #authContext;
    #notificationContext;

    constructor() {
        super();
        this.url = '';
        this.scheme = 'https';
        this.userAgent = 'googlebot-smartphone';
        this.referrer = 'google';
        this.userAgents = [];
        this.referrers = [];
        this.loading = false;
        this.result = null;

        this.consumeContext(UMB_AUTH_CONTEXT, (ctx) => { this.#authContext = ctx; });
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx; });
    }

    async connectedCallback() {
        super.connectedCallback();
        try {
            this.userAgents = await getJson(this.#authContext, VIEWER_BASE, VIEWER_ENDPOINTS.userAgents);
            this.referrers = await getJson(this.#authContext, VIEWER_BASE, VIEWER_ENDPOINTS.referrers);
        } catch (e) {
            console.error('Failed to load presets', e);
        }
    }

    async #fetch() {
        if (!this.url.trim()) {
            this.#notify('warning', 'Enter a URL first.');
            return;
        }
        this.loading = true;
        this.result = null;
        try {
            const res = await postJson(this.#authContext, VIEWER_BASE, VIEWER_ENDPOINTS.fetch, {
                url: this.url,
                scheme: this.scheme,
                userAgent: this.userAgent,
                referrer: this.referrer
            });
            this.result = res.body;
            if (!res.ok) this.#notify('danger', 'Fetch failed.');
        } catch (e) {
            console.error(e);
            this.#notify('danger', 'Fetch request failed.');
        }
        this.loading = false;
    }

    #notify(color, message) {
        this.#notificationContext?.peek(color, { data: { message } });
    }

    render() {
        return html`
            <uui-box headline="URL Viewer">
                <div class="form">
                    <div class="field">
                        <label>Scheme</label>
                        <uui-select
                            .options=${[{ name: 'https', value: 'https', selected: this.scheme === 'https' }, { name: 'http', value: 'http', selected: this.scheme === 'http' }]}
                            @change=${(e) => this.scheme = e.target.value}></uui-select>
                    </div>
                    <div class="field grow">
                        <label>URL (without scheme)</label>
                        <uui-input .value=${this.url} placeholder="example.com/page"
                            @input=${(e) => this.url = e.target.value}
                            @keydown=${(e) => { if (e.key === 'Enter') this.#fetch(); }}></uui-input>
                    </div>
                    <div class="field">
                        <label>User-Agent</label>
                        <uui-select
                            .options=${this.userAgents.map(a => ({ name: a.key, value: a.key, selected: a.key === this.userAgent }))}
                            @change=${(e) => this.userAgent = e.target.value}></uui-select>
                    </div>
                    <div class="field">
                        <label>Referrer</label>
                        <uui-select
                            .options=${this.referrers.map(r => ({ name: r.key, value: r.key, selected: r.key === this.referrer }))}
                            @change=${(e) => this.referrer = e.target.value}></uui-select>
                    </div>
                    <uui-button look="primary" label="Fetch" ?disabled=${this.loading} @click=${this.#fetch}>
                        ${this.loading ? 'Fetching…' : 'Fetch'}
                    </uui-button>
                </div>
                ${this.result ? this.#renderResult() : nothing}
            </uui-box>
        `;
    }

    #renderResult() {
        const r = this.result;
        if (!r) return nothing;
        if (!r.success && r.errorMessage) {
            return html`<div class="status-bad">${r.errorMessage}</div>`;
        }
        const a = r.analysis ?? {};
        return html`
            <uui-box headline="Response">
                <table>
                    <tr><th>Status</th><td class=${r.statusCode >= 400 || r.statusCode === 0 ? 'status-bad' : 'status-ok'}>${r.statusCode} ${r.statusDescription ?? ''}</td></tr>
                    <tr><th>Requested</th><td>${r.requestedUrl}</td></tr>
                    <tr><th>Final URL</th><td>${r.finalUrl}</td></tr>
                    <tr><th>Content-Type</th><td>${r.contentType}</td></tr>
                    <tr><th>Elapsed</th><td>${r.elapsedMilliseconds} ms</td></tr>
                    <tr><th>User-Agent</th><td>${r.userAgentUsed}</td></tr>
                </table>
            </uui-box>

            ${(r.redirectChain?.length ?? 0) > 0 ? html`
            <uui-box headline="Redirect chain (${r.redirectChain.length})">
                <table>
                    <tr><th>#</th><th>Status</th><th>URL</th></tr>
                    ${r.redirectChain.map((h, i) => html`<tr><td>${i + 1}</td><td>${h.statusCode}</td><td>${h.url}</td></tr>`)}
                </table>
            </uui-box>` : nothing}

            ${a.cloaking?.isCloaked ? html`
            <uui-box headline="⚠ Cloaking detected">
                ${(a.cloaking.messages ?? []).map(m => html`<div class="chip warn">${m}</div>`)}
            </uui-box>` : nothing}

            ${(a.spamWords?.length ?? 0) > 0 ? html`
            <uui-box headline="Spam/hack keywords (${a.spamWords.length})">
                ${a.spamWords.map(s => html`<span class="chip warn">${s.word} (${s.count})</span>`)}
            </uui-box>` : nothing}

            ${(a.jsIssues?.length ?? 0) > 0 ? html`
            <uui-box headline="JavaScript issues (${a.jsIssues.length})">
                <table>
                    <tr><th>Severity</th><th>Category</th><th>Message</th></tr>
                    ${a.jsIssues.map(j => html`<tr><td>${j.severity}</td><td>${j.category}</td><td>${j.message}</td></tr>`)}
                </table>
            </uui-box>` : nothing}

            ${(a.metaTags?.length ?? 0) > 0 ? html`
            <uui-box headline="Meta tags (${a.metaTags.length})">
                <table>
                    ${a.metaTags.map(m => html`<tr><th>${m.name}</th><td>${m.content}</td></tr>`)}
                </table>
            </uui-box>` : nothing}
        `;
    }
}

customElements.define('utpro-url-viewer-dashboard', UtproUrlViewerDashboard);
export default UtproUrlViewerDashboard;

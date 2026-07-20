import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { html, css, nothing } from '@umbraco-cms/backoffice/external/lit';

import { UTPRO_NODE_SCAN_CONTEXT } from './node-scan.context.js';

/**
 * Node-level URL Scan tab. The actual scan is run by UtproNodeScanContext as soon as the editor
 * opens (so an issue can be flagged via notification without opening this tab). This view just
 * renders whatever the context has produced, and offers a manual Re-scan.
 */
export class UtproNodeScanView extends UmbLitElement {

    static properties = {
        status: { state: true },
        response: { state: true },
        hasIssue: { state: true }
    };

    static styles = css`
        :host { display: block; padding: var(--uui-size-layout-1, 24px); }
        .toolbar { display: flex; gap: var(--uui-size-space-3, 12px); align-items: center; margin-bottom: var(--uui-size-space-4, 16px); }
        .hint { color: var(--uui-color-text-alt, #68676b); font-size: 0.85rem; }
        uui-box { margin-bottom: var(--uui-size-space-4, 16px); }
        table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
        th, td { text-align: left; padding: 6px 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); vertical-align: top; }
        th { width: 150px; color: var(--uui-color-text-alt, #68676b); }
        .url-head { display: flex; gap: var(--uui-size-space-3, 12px); align-items: center; }
        .culture { display: inline-block; padding: 1px 8px; border-radius: 8px; font-size: 0.72rem; background: var(--uui-color-surface-alt, #f3f3f5); }
        .status-ok { color: var(--uui-color-positive, #2bc37c); font-weight: 600; }
        .status-bad { color: var(--uui-color-danger, #d42054); font-weight: 600; }
        .chip { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 0.75rem; background: var(--uui-color-surface-alt, #f3f3f5); margin: 2px; }
        .chip.warn { background: #fde8ec; color: #d42054; }
        .detail-section { margin-top: 10px; }
        .detail-section-title { font-weight: 700; margin-bottom: 4px; }
    `;

    #scanContext;

    constructor() {
        super();
        this.status = 'idle';
        this.response = null;
        this.hasIssue = false;

        this.consumeContext(UTPRO_NODE_SCAN_CONTEXT, (ctx) => {
            this.#scanContext = ctx;
            if (!ctx) return;
            this.observe(ctx.state, (state) => {
                this.status = state?.status ?? 'idle';
                this.response = state?.response ?? null;
                this.hasIssue = !!state?.hasIssue;
            }, 'utproNodeScanState');
        });
    }

    #rescan() {
        this.#scanContext?.scan(true);
    }

    render() {
        const loading = this.status === 'loading';
        return html`
            <uui-box>
                <div slot="headline" class="url-head">
                    <span>URL Scan</span>
                    ${this.hasIssue
                        ? html`<uui-tag color="danger" look="primary"><uui-icon name="icon-alert"></uui-icon>&nbsp;Issue found</uui-tag>`
                        : nothing}
                </div>
                <div class="toolbar">
                    <uui-button look="secondary" label="Re-scan" ?disabled=${loading} @click=${this.#rescan}>
                        ${loading ? 'Scanning…' : 'Re-scan'}
                    </uui-button>
                    ${loading ? html`<uui-loader-circle></uui-loader-circle>` : nothing}
                </div>
                <p class="hint">
                    This node is scanned automatically when opened. The report covers status, redirect
                    chain, spam/hack keywords, hidden CSS, meta tags, JavaScript issues and cloaking.
                </p>
            </uui-box>
            ${this.#renderBody()}
        `;
    }

    #renderBody() {
        if (this.status === 'loading') {
            return html`<uui-box><uui-loader></uui-loader></uui-box>`;
        }
        if (this.status === 'error') {
            return html`<uui-box><div class="status-bad">Scan failed. Click Re-scan to try again.</div></uui-box>`;
        }
        if (!this.response) {
            return nothing;
        }
        return this.#renderResponse();
    }

    #renderResponse() {
        const r = this.response;
        if (!r.found) {
            return html`<uui-box><div class="status-bad">${r.message ?? 'Node not found.'}</div></uui-box>`;
        }
        if (!r.results || r.results.length === 0) {
            return html`<uui-box><p>${r.message ?? 'No public URL to scan.'}</p></uui-box>`;
        }
        return html`${r.results.map((item) => this.#renderResult(item))}`;
    }

    #renderResult(item) {
        const r = item.report ?? {};
        return html`
            <uui-box>
                <div slot="headline" class="url-head">
                    <span>${item.url}</span>
                    ${item.culture ? html`<span class="culture">${item.culture}</span>` : nothing}
                </div>
                ${(!r.success && r.errorMessage)
                    ? html`<div class="status-bad">${r.errorMessage}</div>`
                    : this.#renderReport(r)}
            </uui-box>
        `;
    }

    #renderReport(r) {
        const a = r.analysis ?? {};
        const bad = r.statusCode >= 400 || r.statusCode === 0;
        return html`
            <table>
                <tr><th>Status</th><td class=${bad ? 'status-bad' : 'status-ok'}>${r.statusCode} ${r.statusDescription ?? ''}</td></tr>
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
                    ${(a.cloaking.messages ?? []).map((m) => html`<span class="chip warn">${m}</span>`)}
                </div>` : nothing}

            ${(a.spamWords?.length ?? 0) > 0 ? html`
                <div class="detail-section">
                    <div class="detail-section-title">Spam/hack keywords (${a.spamWords.length})</div>
                    ${a.spamWords.map((s) => html`<span class="chip warn">${s.word} (${s.count})</span>`)}
                </div>` : nothing}

            ${(a.jsIssues?.length ?? 0) > 0 ? html`
                <div class="detail-section">
                    <div class="detail-section-title">JavaScript issues (${a.jsIssues.length})</div>
                    <table>
                        <tr><th>Severity</th><th>Category</th><th>Message</th></tr>
                        ${a.jsIssues.map((j) => html`<tr><td>${j.severity}</td><td>${j.category}</td><td>${j.message}</td></tr>`)}
                    </table>
                </div>` : nothing}

            ${(a.metaTags?.length ?? 0) > 0 ? html`
                <div class="detail-section">
                    <div class="detail-section-title">Meta tags (${a.metaTags.length})</div>
                    <table>
                        ${a.metaTags.map((m) => html`<tr><th>${m.name}</th><td>${m.content}</td></tr>`)}
                    </table>
                </div>` : nothing}
        `;
    }
}

customElements.define('utpro-node-scan-view', UtproNodeScanView);
export default UtproNodeScanView;

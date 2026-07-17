import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';

import { SCAN_BASE, SCAN_ENDPOINTS } from './config.js';
import { getJson, postJson } from './api.js';

/**
 * Site Scan report view: trigger a full scan, list recent runs and inspect per-URL results.
 */
export class UtproSiteScanView extends UmbLitElement {

    static properties = {
        loading: { state: true },
        isRunning: { state: true },
        runs: { state: true },
        selectedRunId: { state: true },
        results: { state: true },
        filter: { state: true }
    };

    static styles = css`
        :host { display: block; padding: var(--uui-size-layout-1, 24px); }
        .toolbar { display: flex; gap: var(--uui-size-space-3, 12px); align-items: center; margin-bottom: var(--uui-size-space-4, 16px); }
        uui-box { margin-bottom: var(--uui-size-space-4, 16px); }
        table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
        th, td { text-align: left; padding: 6px 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); }
        tr.clickable { cursor: pointer; }
        tr.clickable:hover { background: var(--uui-color-surface-alt, #f7f7f9); }
        .issue { color: var(--uui-color-danger, #d42054); font-weight: 600; }
        .ok { color: var(--uui-color-positive, #2bc37c); }
        .chip { display: inline-block; padding: 1px 6px; border-radius: 8px; font-size: 0.72rem; background: #fde8ec; color: #d42054; margin-right: 4px; }
    `;

    #authContext;
    #notificationContext;
    #pollTimer;

    constructor() {
        super();
        this.loading = true;
        this.isRunning = false;
        this.runs = [];
        this.selectedRunId = null;
        this.results = [];
        this.filter = 'All';

        this.consumeContext(UMB_AUTH_CONTEXT, (ctx) => { this.#authContext = ctx; });
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx; });
    }

    async connectedCallback() {
        super.connectedCallback();
        await this.#reload();
    }

    disconnectedCallback() {
        super.disconnectedCallback();
        clearTimeout(this.#pollTimer);
    }

    async #reload() {
        this.loading = true;
        try {
            const status = await getJson(this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.status);
            this.isRunning = !!status.isRunning;
            this.runs = await getJson(this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.runs(20));
        } catch (e) {
            console.error(e);
            this.#notify('danger', 'Failed to load scan runs.');
        }
        this.loading = false;

        if (this.isRunning) {
            this.#pollTimer = setTimeout(() => this.#reload(), 3000);
        }
    }

    async #runScan() {
        try {
            const res = await postJson(this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.run, {});
            if (res.ok) {
                this.#notify('positive', 'Scan started.');
                this.isRunning = true;
                setTimeout(() => this.#reload(), 1000);
            } else {
                this.#notify('warning', 'Could not start scan.');
            }
        } catch (e) {
            console.error(e);
            this.#notify('danger', 'Scan request failed.');
        }
    }

    async #openRun(runId) {
        this.selectedRunId = runId;
        await this.#loadResults();
    }

    async #loadResults() {
        if (!this.selectedRunId) return;
        try {
            this.results = await getJson(
                this.#authContext, SCAN_BASE,
                SCAN_ENDPOINTS.results(this.selectedRunId, this.filter === 'All' ? null : this.filter));
        } catch (e) {
            console.error(e);
            this.#notify('danger', 'Failed to load results.');
        }
    }

    #notify(color, message) {
        this.#notificationContext?.peek(color, { data: { message } });
    }

    render() {
        return html`
            <uui-box headline="Site URL Scan">
                <div class="toolbar">
                    <uui-button look="primary" label="Run full scan" ?disabled=${this.isRunning} @click=${this.#runScan}>
                        ${this.isRunning ? 'Scan running…' : 'Run full scan'}
                    </uui-button>
                    <uui-button look="secondary" label="Refresh" @click=${this.#reload}>Refresh</uui-button>
                    ${this.isRunning ? html`<uui-loader-circle></uui-loader-circle>` : nothing}
                </div>
                ${this.runs.length === 0
                    ? html`<p>No scan runs yet. Run a full scan to get started.</p>`
                    : this.#renderRuns()}
            </uui-box>
            ${this.selectedRunId ? this.#renderResults() : nothing}
        `;
    }

    #renderRuns() {
        return html`
            <table>
                <tr><th>Started</th><th>Trigger</th><th>State</th><th>Total</th><th>OK</th><th>Failed</th><th>Issues</th></tr>
                ${this.runs.map(run => html`
                    <tr class="clickable" @click=${() => this.#openRun(run.runId)}>
                        <td>${new Date(run.startUtc).toLocaleString()}</td>
                        <td>${run.trigger}</td>
                        <td>${run.state}</td>
                        <td>${run.totalTargets}</td>
                        <td class="ok">${run.successCount}</td>
                        <td class=${run.failureCount > 0 ? 'issue' : ''}>${run.failureCount}</td>
                        <td class=${run.issueCount > 0 ? 'issue' : ''}>${run.issueCount}</td>
                    </tr>`)}
            </table>
        `;
    }

    #renderResults() {
        return html`
            <uui-box headline="Results">
                <div class="toolbar">
                    <uui-select
                        .options=${['All', 'IssuesOnly', 'FailuresOnly', 'SpamOnly', 'CloakingOnly'].map(f => ({ name: f, value: f, selected: f === this.filter }))}
                        @change=${(e) => { this.filter = e.target.value; this.#loadResults(); }}></uui-select>
                </div>
                <table>
                    <tr><th>URL</th><th>Type</th><th>Status</th><th>Redirects</th><th>Flags</th><th>ms</th></tr>
                    ${this.results.map(row => html`
                        <tr>
                            <td>${row.url}</td>
                            <td>${row.type}</td>
                            <td class=${row.success ? 'ok' : 'issue'}>${row.statusCode || (row.errorMessage ?? 'ERR')}</td>
                            <td>${row.redirectCount}</td>
                            <td>
                                ${row.hasSpam ? html`<span class="chip">spam</span>` : nothing}
                                ${row.hasCloaking ? html`<span class="chip">cloaking</span>` : nothing}
                                ${row.jsErrorCount > 0 ? html`<span class="chip">js:${row.jsErrorCount}</span>` : nothing}
                            </td>
                            <td>${row.elapsedMs}</td>
                        </tr>`)}
                </table>
            </uui-box>
        `;
    }
}

customElements.define('utpro-site-scan-view', UtproSiteScanView);
export default UtproSiteScanView;

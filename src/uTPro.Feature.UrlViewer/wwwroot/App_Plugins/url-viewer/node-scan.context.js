import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import { UmbObjectState } from '@umbraco-cms/backoffice/observable-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { UMB_MEDIA_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/media';

import { SCAN_BASE, SCAN_ENDPOINTS } from './config.js';
import { postJson } from './api.js';

/** Shared token so the URL Scan tab view can read the auto-scan result. */
export const UTPRO_NODE_SCAN_CONTEXT = new UmbContextToken('uTPro.UrlViewer.NodeScanContext');

const DEFAULT_STATE = { status: 'idle', response: null, hasIssue: false };

/**
 * Workspace context for the URL Scan feature. It is created as soon as a Content/Media editor
 * opens (regardless of which tab is active), so it can scan the node's own URL(s) in the background.
 * The result is exposed as an observable; the Scan tab view renders the detail, and the workspace
 * footer app shows a short warning line when an issue is found (no intrusive notification popups,
 * which would pile up when browsing many nodes).
 */
export class UtproNodeScanContext extends UmbControllerBase {

    #state = new UmbObjectState(DEFAULT_STATE);
    /** Observable: { status: 'idle'|'loading'|'done'|'error', response, hasIssue }. */
    state = this.#state.asObservable();

    #authContext;
    #nodeKey = null;
    #entityType = 'document';
    #scannedKey = null;

    constructor(host) {
        super(host);
        this.provideContext(UTPRO_NODE_SCAN_CONTEXT, this);

        this.consumeContext(UMB_AUTH_CONTEXT, (ctx) => { this.#authContext = ctx; this.#maybeScan(); });
        this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (ctx) => this.#bind(ctx, 'document'));
        this.consumeContext(UMB_MEDIA_WORKSPACE_CONTEXT, (ctx) => this.#bind(ctx, 'media'));
    }

    getState() { return this.#state.getValue(); }

    #bind(ctx, entityType) {
        if (!ctx) return;
        this.#entityType = entityType;
        this.observe(ctx.unique, (unique) => {
            this.#nodeKey = unique ?? null;
            this.#maybeScan();
        }, 'utproNodeScanContextKey');
    }

    // Auto-scans once per node the first time we have both auth + a node key.
    #maybeScan() {
        if (this.#authContext && this.#nodeKey && this.#scannedKey !== this.#nodeKey) {
            this.scan(false);
        }
    }

    /** Runs a scan. Pass force=true to re-scan the same node (used by the tab's Re-scan button). */
    async scan(force = false) {
        if (!this.#authContext || !this.#nodeKey) return;
        if (!force && this.#scannedKey === this.#nodeKey) return;
        this.#scannedKey = this.#nodeKey;

        this.#state.setValue({ status: 'loading', response: null, hasIssue: false });
        try {
            const res = await postJson(this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.nodeScan, {
                key: this.#nodeKey,
                entityType: this.#entityType
            });
            const response = res.body;
            const hasIssue = this.#computeIssue(response);
            this.#state.setValue({ status: 'done', response, hasIssue });
        } catch (e) {
            console.error('Node URL auto-scan failed', e);
            this.#state.setValue({ status: 'error', response: null, hasIssue: true });
        }
    }

    // A result is an "issue" if any URL failed, returned 4xx/5xx, or flagged spam/cloaking/JS errors.
    #computeIssue(response) {
        if (!response || !response.found) return false;
        return (response.results ?? []).some((item) => {
            const r = item.report ?? {};
            const a = r.analysis ?? {};
            const bad = !r.success || r.statusCode >= 400 || r.statusCode === 0;
            const spam = (a.spamWords?.length ?? 0) > 0;
            const cloak = a.cloaking?.isCloaked === true;
            const jsError = (a.jsIssues ?? []).some((j) => String(j.severity).toLowerCase() === 'error');
            return bad || spam || cloak || jsError;
        });
    }
}

export default UtproNodeScanContext;

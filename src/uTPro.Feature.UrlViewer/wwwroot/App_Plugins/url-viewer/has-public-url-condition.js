import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { UMB_MEDIA_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/media';

import { SCAN_BASE, SCAN_ENDPOINTS } from './config.js';
import { getJson } from './api.js';

/**
 * Workspace-view condition: permitted only when the current Content/Media node actually has a
 * public URL that can be scanned. Rather than guessing the client-side URL shape (which differs
 * between backoffice versions), it asks the server using the very same URL-resolution logic the
 * scan itself uses — so "has a scannable URL" and "tab is shown" always agree. Folders / unrouted
 * nodes resolve to zero URLs and keep the tab hidden.
 */
export class UtproHasPublicUrlCondition extends UmbConditionBase {

    #authContext;
    #nodeKey;
    #entityType = 'document';

    constructor(host, args) {
        super(host, args);

        this.consumeContext(UMB_AUTH_CONTEXT, (ctx) => {
            this.#authContext = ctx;
            this.#check();
        });

        // Only one of these resolves depending on which editor hosts the tab.
        this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (ctx) => this.#bind(ctx, 'document'));
        this.consumeContext(UMB_MEDIA_WORKSPACE_CONTEXT, (ctx) => this.#bind(ctx, 'media'));
    }

    #bind(ctx, entityType) {
        if (!ctx) return;
        this.#entityType = entityType;
        this.observe(ctx.unique, (unique) => {
            this.#nodeKey = unique ?? null;
            this.#check();
        }, 'utproHasPublicUrlKey');
    }

    async #check() {
        if (!this.#authContext || !this.#nodeKey) {
            this.permitted = false;
            return;
        }
        try {
            const res = await getJson(
                this.#authContext, SCAN_BASE, SCAN_ENDPOINTS.nodeHasUrl(this.#nodeKey, this.#entityType));
            this.permitted = !!res?.hasUrl;
        } catch (e) {
            console.error('URL Scan tab condition check failed', e);
            this.permitted = false;
        }
    }
}

export default UtproHasPublicUrlCondition;

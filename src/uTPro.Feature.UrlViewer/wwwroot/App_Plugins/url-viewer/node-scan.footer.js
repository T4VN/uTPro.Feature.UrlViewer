import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { html, css, nothing } from '@umbraco-cms/backoffice/external/lit';

import { UTPRO_NODE_SCAN_CONTEXT } from './node-scan.context.js';

/**
 * Workspace footer app for Content/Media editors. Renders a short, unobtrusive warning line
 * (left of the Save buttons) when the background URL scan for the current node found a problem.
 * This replaces the toast notification, which piled up when browsing many nodes.
 */
export class UtproNodeScanFooter extends UmbLitElement {

    static properties = {
        hasIssue: { state: true }
    };

    static styles = css`
        :host { display: inline-flex; align-items: center; }
        .warn {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            color: var(--uui-color-danger, #d42054);
            font-size: 0.85rem;
            font-weight: 600;
            white-space: nowrap;
        }
    `;

    constructor() {
        super();
        this.hasIssue = false;

        this.consumeContext(UTPRO_NODE_SCAN_CONTEXT, (ctx) => {
            if (!ctx) return;
            this.observe(ctx.state, (state) => {
                this.hasIssue = !!state?.hasIssue;
            }, 'utproNodeScanFooterState');
        });
    }

    render() {
        if (!this.hasIssue) return nothing;
        return html`
            <span class="warn" title="Open the Scan tab for details">
                <uui-icon name="icon-alert"></uui-icon> URL scan: issue found
            </span>
        `;
    }
}

customElements.define('utpro-node-scan-footer', UtproNodeScanFooter);
export default UtproNodeScanFooter;

// Static configuration for the uTPro URL Viewer backoffice views.

export const VIEWER_BASE = '/umbraco/management/api/v1/utpro/url-viewer';
export const SCAN_BASE = '/umbraco/management/api/v1/utpro/url-scan';

export const VIEWER_ENDPOINTS = {
    fetch: 'fetch',
    userAgents: 'user-agents',
    referrers: 'referrers'
};

export const SCAN_ENDPOINTS = {
    status: 'status',
    run: 'run',
    runs: (limit = 20) => `runs?limit=${encodeURIComponent(limit)}`,
    results: (runId, filter) => `runs/${encodeURIComponent(runId)}/results${filter ? `?filter=${encodeURIComponent(filter)}` : ''}`,
    errors: 'errors',
    rescanErrors: 'errors/rescan',
    rescanUrl: 'errors/rescan-url',
    nodeScan: 'node-scan',
    nodeHasUrl: (key, entityType) =>
        `node-has-url?key=${encodeURIComponent(key)}&entityType=${encodeURIComponent(entityType || 'document')}`
};

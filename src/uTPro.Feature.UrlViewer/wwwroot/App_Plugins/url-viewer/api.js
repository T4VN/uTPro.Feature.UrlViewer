// Thin wrapper around the Management API, handling backoffice auth headers.

async function authHeaders(authContext) {
    const config = authContext?.getOpenApiConfiguration();
    const headers = { 'Content-Type': 'application/json' };
    if (config?.token) {
        const token = await config.token();
        if (token) headers['Authorization'] = `Bearer ${token}`;
    }
    return { headers, credentials: config?.credentials || 'same-origin' };
}

/** GET an endpoint and return the parsed JSON body. */
export async function getJson(authContext, base, endpoint) {
    const { headers, credentials } = await authHeaders(authContext);
    const response = await fetch(`${base}/${endpoint}`, { method: 'GET', headers, credentials });
    if (!response.ok) throw new Error(`API error: ${response.status}`);
    return response.json();
}

/** POST a JSON body and return { ok, status, body }. */
export async function postJson(authContext, base, endpoint, payload) {
    const { headers, credentials } = await authHeaders(authContext);
    const response = await fetch(`${base}/${endpoint}`, {
        method: 'POST',
        headers,
        credentials,
        body: JSON.stringify(payload ?? {})
    });
    let body = null;
    try { body = await response.json(); } catch { /* no body */ }
    return { ok: response.ok, status: response.status, body };
}

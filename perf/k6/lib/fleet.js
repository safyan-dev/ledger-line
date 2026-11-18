// Tenant fleet helpers: one VU owns one tenant, with its own session and webhook secret.
// A VU never shares a tenant with another VU, so token refresh stays single-writer.

export const API_URL = (__ENV.PERF_API_URL || 'http://127.0.0.1:5082').replace(/\/$/, '');
export const RUN_ID = __ENV.PERF_RUN_ID || 'fleet-local';
export const THINK_TIME = Number(__ENV.PERF_THINK_TIME || 1);
export const PAGE_SIZE = Number(__ENV.PERF_PAGE_SIZE || 25);
export const TEST_TYPE = __ENV.PERF_TEST_TYPE || 'fleet';
export const REFRESH_MARGIN_SECONDS = Number(__ENV.PERF_REFRESH_MARGIN_SECONDS || 120);

export function loadFleet() {
  const file = __ENV.FLEET_FILE;
  if (!file) {
    throw new Error('FLEET_FILE is required. Run perf/fleet.py first.');
  }

  const fleet = JSON.parse(open(file));
  if (!fleet.tenants || fleet.tenants.length === 0) {
    throw new Error(`Fleet file ${file} has no tenants.`);
  }

  return fleet;
}

export function unique(prefix) {
  const random = Math.random().toString(36).slice(2, 8);
  return `${prefix}-${RUN_ID}-${__VU}-${__ITER}-${random}`;
}

export function tenantForVu(pool, offset = 0) {
  const tenant = pool[(__VU - 1 + offset) % pool.length];
  if (!tenant) {
    throw new Error('No tenant available for this VU; fleet is smaller than VU count.');
  }

  return tenant;
}

export function sessionFromTenant(tenant) {
  return {
    tenant,
    host: tenant.host,
    accessToken: tenant.accessToken,
    refreshToken: tenant.refreshToken,
    expiresAt: Date.parse(tenant.accessTokenExpiresAt),
  };
}

export function hasFreshToken(session) {
  return Boolean(session.accessToken) && session.expiresAt - Date.now() > REFRESH_MARGIN_SECONDS * 1000;
}

export function headersFor(session, extra = {}) {
  return {
    Host: session.host,
    Authorization: `Bearer ${session.accessToken}`,
    'content-type': 'application/json',
    ...extra,
  };
}

export function tagsFor(session, workflow) {
  return { test_type: TEST_TYPE, workflow, tenant: session.tenant.slug };
}

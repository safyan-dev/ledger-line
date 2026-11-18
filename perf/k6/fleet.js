// Fleet load test: every VU is a distinct tenant owner signing in to its own workspace.
// VUs are pinned 1:1 to tenants; the pool must be at least as large as the VU count.

import { sleep } from 'k6';
import { loadFleet, tenantForVu, sessionFromTenant, hasFreshToken, THINK_TIME } from './lib/fleet.js';
import { refreshSession } from './lib/tenant-client.js';
import { runWorkflow } from './lib/fleet-workflows.js';

const fleet = loadFleet();
const pool = fleet.tenants;

const START_VUS = Number(__ENV.PERF_START_VUS || 1);
const TARGET_VUS = Number(__ENV.PERF_TARGET_VUS || 20);
const RAMP = __ENV.PERF_RAMP || '30s';
const HOLD = __ENV.PERF_HOLD || '2m';

export const options = {
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
  scenarios: {
    fleet: {
      executor: 'ramping-vus',
      startVUs: START_VUS,
      stages: [
        { duration: RAMP, target: TARGET_VUS },
        { duration: HOLD, target: TARGET_VUS },
        { duration: '15s', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<750', 'p(99)<1500'],
  },
};

export function setup() {
  if (pool.length < TARGET_VUS) {
    throw new Error(
      `Fleet has ${pool.length} tenants but the profile ramps to ${TARGET_VUS} VUs. ` +
        'Seed more tenants with perf/fleet.py or lower PERF_TARGET_VUS.',
    );
  }

  console.log(`Fleet ${fleet.runId}: ${pool.length} tenants, plan ${fleet.planCode}`);
  return { runId: fleet.runId };
}

export default function () {
  const session = sessionFromTenant(tenantForVu(pool));

  if (!hasFreshToken(session) && !refreshSession(session)) {
    return;
  }

  try {
    runWorkflow(session);
  } catch (error) {
    // A refused request must not silently pass; surface it as a failed iteration.
    console.error(`tenant ${session.tenant.slug} iteration failed: ${error}`);
    throw error;
  } finally {
    sleep(THINK_TIME);
  }
}

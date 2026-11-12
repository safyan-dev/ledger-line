export const API_URL = (__ENV.PERF_API_URL || 'http://localhost:5082').replace(/\/$/, '');
export const WEBHOOK_SECRET = __ENV.WEBHOOK_SECRET || 'whsec_dev_ledgerline';
export const RUN_ID = __ENV.PERF_RUN_ID || 'local';
export const THINK_TIME = Number(__ENV.PERF_THINK_TIME || 1);
export const PAGE_SIZE = Number(__ENV.PERF_PAGE_SIZE || 25);
export const TEST_TYPE = __ENV.PERF_TEST_TYPE || 'manual';

// helper used for order, payment and webhook IDs.
export function unique(prefix) {
  const random = Math.random().toString(36).slice(2, 8);
  return `${prefix}-${RUN_ID}-${__VU}-${__ITER}-${random}`;
}

export function requestTags(workflow) {
  return {
    test_type: TEST_TYPE,
    workflow,
  };
}

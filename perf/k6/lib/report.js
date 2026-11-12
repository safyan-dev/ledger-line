//two artifacts automatically

import { RUN_ID } from './env.js';

function value(metric, path, fallback = '—') {
  if (!metric || !metric.values) return fallback;
  const found = metric.values[path];
  return found === undefined || found === null ? fallback : found;
}

function number(value, digits = 2) {
  return typeof value === 'number' ? value.toFixed(digits) : value;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

export function summaryOutput(data, testType) {
  const safeRunId = RUN_ID.replace(/[^a-zA-Z0-9_-]/g, '-');
  const name = `${safeRunId}-${testType}`;
  const metrics = data.metrics;

  const rows = [
    ['Requests', number(value(metrics.http_reqs, 'count', 0), 0)],
    ['Throughput', `${number(value(metrics.http_reqs, 'rate', 0))} req/s`],
    ['Average response', `${number(value(metrics.http_req_duration, 'avg'))} ms`],
    ['p50 response', `${number(value(metrics.http_req_duration, 'p(50)'))} ms`],
    ['p95 response', `${number(value(metrics.http_req_duration, 'p(95)'))} ms`],
    ['p99 response', `${number(value(metrics.http_req_duration, 'p(99)'))} ms`],
    ['Failed requests', `${number(100 * value(metrics.http_req_failed, 'rate', 0))}%`],
    ['Iterations', number(value(metrics.iterations, 'count', 0), 0)],
    ['Max VUs', number(value(metrics.vus_max, 'value', 0), 0)],
  ];

  const thresholdRows = Object.entries(data.metrics)
    .filter(([, metric]) => metric.thresholds && Object.keys(metric.thresholds).length > 0)
    .flatMap(([metricName, metric]) =>
      Object.entries(metric.thresholds).map(
        ([expression, result]) =>
          `<tr><td>${escapeHtml(metricName)}</td><td>${escapeHtml(expression)}</td><td class="${result.ok ? 'ok' : 'bad'}">${result.ok ? 'PASS' : 'FAIL'}</td></tr>`,
      ),
    )
    .join('');

  const html = `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Ledgerline ${escapeHtml(testType)} report</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; margin: 0; padding: 32px; background: #f6f7fb; color: #172033; }
    main { max-width: 960px; margin: 0 auto; background: #fff; border: 1px solid #e3e7ef; border-radius: 12px; padding: 28px; }
    h1 { margin: 0 0 4px; font-size: 24px; }
    p { color: #5d687c; }
    table { width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 14px; }
    th, td { text-align: left; padding: 10px 12px; border-bottom: 1px solid #e8ebf2; }
    th { background: #f8f9fc; color: #5d687c; font-size: 12px; text-transform: uppercase; letter-spacing: .04em; }
    td:first-child { width: 42%; color: #5d687c; }
    .ok { color: #137a51; font-weight: 600; }
    .bad { color: #b42318; font-weight: 600; }
    code { background: #f8f9fc; padding: 2px 5px; border-radius: 4px; }
  </style>
</head>
<body>
  <main>
    <h1>Ledgerline performance report</h1>
    <p>Test type: <strong>${escapeHtml(testType)}</strong> · Run ID: <code>${escapeHtml(RUN_ID)}</code> · Generated ${escapeHtml(new Date().toISOString())}</p>
    <table>
      <thead><tr><th>Metric</th><th>Value</th></tr></thead>
      <tbody>${rows.map(([label, metricValue]) => `<tr><td>${escapeHtml(label)}</td><td>${escapeHtml(metricValue)}</td></tr>`).join('')}</tbody>
    </table>
    <h2>Thresholds</h2>
    <table>
      <thead><tr><th>Metric</th><th>Threshold</th><th>Result</th></tr></thead>
      <tbody>${thresholdRows || '<tr><td colspan="3">No thresholds defined.</td></tr>'}</tbody>
    </table>
  </main>
</body>
</html>`;

  return {
    [`perf/results/${name}-summary.json`]: JSON.stringify(data, null, 2),
    [`perf/results/${name}-report.html`]: html,
  };
}

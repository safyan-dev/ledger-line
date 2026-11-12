import { runScenario } from './lib/scenario.js';
import { summaryOutput } from './lib/report.js';

export const options = {
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
  vus: 5,
  duration: '1m',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
  },
};

export default runScenario;

export function handleSummary(data) {
  return summaryOutput(data, 'baseline');
}

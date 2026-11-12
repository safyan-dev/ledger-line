import { runScenario } from './lib/scenario.js';
import { summaryOutput } from './lib/report.js';

export const options = {
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
  vus: 20,
  duration: '30m',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000', 'p(99)<2000'],
  },
};

export default runScenario;

export function handleSummary(data) {
  return summaryOutput(data, 'soak');
}

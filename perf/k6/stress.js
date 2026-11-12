import { runScenario } from './lib/scenario.js';
import { summaryOutput } from './lib/report.js';

export const options = {
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
  stages: [
    { duration: '2m', target: 10 },
    { duration: '2m', target: 25 },
    { duration: '2m', target: 50 },
    { duration: '2m', target: 100 },
    { duration: '2m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000', 'p(99)<4000'],
  },
};

export default runScenario;

export function handleSummary(data) {
  return summaryOutput(data, 'stress');
}

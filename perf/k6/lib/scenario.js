// Runs the workflow and then waits for the think time. 
// controls approximate user pace.

import { sleep } from 'k6';
import { THINK_TIME } from './env.js';
import { runWorkflow } from './workflows.js';

export function runScenario() {
  runWorkflow();
  sleep(THINK_TIME);
}

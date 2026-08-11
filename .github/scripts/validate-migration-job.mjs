import { readFileSync } from 'node:fs';
import { validateExecutionEvidence, validateJobDefinition } from './migration-job-policy.mjs';

const [command, file, ...values] = process.argv.slice(2);
try {
  if (command === 'definition') {
    validateJobDefinition(JSON.parse(readFileSync(file, 'utf8')), {
      image: values[0], environmentId: values[1], migrationIdentityId: values[2], pullIdentityId: values[3],
      releaseSha: values[4], imageDigest: values[5]
    });
  } else if (command === 'execution') {
    validateExecutionEvidence(JSON.parse(readFileSync(file, 'utf8')), readFileSync(values[0], 'utf8'), {
      operationId: values[1], releaseSha: values[2], imageDigest: values[3], classification: values[4]
    });
  } else throw new Error('unsupported validation command');
  console.log(JSON.stringify({ eventName: 'migration-job-policy-validated', command }));
} catch (error) {
  console.error(error.message);
  process.exit(1);
}

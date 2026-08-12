import { readFileSync } from 'node:fs';

const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';
const principal = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8';
const expected = new Map([
  ['5c14d19b-04c7-4dfa-83ed-9447d0ea3c33', '4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54'],
  ['fa329695-3907-4852-94f5-fda8a26a4698', '9df6bf68-4db7-4d38-b7f1-7bb26a541199'],
]);
function fail(value) { throw new Error(value); }
function parse(path) {
  const value = JSON.parse(readFileSync(path, 'utf8'));
  if (!Array.isArray(value)) fail('ambiguous_assignment_evidence');
  return value;
}
export function validateCleanupInspection(items) {
  if (!Array.isArray(items)) fail('ambiguous_assignment_evidence');
  const present = [];
  for (const item of items) {
    if (!item || typeof item !== 'object' || Array.isArray(item)) fail('ambiguous_assignment_evidence');
    const assignment = item.id?.split('/').at(-1)?.toLowerCase();
    const role = item.roleDefinitionId?.split('/').at(-1)?.toLowerCase();
    if (!expected.has(assignment) || role !== expected.get(assignment) || item.principalId?.toLowerCase() !== principal || item.scope?.toLowerCase() !== scope.toLowerCase() || present.includes(assignment)) {
      fail('assignment_identity_mismatch');
    }
    present.push(assignment);
  }
  return present;
}
export function validateZeroResidue(items) {
  if (!Array.isArray(items)) fail('ambiguous_assignment_evidence');
  if (items.length !== 0) fail('assignment_residue');
  return 0;
}
if (process.argv[1]?.endsWith('foundation-assignment-cleanup-policy.mjs')) {
  try {
    const [mode, path] = process.argv.slice(2);
    const result = mode === 'inspect' ? validateCleanupInspection(parse(path)) : mode === 'residue' ? validateZeroResidue(parse(path)) : fail('unsupported_cleanup_mode');
    process.stdout.write(Array.isArray(result) ? `${result.join(' ')}\n` : `${result}\n`);
  } catch (error) {
    process.stdout.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}

import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { validateExecutionEvidence, validateJobDefinition, validateRoleAssignmentScope } from './migration-job-policy.mjs';

const expected = { operationId: 'operation-0001', releaseSha: 'a'.repeat(40), imageDigest: `sha256:${'b'.repeat(64)}`, classification: 'ExecutionChannelComplete' };
function envelope() {
  const payload = { operationId: expected.operationId, releaseSha: expected.releaseSha, imageDigest: expected.imageDigest, classification: expected.classification, processExitCode: 0 };
  return { eventName: 'migration-job-completion', payload, envelopeChecksum: createHash('sha256').update(JSON.stringify(payload)).digest('hex') };
}
const succeeded = { properties: { status: 'Succeeded' } };

test('accepts one valid exact-execution envelope', () => assert.ok(validateExecutionEvidence(succeeded, JSON.stringify(envelope()), expected)));
test('rejects missing logs', () => assert.throws(() => validateExecutionEvidence(succeeded, '', expected), /logs are missing/));
test('rejects multiple envelopes', () => assert.throws(() => validateExecutionEvidence(succeeded, `${JSON.stringify(envelope())}\n${JSON.stringify(envelope())}`, expected), /found 2/));
test('rejects checksum mismatch', () => { const value = envelope(); value.envelopeChecksum = '0'.repeat(64); assert.throws(() => validateExecutionEvidence(succeeded, JSON.stringify(value), expected), /checksum mismatch/); });
test('rejects terminal failure', () => assert.throws(() => validateExecutionEvidence({ properties: { status: 'Failed' } }, JSON.stringify(envelope()), expected), /did not succeed/));
test('rejects polling timeout represented by nonterminal status', () => assert.throws(() => validateExecutionEvidence({ properties: { status: 'Running' } }, JSON.stringify(envelope()), expected), /did not succeed/));

function job() {
  return { identity: { type: 'UserAssigned', userAssignedIdentities: { migration: {}, pull: {} } }, properties: { environmentId: 'environment', configuration: { triggerType: 'Manual', replicaTimeout: 900, replicaRetryLimit: 0, manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }, registries: [{ identity: 'pull' }] }, template: { containers: [{ name: 'database-migrator', image: 'registry/repository@sha256:digest', command: ['/app/container-entrypoint.sh'], args: ['--verify-execution-channel'], env: [{ name: 'ADVENTURESSUITE_RELEASE_SHA', value: 'a'.repeat(40) }, { name: 'ADVENTURESSUITE_IMAGE_DIGEST', value: 'sha256:digest' }] }] } } };
}
const definitionExpected = { image: 'registry/repository@sha256:digest', environmentId: 'environment', migrationIdentityId: 'migration', pullIdentityId: 'pull', releaseSha: 'a'.repeat(40), imageDigest: 'sha256:digest' };
test('accepts exact reviewed runtime configuration', () => assert.equal(validateJobDefinition(job(), definitionExpected), true));
test('rejects runtime configuration drift', () => { const value = job(); value.properties.configuration.replicaRetryLimit = 1; assert.throws(() => validateJobDefinition(value, definitionExpected), /retry drift/); });
test('rejects broader starter role scope', () => assert.throws(() => validateRoleAssignmentScope('/subscriptions/s/resourceGroups/rg', '/subscriptions/s/resourceGroups/rg/providers/Microsoft.App/jobs/job'), /broader/));
test('IaC assigns starter and configurator only at exact Job scope', () => {
  const foundation = readFileSync('infrastructure/container-apps-migrations/foundation.bicep', 'utf8');
  const jobTemplate = readFileSync('infrastructure/container-apps-migrations/job.bicep', 'utf8');
  assert.doesNotMatch(foundation, /resource (starter|configurator)Assignment/);
  assert.match(jobTemplate, /resource starterAssignment[\s\S]*?scope: job/);
  assert.match(jobTemplate, /resource configuratorAssignment[\s\S]*?scope: job/);
});

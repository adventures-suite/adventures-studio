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
test('four IaC boundaries preserve resource and authorization separation', () => {
  const foundationResources = readFileSync('infrastructure/container-apps-migrations/foundation-resources.bicep', 'utf8');
  const foundationAccess = readFileSync('infrastructure/container-apps-migrations/foundation-access.bicep', 'utf8');
  const jobResource = readFileSync('infrastructure/container-apps-migrations/job-resource.bicep', 'utf8');
  const jobAccess = readFileSync('infrastructure/container-apps-migrations/job-access.bicep', 'utf8');
  assert.doesNotMatch(`${foundationResources}\n${jobResource}`, /Microsoft\.Authorization|roleAssignments|roleDefinitions/);
  const ordinaryReferences = `${foundationAccess}\n${jobAccess}`.split('\n').filter(line => /^resource .*'(Microsoft\.(Network|App\/managedEnvironments|App\/jobs|ContainerRegistry|ManagedIdentity|OperationalInsights))/.test(line));
  assert.ok(ordinaryReferences.length > 0);
  for (const line of ordinaryReferences) assert.match(line, / existing =/, `access-template ordinary resource must be existing: ${line}`);
  assert.match(foundationAccess, /7f951dda-4ed3-4680-a7ca-43fe172d538d/);
  assert.match(foundationAccess, /8311e382-0749-4cb8-b61a-304f252e45ec/);
  assert.match(foundationAccess, /73c42c96-874c-492b-b04d-ab87d138a893/);
  assert.match(jobAccess, /resource starterAssignment[\s\S]*?scope: job/);
  assert.doesNotMatch(`${foundationResources}\n${foundationAccess}\n${jobResource}\n${jobAccess}`, /configurator/i);
});

test('deployer action sets preserve authority separation', () => {
  const infrastructure = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/infrastructure-deployer.role.json'));
  const roleDefinition = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/rbac-role-definition-deployer.role.json'));
  const assignment = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/rbac-assignment-deployer.role.json'));
  const infraActions = infrastructure.permissions.flatMap(value => value.actions);
  const rbacActions = [...roleDefinition.permissions, ...assignment.permissions].flatMap(value => value.actions);
  assert.ok(infraActions.some(value => value.endsWith('/write')));
  assert.ok(infraActions.every(value => !value.startsWith('Microsoft.Authorization/')));
  assert.ok(rbacActions.filter(value => value.endsWith('/write')).every(value => value.startsWith('Microsoft.Authorization/') || value === 'Microsoft.Resources/deployments/write'));
  assert.ok(rbacActions.every(value => !value.startsWith('Microsoft.Network/') && !value.startsWith('Microsoft.App/jobs/write') && !value.startsWith('Microsoft.ContainerRegistry/registries/write')));
});

test('old combined templates are absent and deployment order is documented', () => {
  assert.throws(() => readFileSync('infrastructure/container-apps-migrations/foundation.bicep'));
  assert.throws(() => readFileSync('infrastructure/container-apps-migrations/job.bicep'));
  const readme = readFileSync('infrastructure/container-apps-migrations/README.md', 'utf8');
  for (const name of ['foundation-resources.bicep', 'foundation-access.bicep', 'job-resource.bicep', 'job-access.bicep']) assert.match(readme, new RegExp(name.replace('.', '\\.')));
  const positions = ['foundation-resources.bicep', 'foundation-access.bicep', 'job-resource.bicep', 'job-access.bicep'].map(name => readme.indexOf(name));
  assert.ok(positions.every((value, index) => index === 0 || value > positions[index - 1]), 'deployment order must be explicit');
  assert.throws(() => readFileSync('infrastructure/container-apps-migrations/job-access.dev.bicepparam'));
});

test('third-party GitHub Actions are pinned to full commit SHAs', () => {
  for (const path of ['.github/workflows/database-migration-job.yml', '.github/workflows/deploy-companion-api-dev.yml', '.github/workflows/deploy-dev.yml', '.github/workflows/publish-companion-testflight.yml', '.github/workflows/validate-migration-container.yml', '.github/workflows/validate-pull-request.yml', '.github/workflows/validate-sql-migrations.yml']) {
    const workflow = readFileSync(path, 'utf8');
    const uses = workflow.split('\n').map(line => line.match(/^\s*uses:\s*([^\s#]+)/)?.[1]).filter(Boolean);
    assert.ok(uses.length > 0);
    for (const action of uses.filter(value => !value.startsWith('./'))) assert.match(action, /@[0-9a-f]{40}$/, `${path}: ${action} is not pinned`);
  }
});

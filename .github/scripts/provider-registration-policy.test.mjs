import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { policy, validateAuthorityWindow, validateCatalog, validateCleanupEvidence, validateDeadline, validateProviderEvidence } from './provider-registration-policy.mjs';

const read = path => JSON.parse(fs.readFileSync(path, 'utf8'));
const registrarPath = 'infrastructure/container-apps-migrations/roles/provider-registration.role.json';

test('fixed identities and deterministic IDs remain exact', () => {
  assert.deepEqual(policy.providers, ['Microsoft.App', 'Microsoft.ContainerRegistry']);
  assert.equal(policy.registrationPrincipalId, 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8');
  assert.equal(policy.registrationAssignmentId, '3327e40f-74ee-42e5-a0ee-e8002b125cb3');
});

test('accepts only the exact registration catalog', () => {
  assert.equal(validateCatalog('registration', read(registrarPath)).classification, 'catalog_valid');
});

test('authority window fails closed after five elapsed minutes or below twenty-five remaining minutes', () => {
  const now = Date.parse('2026-08-11T12:05:00Z');
  assert.equal(validateAuthorityWindow('2026-08-11T12:00:00Z', '2026-08-11T12:30:00Z', now).deadline, Date.parse('2026-08-11T12:30:00Z'));
  for (const values of [
    ['2026-08-11T11:59:59Z', '2026-08-11T12:29:59Z'],
    ['2026-08-11T12:05:01Z', '2026-08-11T12:35:01Z'],
    ['2026-08-11T12:00:00Z', '2026-08-11T12:30:01Z'],
    ['bad', '2026-08-11T12:30:00Z'],
  ]) assert.throws(() => validateAuthorityWindow(values[0], values[1], now));
});

for (const mutation of [
  role => role.properties.permissions[0].actions.push('*'),
  role => role.properties.permissions[0].notActions.push('x'),
  role => { role.properties.assignableScopes[0] = '/subscriptions/other'; },
  role => { role.name = '00000000-0000-0000-0000-000000000000'; },
]) test('rejects catalog mutation fail closed', () => {
  const role = read(registrarPath); mutation(role);
  assert.throws(() => validateCatalog('registration', role));
});

test('deadline is absolute, future, and at most thirty minutes', () => {
  const now = Date.parse('2026-08-11T12:00:00Z');
  assert.equal(validateDeadline('2026-08-11T12:30:00Z', now), now + 1_800_000);
  for (const value of ['2026-08-11T12:30:01Z', '2026-08-11T11:59:59Z', 'not-a-date']) assert.throws(() => validateDeadline(value, now));
});

test('registration evidence requires exact initial and terminal states', () => {
  const evidence = { assignmentId: policy.registrationAssignmentId, assignmentTimestamp: '2026-08-11T12:00:00Z', authorityDeadline: '2026-08-11T12:30:00Z', classification: 'providers_registered', providers: policy.providers.map(namespace => ({ namespace, initialState: 'NotRegistered', terminalState: 'Registered' })) };
  assert.equal(validateProviderEvidence(evidence).classification, 'registration_evidence_valid');
  for (const mutate of [
    value => { value.providers[0].initialState = 'Registering'; },
    value => { value.providers[1].terminalState = 'Unregistered'; },
    value => value.providers.push({ namespace: 'Microsoft.Sql', initialState: 'NotRegistered', terminalState: 'Registered' }),
    value => { value.extra = 'ambiguous'; },
  ]) { const copy = structuredClone(evidence); mutate(copy); assert.throws(() => validateProviderEvidence(copy)); }
  assert.throws(() => validateProviderEvidence({ ...evidence, padding: 'x'.repeat(5000) }), /oversized_evidence/);
});

test('cleanup evidence rejects residue, malformed, ambiguous, and oversized results', () => {
  const exact = { classification: 'owner_cleanup_complete', assignmentId: policy.registrationAssignmentId, residualAssignments: 0 };
  assert.equal(validateCleanupEvidence(exact).classification, 'cleanup_evidence_valid');
  for (const value of [
    { ...exact, residualAssignments: 1 },
    { ...exact, assignmentId: '00000000-0000-0000-0000-000000000000' },
    { ...exact, competingClassification: 'complete' },
    { ...exact, padding: 'x'.repeat(3000) },
    null,
  ]) assert.throws(() => validateCleanupEvidence(value));
});

test('Owner cleanup targets only the deterministic assignment and verifies residue', () => {
  const runner = fs.readFileSync('.github/scripts/run-provider-registration-owner-cleanup.sh', 'utf8');
  assert.match(runner, new RegExp(policy.registrationAssignmentId));
  assert.equal((runner.match(/role assignment delete/g) ?? []).length, 1);
  assert.doesNotMatch(runner, /role assignment (create|update)|role definition|provider (register|unregister)/);
});

test('registration runner fails closed on assignment, identity, provider, and timeout drift', () => {
  const runner = fs.readFileSync('.github/scripts/run-provider-registration.sh', 'utf8');
  for (const exact of [policy.subscriptionId, policy.registrationClientId, policy.registrationPrincipalId, policy.registrationRoleId, policy.registrationAssignmentId]) {
    assert.match(runner, new RegExp(exact));
  }
  assert.match(runner, /values\.length !== 1/);
  assert.match(runner, /initialState":"NotRegistered"/);
  assert.match(runner, /case "\$state" in NotRegistered\|Registering\|Registered/);
  assert.match(runner, /seq 1 120/);
  assert.match(runner, /sleep 10/);
  assert.doesNotMatch(runner, /provider unregister|role assignment (create|delete|update)/);
});

test('Owner artifacts preserve role creation and assignment separation', () => {
  const definitions = fs.readFileSync('infrastructure/container-apps-migrations/provider-registration-role-definitions.bicep', 'utf8');
  const registration = fs.readFileSync('infrastructure/container-apps-migrations/provider-registration-access.bicep', 'utf8');
  assert.equal((definitions.match(/Microsoft\.Authorization\/roleDefinitions@/g) ?? []).length, 1);
  assert.doesNotMatch(definitions, /roleAssignments@|Microsoft\.(App|ContainerRegistry)\//);
  assert.equal((registration.match(/Microsoft\.Authorization\/roleAssignments@/g) ?? []).length, 1);
  assert.match(registration, new RegExp(policy.registrationAssignmentId));
  for (const content of [registration]) {
    assert.equal((content.match(/Microsoft\.Authorization\/roleDefinitions@/g) ?? []).length, 1);
    assert.match(content, /roleDefinitions@[^\n]+ existing =/);
  }
});

test('workflow never mutates assignments and exposes separate Owner-cleanup proof', () => {
  const workflow = fs.readFileSync('.github/workflows/manage-provider-registration.yml', 'utf8');
  assert.match(workflow, /prove-owner-cleanup/);
  assert.match(workflow, /timeout-minutes: 25/);
  assert.doesNotMatch(workflow, /provider unregister/);
  assert.match(workflow, /provider-registration-denial/);
  assert.match(workflow, /if: always\(\)/);
  assert.doesNotMatch(workflow, /role assignment (create|delete|update)|role definition/);
  assert.match(workflow, /owner_cleanup_evidence_sha256/);
  assert.match(workflow, /cleanup-evidence/);
});

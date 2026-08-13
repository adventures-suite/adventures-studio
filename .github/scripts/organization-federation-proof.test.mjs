import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { FIXED, decodeClaims, validateAzureEvidence, validateGitHubClaims } from './organization-federation-proof.mjs';

const sha = 'e58c5e30adb5f36c3dae3d9c699da1c271026736';
const context = (overrides = {}) => ({
  proofTarget: 'database-migration', environment: 'database-development',
  repository: FIXED.repository, repositoryId: FIXED.repositoryId,
  organization: FIXED.organization, organizationId: FIXED.organizationId,
  ref: 'refs/heads/main', sourceSha: sha, ...overrides,
});
const claims = (overrides = {}) => ({
  iss: FIXED.issuer, aud: FIXED.oidcAudience,
  sub: `repo:adventures-suite@316268438/adventures-studio@1317655952:environment:database-development`,
  repository: FIXED.repository, repository_id: FIXED.repositoryId,
  repository_owner: FIXED.organization, repository_owner_id: FIXED.organizationId,
  ref: 'refs/heads/main', sha, environment: 'database-development', ...overrides,
});
const account = (overrides = {}) => ({
  environmentName: 'AzureCloud', homeTenantId: FIXED.tenantId,
  id: FIXED.subscriptionId, isDefault: true, name: 'Development', state: 'Enabled',
  tenantId: FIXED.tenantId,
  user: { name: 'd0da8236-91dc-4454-8a3d-19d08a406e5d', type: 'servicePrincipal' },
  ...overrides,
});

test('accepts exact immutable organization federation evidence', () => {
  const evidence = validateAzureEvidence(account(), claims(), context());
  assert.equal(evidence.classification, 'organization_federation_verified');
  assert.equal(evidence.clientIdVerified, true);
  assert.equal(evidence.subjectVerified, true);
});

for (const [name, changedContext] of [
  ['repository', { repository: 'adventures-suite/other' }],
  ['repository ID', { repositoryId: '1' }],
  ['organization', { organization: 'other' }],
  ['organization ID', { organizationId: '1' }],
  ['SHA', { sourceSha: '0'.repeat(40) }],
  ['ref', { ref: 'refs/heads/other' }],
  ['Environment', { environment: 'dev' }],
  ['target', { proofTarget: 'other' }],
]) test(`rejects wrong ${name}`, () => assert.throws(() => validateGitHubClaims(claims(), context(changedContext))));

for (const [name, changedClaims] of [
  ['old personal subject', { sub: 'repo:ssimonton007@55812276/adventures-studio@1317655952:environment:database-development' }],
  ['issuer', { iss: 'https://example.invalid' }],
  ['audience', { aud: 'other' }],
  ['repository claim', { repository_id: '1' }],
  ['organization claim', { repository_owner_id: '1' }],
  ['SHA claim', { sha: '0'.repeat(40) }],
  ['Environment claim', { environment: 'dev' }],
]) test(`rejects wrong ${name}`, () => assert.throws(() => validateGitHubClaims(claims(changedClaims), context())));

test('rejects malformed and oversized tokens', () => {
  assert.throws(() => decodeClaims('malformed'));
  assert.throws(() => decodeClaims('x'.repeat(16_385)));
});

test('rejects malformed Azure evidence and unexpected identity', () => {
  assert.throws(() => validateAzureEvidence(account({ extra: true }), claims(), context()));
  assert.throws(() => validateAzureEvidence(account({ tenantId: 'wrong' }), claims(), context()));
  assert.throws(() => validateAzureEvidence(account({ user: { name: 'wrong', type: 'servicePrincipal' } }), claims(), context()));
  assert.throws(() => validateAzureEvidence(account({ user: { type: 'servicePrincipal' } }), claims(), context()));
});

test('bounded evidence contains exact client verification and no raw client ID, token, or URL fields', () => {
  const evidence = validateAzureEvidence(account(), claims(), context());
  assert.equal(evidence.clientIdVerified, true);
  assert.equal(Object.hasOwn(evidence, 'clientId'), false);
  const serialized = JSON.stringify(evidence);
  assert.doesNotMatch(serialized, /d0da8236-91dc-4454-8a3d-19d08a406e5d/i);
  assert.doesNotMatch(serialized, /token|authorization|header|url|request/i);
});

test('clientIdVerified is the exact boolean true in successful evidence', () => {
  const evidence = validateAzureEvidence(account(), claims(), context());
  assert.strictEqual(evidence.clientIdVerified, true);
  assert.equal(typeof evidence.clientIdVerified, 'boolean');
});

test('workflow is manual-only, statically Environment-bound, pinned, and mutation-free', () => {
  const workflow = readFileSync(new URL('../workflows/prove-organization-federation.yml', import.meta.url), 'utf8');
  assert.match(workflow, /^\s{2}workflow_dispatch:/m);
  assert.doesNotMatch(workflow, /^\s{2}(push|pull_request|schedule|workflow_run):/m);
  for (const environment of ['dev', 'migration-foundation-deployment', 'migration-rbac-deployment', 'database-development'])
    assert.match(workflow, new RegExp(`environment: ${environment.replaceAll('-', '\\-')}`));
  assert.equal((workflow.match(/uses: actions\/checkout@[0-9a-f]{40}/g) ?? []).length, 5);
  assert.equal((workflow.match(/uses: azure\/login@[0-9a-f]{40}/g) ?? []).length, 5);
  assert.equal((workflow.match(/timeout-minutes: 5/g) ?? []).length, 5);
  assert.equal((workflow.match(/cancel-in-progress: false/g) ?? []).length, 1);
  assert.doesNotMatch(workflow, /az\s+(deployment|role|resource|webapp|sql|group|network|identity\s+(create|delete|update))/i);
  assert.doesNotMatch(workflow, /sqlcmd|dotnet\s+run|--migrate|az\s+webapp|upload-artifact|attest-build-provenance/i);
  assert.doesNotMatch(workflow, /client-secret|password|secrets\./i);
});

test('proof script invokes only bounded read-only Azure account operations', () => {
  const script = readFileSync(new URL('./organization-federation-proof.mjs', import.meta.url), 'utf8');
  assert.equal((script.match(/exec\('az'/g) ?? []).length, 2);
  assert.match(script, /\['account', 'show'/);
  assert.match(script, /\['account', 'get-access-token'/);
  assert.doesNotMatch(script, /\['(?:deployment|role|resource|group|network|identity|sql|webapp)'/i);
  assert.doesNotMatch(script, /['"](?:create|delete|update|assign|remove|deploy|migrate)['"]/i);
  assert.doesNotMatch(script, /console\.(?:log|error)|process\.stdout\.write\([^`]/);
});

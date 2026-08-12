import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { validateDeployment, validateWhatIf } from './foundation-deployment-policy.mjs';
import { validateAuthorityWindow } from './foundation-authority-window.mjs';
import { validateCleanupInspection, validateZeroResidue } from './foundation-assignment-cleanup-policy.mjs';

const fixture = () => JSON.parse(readFileSync('.github/scripts/fixtures/foundation-what-if-root.json', 'utf8'));
const nullTypeFixture = () => JSON.parse(readFileSync('.github/scripts/fixtures/foundation-what-if-root-null-types.json', 'utf8'));
const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';
const identityCatalog = () => JSON.parse(readFileSync('infrastructure/container-apps-migrations/foundation-identity-catalog.dev.json', 'utf8'));
const deploymentFixture = () => JSON.parse(readFileSync('.github/scripts/fixtures/foundation-deployment-show.json', 'utf8'));

test('accepts the live-shaped root envelope and one legacy envelope', () => {
  assert.deepEqual(validateWhatIf(fixture()), { classification: 'what_if_approved', resourceCount: 4 });
  const legacy = fixture();
  legacy.properties = { changes: legacy.changes };
  delete legacy.changes;
  assert.deepEqual(validateWhatIf(legacy), { classification: 'what_if_approved', resourceCount: 4 });
});

test('derives exact simple and nested types when resourceType is null or omitted', () => {
  assert.deepEqual(validateWhatIf(nullTypeFixture()), { classification: 'what_if_approved', resourceCount: 4 });
});

test('accepts a valid present matching type case-insensitively', () => {
  const document = nullTypeFixture();
  document.changes[2].resourceType = 'microsoft.network/VIRTUALNETWORKS/Subnets';
  assert.deepEqual(validateWhatIf(document), { classification: 'what_if_approved', resourceCount: 4 });
});

test('rejects conflicting, malformed, shortened, parent-only, and unrelated supplied types', () => {
  for (const resourceType of [
    '', ' Microsoft.Network/virtualNetworks/subnets', 'Microsoft.Network//subnets',
    'Microsoft.Network/virtualNetworks', 'Microsoft.Network/subnets',
    'Microsoft.Network/virtualNetworks/subnets/child', 'Microsoft.Storage/storageAccounts', 42, {},
  ]) {
    const document = nullTypeFixture();
    document.changes[2].resourceType = resourceType;
    assert.throws(() => validateWhatIf(document), /unexpected_what_if_type/, String(resourceType));
  }
});

test('derives nested types from alternating type and name segments', () => {
  const document = nullTypeFixture();
  document.changes[2].resourceId = `${scope}/providers/Microsoft.Network/virtualNetworks/subnets/snet-container-apps-migrations`;
  assert.throws(() => validateWhatIf(document), /malformed_actionable_resource_id/);
  document.changes[2].resourceId = `${scope}/providers/Microsoft.Network/virtualNetworks/vnet-adventures-suite-dev/subnets`;
  assert.throws(() => validateWhatIf(document), /malformed_actionable_resource_id/);
});

test('rejects missing, competing, conflicting, and malformed collections', () => {
  assert.throws(() => validateWhatIf({}), /malformed_what_if/);
  assert.throws(() => validateWhatIf({ changes: {} }), /malformed_what_if/);
  assert.throws(() => validateWhatIf({ properties: { changes: {} } }), /malformed_what_if/);
  for (const legacyChanges of [fixture().changes, []]) {
    const document = fixture();
    document.properties = { changes: structuredClone(legacyChanges) };
    assert.throws(() => validateWhatIf(document), /malformed_what_if/);
  }
});

test('accepts only exact Ignore observations with complete in-scope ARM IDs', () => {
  const invalidIds = [
    '', scope, `${scope}/providers/Microsoft.Network`, `${scope}/providers/Microsoft.Network/virtualNetworks`,
    `${scope}/providers/Microsoft.Network/virtualNetworks/`, `${scope}//providers/Microsoft.Network/virtualNetworks/example`,
    `${scope}/providers/Microsoft.Network/virtualNetworks/../subnets/example`,
    `${scope}/providers/Microsoft.Network/virtualNetworks/%2E%2E/subnets/example`,
    `${scope}/providers/Microsoft.Network/virtualNetworks/example?api-version=1`,
    `${scope}/providers/Microsoft.Network/virtualNetworks/example#fragment`,
    `${scope}-lookalike/providers/Microsoft.Network/virtualNetworks/example`,
    '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Network/virtualNetworks/example',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev-copy/providers/Microsoft.Network/virtualNetworks/example',
  ];
  for (const resourceId of invalidIds) {
    const document = fixture();
    document.changes[0].resourceId = resourceId;
    assert.throws(() => validateWhatIf(document), /malformed_ignored_resource_id/, resourceId);
  }
  for (const changeType of ['', 'ignore', 'NoChange', 'Modify']) {
    const document = fixture();
    document.changes[0].changeType = changeType;
    assert.throws(() => validateWhatIf(document), /unexpected_what_if_resource/, changeType);
  }
});

test('requires four unique exact Create actions and rejects substitutions', () => {
  for (const changeType of ['NoChange', 'Modify', 'Delete', 'Replace']) {
    const document = fixture();
    document.changes[2].changeType = changeType;
    assert.throws(() => validateWhatIf(document), /unexpected_what_if_operation/);
  }
  const duplicate = fixture();
  duplicate.changes[3] = structuredClone(duplicate.changes[2]);
  assert.throws(() => validateWhatIf(duplicate), /duplicate_what_if_resource/);
  const missing = fixture();
  missing.changes.splice(2, 1);
  assert.throws(() => validateWhatIf(missing), /missing_what_if_resource/);
  const extra = fixture();
  extra.changes.push({ resourceId: `${scope}/providers/Microsoft.Storage/storageAccounts/unreviewed`, resourceType: 'Microsoft.Storage/storageAccounts', changeType: 'Create' });
  assert.throws(() => validateWhatIf(extra), /unexpected_what_if_resource/);
});

test('validates every exact live-shaped deployment output against trusted identities', () => {
  const result = validateDeployment(deploymentFixture(), identityCatalog());
  assert.equal(result.classification, 'deployment_complete');
  assert.deepEqual(Object.keys(result), [
    'classification', 'provisioningState', 'registryResourceId', 'registryLoginServer',
    'logWorkspaceResourceId', 'environmentResourceId', 'migrationIdentityResourceId',
    'migrationIdentityPrincipalId', 'migrationIdentityClientId', 'pullIdentityResourceId',
    'publisherIdentityResourceId', 'starterIdentityResourceId',
  ]);
});

test('rejects drift or malformed shape in each deployment output', () => {
  for (const name of Object.keys(deploymentFixture().properties.outputs)) {
    const drift = deploymentFixture();
    drift.properties.outputs[name].value = name.endsWith('Id') ? '00000000-0000-0000-0000-000000000000' : 'unexpected';
    assert.throws(() => validateDeployment(drift, identityCatalog()), /deployment_output_identity_mismatch/, name);
    const malformed = deploymentFixture();
    malformed.properties.outputs[name].type = 'Object';
    assert.throws(() => validateDeployment(malformed, identityCatalog()), /malformed_deployment_outputs/, name);
  }
  const extra = deploymentFixture();
  extra.properties.outputs.extra = { type: 'String', value: 'no' };
  assert.throws(() => validateDeployment(extra, identityCatalog()), /unexpected_deployment_outputs/);
});

test('rejects trusted identity catalog drift and noncanonical identity GUIDs', () => {
  for (const name of Object.keys(identityCatalog())) {
    const catalog = identityCatalog();
    catalog[name] = 'wrong';
    assert.throws(() => validateDeployment(deploymentFixture(), catalog), /identity_catalog_mismatch/, name);
  }
  assert.throws(() => validateDeployment(deploymentFixture(), { ...identityCatalog(), extra: 'x' }), /malformed_identity_catalog/);
});

test('enforces deterministic strict UTC authority windows of at most 30 minutes', () => {
  assert.deepEqual(validateAuthorityWindow('2026-08-11T20:00:00Z', '2026-08-11T20:30:00Z', '2026-08-11T20:15:00Z'), {
    assignmentTimestampUtc: '2026-08-11T20:00:00Z', authorityDeadlineUtc: '2026-08-11T20:30:00Z',
  });
  assert.doesNotThrow(() => validateAuthorityWindow('2026-08-11T20:00:00Z', '2026-08-11T20:30:00Z', '2026-08-12T20:00:00Z', false));
  for (const values of [
    ['2026-08-11 20:00:00Z', '2026-08-11T20:30:00Z', '2026-08-11T20:15:00Z'],
    ['2026-08-11T20:00:00.000Z', '2026-08-11T20:30:00Z', '2026-08-11T20:15:00Z'],
    ['2026-08-11T20:00:00Z', '2026-08-11T20:30:01Z', '2026-08-11T20:15:00Z'],
    ['2026-08-11T20:00:00Z', '2026-08-11T20:30:00Z', '2026-08-11T20:30:00Z'],
    ['2026-08-11T20:00:00Z', '2026-08-11T20:30:00Z', '2026-08-11T19:59:59Z'],
  ]) assert.throws(() => validateAuthorityWindow(...values), /authority_window|invalid_authority/);
});

test('cleanup accepts both, either, or no exact deterministic assignments and is repeatable', () => {
  const assignment = (id, role) => ({ id: `${scope}/providers/Microsoft.Authorization/roleAssignments/${id}`, roleDefinitionId: `${scope}/providers/Microsoft.Authorization/roleDefinitions/${role}`, principalId: 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8', scope });
  const first = assignment('5c14d19b-04c7-4dfa-83ed-9447d0ea3c33', '4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54');
  const second = assignment('fa329695-3907-4852-94f5-fda8a26a4698', '9df6bf68-4db7-4d38-b7f1-7bb26a541199');
  assert.deepEqual(validateCleanupInspection([first, second]), ['5c14d19b-04c7-4dfa-83ed-9447d0ea3c33', 'fa329695-3907-4852-94f5-fda8a26a4698']);
  assert.deepEqual(validateCleanupInspection([first]), ['5c14d19b-04c7-4dfa-83ed-9447d0ea3c33']);
  assert.deepEqual(validateCleanupInspection([second]), ['fa329695-3907-4852-94f5-fda8a26a4698']);
  assert.deepEqual(validateCleanupInspection([]), []);
  assert.deepEqual(validateCleanupInspection([]), []);
  for (const drift of [
    [{ ...first, principalId: '00000000-0000-0000-0000-000000000000' }],
    [{ ...first, scope: `${scope}-other` }],
    [{ ...first, roleDefinitionId: second.roleDefinitionId }],
    [{ ...first, id: `${scope}/providers/Microsoft.Authorization/roleAssignments/00000000-0000-0000-0000-000000000000` }],
    [first, first], [{ unrelated: true }], null,
  ]) assert.throws(() => validateCleanupInspection(drift), /assignment|ambiguous/);
  assert.equal(validateZeroResidue([]), 0);
  assert.throws(() => validateZeroResidue([first]), /assignment_residue/);
  assert.throws(() => validateZeroResidue(null), /ambiguous_assignment_evidence/);
});

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { validateWhatIf } from './foundation-deployment-policy.mjs';

const fixture = () => JSON.parse(readFileSync('.github/scripts/fixtures/foundation-what-if-root.json', 'utf8'));
const nullTypeFixture = () => JSON.parse(readFileSync('.github/scripts/fixtures/foundation-what-if-root-null-types.json', 'utf8'));
const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';

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

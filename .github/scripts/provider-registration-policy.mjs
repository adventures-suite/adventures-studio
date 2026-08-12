#!/usr/bin/env node
import fs from 'node:fs';

export const policy = Object.freeze({
  subscriptionId: '5ace9cdd-06d1-47d9-8214-1e7c756d076a',
  subscriptionScope: '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a',
  tenantId: 'd7add2bb-ac03-49a8-9377-d0bf6a012f2f',
  registrationPrincipalId: 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8',
  registrationClientId: '223af00d-69e5-4302-9ac5-6b338f3ea2e5',
  registrationRoleId: 'fcdbbdc4-b56a-4863-aebb-32790e5b1a51',
  registrationAssignmentId: '3327e40f-74ee-42e5-a0ee-e8002b125cb3',
  providers: ['Microsoft.App', 'Microsoft.ContainerRegistry'],
});

const catalogs = {
  registration: {
    id: policy.registrationRoleId,
    name: 'AdventuresSuite Migration Provider Registrar',
    description: 'Temporary authority to read subscription provider state and register only the reviewed Container Apps foundation providers.',
    actions: ['Microsoft.Resources/subscriptions/providers/read', 'Microsoft.App/register/action', 'Microsoft.ContainerRegistry/register/action'],
  },
};

function fail(classification) { throw new Error(classification); }
function exactArray(actual, expected) {
  return Array.isArray(actual) && actual.length === expected.length && actual.every((value, index) => value === expected[index]);
}
export function validateCatalog(kind, document) {
  const expected = catalogs[kind];
  if (!expected || !document || typeof document !== 'object' || Array.isArray(document)) fail('malformed_catalog');
  const properties = document.properties;
  const permissions = properties?.permissions;
  const permission = permissions?.[0];
  if (document.name !== expected.id || Object.keys(document).sort().join(',') !== 'name,properties' ||
      properties?.roleName !== expected.name || properties?.description !== expected.description || properties?.type !== 'CustomRole' ||
      !exactArray(properties?.assignableScopes, [policy.subscriptionScope]) || !Array.isArray(permissions) || permissions.length !== 1 ||
      !exactArray(permission?.actions, expected.actions) || !exactArray(permission?.notActions, []) ||
      !exactArray(permission?.dataActions, []) || !exactArray(permission?.notDataActions, []) ||
      permission.actions.some(action => typeof action !== 'string' || action.includes('*'))) fail('unexpected_catalog');
  return { classification: 'catalog_valid', kind };
}

export function validateDeadline(value, now = Date.now()) {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(value)) fail('malformed_deadline');
  const deadline = Date.parse(value);
  if (!Number.isFinite(deadline) || deadline <= now || deadline - now > 30 * 60 * 1000) fail('deadline_out_of_bounds');
  return deadline;
}

export function validateAuthorityWindow(assignedAtValue, deadlineValue, now = Date.now()) {
  const exact = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;
  if (!exact.test(assignedAtValue) || !exact.test(deadlineValue)) fail('malformed_authority_window');
  const assignedAt = Date.parse(assignedAtValue), deadline = Date.parse(deadlineValue);
  if (![assignedAt, deadline].every(Number.isFinite) || assignedAt > now || now - assignedAt > 5 * 60 * 1000 ||
      deadline <= assignedAt || deadline - assignedAt > 30 * 60 * 1000 || deadline - now < 25 * 60 * 1000) fail('authority_window_expired');
  return { assignedAt, deadline };
}

export function validateProviderEvidence(document) {
  if (Buffer.byteLength(JSON.stringify(document)) > 4096) fail('oversized_evidence');
  if (!document || typeof document !== 'object' || Array.isArray(document) ||
      Object.keys(document).sort().join(',') !== 'assignmentId,assignmentTimestamp,authorityDeadline,classification,providers') fail('malformed_evidence');
  if (document.assignmentId !== policy.registrationAssignmentId ||
      typeof document.assignmentTimestamp !== 'string' || typeof document.authorityDeadline !== 'string') fail('unexpected_assignment');
  const timestampPattern = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;
  const assignedAt = Date.parse(document.assignmentTimestamp);
  const deadline = Date.parse(document.authorityDeadline);
  if (!timestampPattern.test(document.assignmentTimestamp) || !timestampPattern.test(document.authorityDeadline) ||
      !Number.isFinite(assignedAt) || !Number.isFinite(deadline) || deadline <= assignedAt || deadline - assignedAt > 30 * 60 * 1000) fail('invalid_authority_window');
  if (document.classification !== 'providers_registered' || !Array.isArray(document.providers) || document.providers.length !== 2) fail('unexpected_evidence');
  for (let index = 0; index < policy.providers.length; index += 1) {
    const item = document.providers[index];
    if (!item || Object.keys(item).sort().join(',') !== 'initialState,namespace,terminalState' ||
        item.namespace !== policy.providers[index] || item.initialState !== 'NotRegistered' || item.terminalState !== 'Registered') fail('unexpected_provider_state');
  }
  return { classification: 'registration_evidence_valid' };
}

export function validateCleanupEvidence(document) {
  if (Buffer.byteLength(JSON.stringify(document)) > 2048) fail('oversized_evidence');
  if (!document || typeof document !== 'object' || Array.isArray(document) || Object.keys(document).sort().join(',') !== 'assignmentId,classification,residualAssignments' ||
      document.classification !== 'owner_cleanup_complete' || document.assignmentId !== policy.registrationAssignmentId || document.residualAssignments !== 0) fail('cleanup_evidence_invalid');
  return { classification: 'cleanup_evidence_valid' };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  try {
    const [mode, path] = process.argv.slice(2);
    const document = JSON.parse(fs.readFileSync(path, 'utf8'));
    const result = mode === 'registration-catalog' ? validateCatalog('registration', document)
      : mode === 'registration-evidence' ? validateProviderEvidence(document)
      : mode === 'cleanup-evidence' ? validateCleanupEvidence(document)
      : fail('unsupported_mode');
    process.stdout.write(`${JSON.stringify(result)}\n`);
  } catch (error) {
    process.stdout.write(`${JSON.stringify({ classification: error.message || 'policy_failed' })}\n`);
    process.exitCode = 1;
  }
}

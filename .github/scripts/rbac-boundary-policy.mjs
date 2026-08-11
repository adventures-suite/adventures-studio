import { readFileSync } from 'node:fs';

const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';
const roleIds = {
  infrastructure: '4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54',
  identityReader: '9df6bf68-4db7-4d38-b7f1-7bb26a541199',
};
const assignmentIds = ['5c14d19b-04c7-4dfa-83ed-9447d0ea3c33', 'fa329695-3907-4852-94f5-fda8a26a4698'];
const broadRoleDefinitionIds = new Set([
  '8e3af657-a8ff-443c-a75c-2fe8c4bcb635', // Owner
  'b24988ac-6180-42a0-ab88-20f7382dd24c', // Contributor
]);
function fail(value) { throw new Error(value); }
function load(path) {
  const text = readFileSync(path, 'utf8');
  if (Buffer.byteLength(text) > 512 * 1024) fail('oversized_evidence');
  try { return JSON.parse(text); } catch { fail('malformed_evidence'); }
}
function selectChanges(document) {
  if (!document || typeof document !== 'object' || Array.isArray(document)) fail('malformed_what_if');
  const hasRootChanges = Object.hasOwn(document, 'changes');
  const properties = document.properties;
  const hasNestedChanges = properties !== null && typeof properties === 'object' && !Array.isArray(properties)
    && Object.hasOwn(properties, 'changes');
  if (hasRootChanges === hasNestedChanges) fail('malformed_what_if');
  const changes = hasRootChanges ? document.changes : properties.changes;
  if (!Array.isArray(changes)) fail('malformed_what_if');
  return changes;
}
function validateIgnoredResourceId(value) {
  if (typeof value !== 'string' || value.includes('?') || value.includes('#') || value.includes('\\')) fail('malformed_what_if');
  const segments = value.split('/');
  const scopeSegments = scope.split('/');
  if (segments[0] !== '' || segments.slice(1).some(segment => segment.length === 0)) fail('malformed_what_if');
  if (segments.length < scopeSegments.length + 4 || segments.length % 2 !== scopeSegments.length % 2) fail('malformed_what_if');
  for (let index = 0; index < scopeSegments.length; index += 1) {
    if (segments[index].toLowerCase() !== scopeSegments[index].toLowerCase()) fail('malformed_what_if');
  }
  if (segments[scopeSegments.length].toLowerCase() !== 'providers') fail('malformed_what_if');
  const resourceSegments = segments.slice(scopeSegments.length + 1);
  if (!/^[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)+$/.test(resourceSegments[0]) || (resourceSegments.length - 1) % 2 !== 0) fail('malformed_what_if');
  for (let index = 1; index < resourceSegments.length; index += 1) {
    const segment = resourceSegments[index];
    let decoded;
    try { decoded = decodeURIComponent(segment); } catch { fail('malformed_what_if'); }
    if (!segment || decoded === '.' || decoded === '..' || /[/?#\\\u0000-\u001f]/.test(decoded)) fail('malformed_what_if');
    if (index % 2 === 1 && !/^[A-Za-z][A-Za-z0-9.-]*$/.test(decoded)) fail('malformed_what_if');
  }
  return true;
}
function exactChanges(document, expectedIds, allowedOperations) {
  const changes = selectChanges(document);
  const expected = new Set(expectedIds.map(value => value.toLowerCase()));
  const observed = new Set();
  const actionable = [];
  for (const change of changes) {
    const id = String(change.resourceId ?? '').toLowerCase();
    if (change.changeType === 'Ignore') {
      validateIgnoredResourceId(change.resourceId);
      continue;
    }
    if (!expected.has(id) || observed.has(id)) fail('unexpected_rbac_resource');
    if (!allowedOperations.includes(change.changeType)) fail('unexpected_rbac_operation');
    observed.add(id);
    actionable.push(change);
  }
  if (observed.size !== expected.size) fail('missing_rbac_resource');
  return actionable;
}
function isBroadRoleName(value) {
  return typeof value === 'string' && /^(owner|contributor)$/i.test(value);
}
function isBroadRoleDefinitionId(value) {
  if (typeof value !== 'string') return false;
  return broadRoleDefinitionIds.has(value.toLowerCase().replace(/\/$/, '').split('/').at(-1));
}
function validateActionableBroadRoles(mode, changes) {
  for (const change of changes) {
    const after = change.after && typeof change.after === 'object' && !Array.isArray(change.after) ? change.after : {};
    const properties = after.properties && typeof after.properties === 'object' && !Array.isArray(after.properties)
      ? after.properties
      : {};
    const names = [after.roleName, after.roleDefinitionName, properties.roleName, properties.roleDefinitionName];
    const definitionIds = [after.roleDefinitionId, properties.roleDefinitionId];
    if (names.some(isBroadRoleName) || definitionIds.some(isBroadRoleDefinitionId)) fail('broad_role_substitution');
    if (mode === 'bootstrap' && (isBroadRoleDefinitionId(after.id) || isBroadRoleDefinitionId(after.name))) fail('broad_role_substitution');
  }
}
export function validateRoleCatalog(role) {
  if (role.properties?.type !== 'CustomRole') fail('unexpected_role_type');
  if (JSON.stringify(role.properties.assignableScopes) !== JSON.stringify([scope])) fail('broader_assignable_scope');
  const permissions = role.properties.permissions;
  if (!Array.isArray(permissions) || permissions.length !== 1) fail('unexpected_permissions');
  const block = permissions[0];
  if ([...(block.actions ?? []), ...(block.notActions ?? []), ...(block.dataActions ?? []), ...(block.notDataActions ?? [])].some(value => value.includes('*'))) fail('wildcard_permission');
  if ((block.actions ?? []).some(value => /roleAssignments|roleDefinitions/i.test(value))) fail('role_can_modify_rbac');
  if ((block.actions ?? []).some(value => /userAssignedIdentities\/(write|delete)|federatedIdentityCredentials/i.test(value))) fail('role_can_modify_identity');
  if ((block.actions ?? []).some(value => /(^|\/)Owner$|(^|\/)Contributor$/i.test(value))) fail('broad_role_substitution');
  return true;
}
export function validateRbacWhatIf(mode, document) {
  const ids = mode === 'bootstrap'
    ? Object.values(roleIds).map(id => `${scope}/providers/Microsoft.Authorization/roleDefinitions/${id}`)
    : assignmentIds.map(id => `${scope}/providers/Microsoft.Authorization/roleAssignments/${id}`);
  const actionable = exactChanges(document, ids, ['Create']);
  validateActionableBroadRoles(mode, actionable);
  return { classification: `${mode}_what_if_approved`, resourceCount: ids.length };
}
if (process.argv[1]?.endsWith('rbac-boundary-policy.mjs')) {
  try {
    const [mode, path] = process.argv.slice(2);
    const result = mode === 'catalog'
      ? (validateRoleCatalog(load(path)), { classification: 'role_catalog_approved' })
      : validateRbacWhatIf(mode, load(path));
    process.stdout.write(`${JSON.stringify(result)}\n`);
  } catch (error) {
    process.stdout.write(`${JSON.stringify({ classification: error.message })}\n`);
    process.exitCode = 1;
  }
}

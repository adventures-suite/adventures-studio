import { readFileSync } from 'node:fs';

const subscriptionId = '5ace9cdd-06d1-47d9-8214-1e7c756d076a';
const resourceGroup = 'rg-adventures-suite-dev';
const scope = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}`;
const expectedResources = new Map([
  [`${scope}/providers/Microsoft.Network/virtualNetworks/vnet-adventures-suite-dev/subnets/snet-container-apps-migrations`.toLowerCase(), 'Microsoft.Network/virtualNetworks/subnets'],
  [`${scope}/providers/Microsoft.OperationalInsights/workspaces/log-adventures-suite-migrations-dev`.toLowerCase(), 'Microsoft.OperationalInsights/workspaces'],
  [`${scope}/providers/Microsoft.ContainerRegistry/registries/advsuitemigrationsdev`.toLowerCase(), 'Microsoft.ContainerRegistry/registries'],
  [`${scope}/providers/Microsoft.App/managedEnvironments/cae-adventures-suite-migrations-dev`.toLowerCase(), 'Microsoft.App/managedEnvironments'],
]);
const expectedOutputs = new Set([
  'registryResourceId', 'registryLoginServer', 'logWorkspaceResourceId', 'environmentResourceId',
  'migrationIdentityResourceId', 'migrationIdentityPrincipalId', 'migrationIdentityClientId',
  'pullIdentityResourceId', 'publisherIdentityResourceId', 'starterIdentityResourceId',
]);

function fail(classification) { throw new Error(classification); }
function parse(path) {
  const text = readFileSync(path, 'utf8');
  if (Buffer.byteLength(text) > 1024 * 1024) fail('oversized_evidence');
  try { return JSON.parse(text); } catch { fail('malformed_evidence'); }
}
function containsForbidden(value) {
  const text = JSON.stringify(value).toLowerCase();
  return text.includes('microsoft.authorization/') ||
    text.includes('microsoft.managedidentity/userassignedidentities/write') ||
    text.includes('publicnetworkaccess":"enabled') ||
    text.includes('publicnetworkaccess": "enabled') ||
    text.includes('"internal":false') || text.includes('"internal": false') ||
    text.includes('"external":true') || text.includes('"external": true');
}

function selectChanges(document) {
  if (!document || typeof document !== 'object' || Array.isArray(document)) fail('malformed_what_if');
  const rootPresent = Object.prototype.hasOwnProperty.call(document, 'changes');
  const properties = document.properties;
  const legacyPresent = properties !== null && typeof properties === 'object' && !Array.isArray(properties) &&
    Object.prototype.hasOwnProperty.call(properties, 'changes');
  if (rootPresent === legacyPresent) fail('malformed_what_if');
  const changes = rootPresent ? document.changes : properties.changes;
  if (!Array.isArray(changes)) fail('malformed_what_if');
  return changes;
}

function parseResourceId(value, classification = 'malformed_ignored_resource_id') {
  if (typeof value !== 'string' || value.length === 0 || value.includes('?') || value.includes('#') ||
      value.includes('\\') || value.includes('//')) fail(classification);
  const segments = value.split('/');
  if (segments[0] !== '' || segments.some((segment, index) => index > 0 && segment.length === 0)) fail(classification);
  let decoded;
  try { decoded = segments.map(segment => decodeURIComponent(segment)); } catch { fail(classification); }
  if (decoded.some(segment => segment === '.' || segment === '..' || segment.includes('/') || segment.includes('\\'))) {
    fail(classification);
  }
  if (segments.length < 9 || segments.length % 2 === 0 ||
      segments[1].toLowerCase() !== 'subscriptions' || segments[2].toLowerCase() !== subscriptionId ||
      segments[3].toLowerCase() !== 'resourcegroups' || segments[4].toLowerCase() !== resourceGroup ||
      segments[5].toLowerCase() !== 'providers' || !/^[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)+$/.test(segments[6])) {
    fail(classification);
  }
  const typeSegments = [];
  for (let index = 7; index < segments.length; index += 2) {
    if (!/^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(segments[index]) ||
        !/^[A-Za-z0-9][A-Za-z0-9._() -]*$/.test(segments[index + 1])) fail(classification);
    typeSegments.push(segments[index]);
  }
  return { normalizedId: value.toLowerCase(), derivedType: `${segments[6]}/${typeSegments.join('/')}` };
}

function validateObservedResourceId(value) {
  return parseResourceId(value).normalizedId;
}

function validateActionableResourceType(parsedId, suppliedType, expectedType) {
  const { normalizedId, derivedType } = parsedId;
  if (derivedType.toLowerCase() !== expectedType.toLowerCase()) fail('unexpected_what_if_type');
  if (suppliedType !== undefined && suppliedType !== null) {
    if (typeof suppliedType !== 'string' || suppliedType.length === 0 || suppliedType.trim() !== suppliedType ||
        !/^[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)+(?:\/[A-Za-z0-9][A-Za-z0-9._-]*)+$/.test(suppliedType) ||
        suppliedType.toLowerCase() !== derivedType.toLowerCase() || suppliedType.toLowerCase() !== expectedType.toLowerCase()) {
      fail('unexpected_what_if_type');
    }
  }
  return normalizedId;
}

export function validateApproval(input) {
  if (input.ref !== 'refs/heads/main' || !/^[0-9a-f]{40}$/.test(input.releaseSha) || input.workflowSha !== input.releaseSha) fail('approval_sha_mismatch');
  if (!/^foundation-deploy-[A-Za-z0-9._-]{8,96}$/.test(input.approvalId ?? '')) fail('approval_id_mismatch');
  if (!/^[0-9a-f]{64}$/.test(input.templateSha ?? '') || input.templateSha !== input.actualTemplateSha) fail('template_checksum_mismatch');
  if (!/^[0-9a-f]{64}$/.test(input.parametersSha ?? '') || input.parametersSha !== input.actualParametersSha) fail('parameters_checksum_mismatch');
  if (input.subscriptionId !== subscriptionId || input.resourceGroup !== resourceGroup) fail('target_mismatch');
  if (input.clientId !== '223af00d-69e5-4302-9ac5-6b338f3ea2e5' || input.principalId !== 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8') fail('identity_mismatch');
  return true;
}

export function validateWhatIf(document) {
  const changes = selectChanges(document);
  if (containsForbidden(document)) fail('forbidden_what_if_content');
  const seen = new Set();
  for (const change of changes) {
    if (!change || typeof change !== 'object' || Array.isArray(change)) fail('malformed_what_if');
    if (change.changeType === 'Ignore') {
      validateObservedResourceId(change.resourceId);
      continue;
    }
    const parsedId = parseResourceId(change.resourceId, 'malformed_actionable_resource_id');
    const expectedType = expectedResources.get(parsedId.normalizedId);
    if (!expectedType) fail('unexpected_what_if_resource');
    const id = validateActionableResourceType(parsedId, change.resourceType, expectedType);
    if (change.changeType !== 'Create') fail('unexpected_what_if_operation');
    if (seen.has(id)) fail('duplicate_what_if_resource');
    seen.add(id);
  }
  if (seen.size !== expectedResources.size) fail('missing_what_if_resource');
  return { classification: 'what_if_approved', resourceCount: seen.size };
}

function validateTrustedIdentities(trusted) {
  if (!trusted || typeof trusted !== 'object' || Array.isArray(trusted)) fail('malformed_identity_catalog');
  const expected = {
    migrationIdentityResourceId: `${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-job-dev`,
    migrationIdentityPrincipalId: 'ffc9a4bd-67c4-44af-82dc-b7f663f8bea5',
    migrationIdentityClientId: 'd0da8236-91dc-4454-8a3d-19d08a406e5d',
    pullIdentityResourceId: `${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-pull-dev`,
    publisherIdentityResourceId: `${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-publisher-dev`,
    starterIdentityResourceId: `${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-starter-dev`,
  };
  if (Object.keys(trusted).length !== Object.keys(expected).length) fail('malformed_identity_catalog');
  for (const [name, value] of Object.entries(expected)) {
    const actual = trusted[name];
    if (typeof actual !== 'string' || (name.endsWith('ResourceId') ? actual.toLowerCase() !== value.toLowerCase() : actual.toLowerCase() !== value)) {
      fail('identity_catalog_mismatch');
    }
  }
  return expected;
}

export function validateDeployment(document, trustedIdentities) {
  const properties = document?.properties;
  if (properties?.provisioningState !== 'Succeeded') fail('deployment_not_succeeded');
  const outputs = properties.outputs;
  if (!outputs || typeof outputs !== 'object' || Array.isArray(outputs)) fail('malformed_deployment_outputs');
  if (Object.keys(outputs).length !== expectedOutputs.size || Object.keys(outputs).some(name => !expectedOutputs.has(name))) fail('unexpected_deployment_outputs');
  if (containsForbidden(outputs)) fail('forbidden_deployment_output');
  const trusted = validateTrustedIdentities(trustedIdentities);
  const exact = {
    registryResourceId: `${scope}/providers/Microsoft.ContainerRegistry/registries/advsuitemigrationsdev`,
    registryLoginServer: 'advsuitemigrationsdev.azurecr.io',
    logWorkspaceResourceId: `${scope}/providers/Microsoft.OperationalInsights/workspaces/log-adventures-suite-migrations-dev`,
    environmentResourceId: `${scope}/providers/Microsoft.App/managedEnvironments/cae-adventures-suite-migrations-dev`,
    ...trusted,
  };
  for (const [name, expected] of Object.entries(exact)) {
    const output = outputs[name];
    if (!output || Object.keys(output).length !== 2 || output.type !== 'String' || typeof output.value !== 'string') fail('malformed_deployment_outputs');
    const isId = name.endsWith('ResourceId');
    const isGuid = name.endsWith('PrincipalId') || name.endsWith('ClientId');
    if ((isId && output.value.toLowerCase() !== expected.toLowerCase()) ||
        (isGuid && (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/.test(output.value) || output.value.toLowerCase() !== expected)) ||
        (!isId && !isGuid && output.value !== expected)) fail('deployment_output_identity_mismatch');
  }
  return { classification: 'deployment_complete', provisioningState: 'Succeeded', ...Object.fromEntries(Object.keys(exact).map(name => [name, outputs[name].value])) };
}

if (process.argv[1]?.endsWith('foundation-deployment-policy.mjs')) {
  const [mode, path, trustedPath] = process.argv.slice(2);
  try {
    const result = mode === 'what-if' ? validateWhatIf(parse(path)) :
      mode === 'deployment' ? validateDeployment(parse(path), parse(trustedPath)) : fail('unsupported_policy_mode');
    process.stdout.write(`${JSON.stringify(result)}\n`);
  } catch (error) {
    process.stdout.write(`${JSON.stringify({ classification: error.message })}\n`);
    process.exitCode = 1;
  }
}

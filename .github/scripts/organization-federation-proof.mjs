import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

export const FIXED = Object.freeze({
  repository: 'adventures-suite/adventures-studio',
  repositoryId: '1317655952',
  organization: 'adventures-suite',
  organizationId: '316268438',
  tenantId: 'd7add2bb-ac03-49a8-9377-d0bf6a012f2f',
  subscriptionId: '5ace9cdd-06d1-47d9-8214-1e7c756d076a',
  issuer: 'https://token.actions.githubusercontent.com',
  oidcAudience: 'api://AzureADTokenExchange',
  azureAudience: 'https://management.azure.com/',
});

const TARGETS = Object.freeze({
  'web-dev': { environment: 'dev', clientId: '1a45a93e-9630-4df9-861b-ad9cca04a05f', principalId: 'dd9500f5-9ba7-46b5-8dbc-94038f1ef03e' },
  'companion-api-dev': { environment: 'dev', clientId: '91d49097-719d-44ae-9d8c-c394a68781e3', principalId: '96b245aa-8f4c-4ce4-a37d-aaf5c8b470bc' },
  'migration-foundation': { environment: 'migration-foundation-deployment', clientId: '223af00d-69e5-4302-9ac5-6b338f3ea2e5', principalId: 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8' },
  'migration-rbac': { environment: 'migration-rbac-deployment', clientId: 'd678e2ad-ada2-4cde-bb79-44630acf1cc8', principalId: '822c1c0c-39e1-400f-b9fc-9532a11bae5d' },
  'database-migration': { environment: 'database-development', clientId: 'd0da8236-91dc-4454-8a3d-19d08a406e5d', principalId: 'ffc9a4bd-67c4-44af-82dc-b7f663f8bea5' },
});

const exactKeys = (value, expected) =>
  value && typeof value === 'object' && !Array.isArray(value)
  && Object.keys(value).sort().join('\n') === [...expected].sort().join('\n');

const requireExact = (actual, expected, label) => {
  if (actual !== expected) throw new Error(`${label} is not approved.`);
};

export function decodeClaims(token) {
  if (typeof token !== 'string' || token.length > 16_384) throw new Error('Token evidence is malformed.');
  const parts = token.split('.');
  if (parts.length !== 3) throw new Error('Token evidence is malformed.');
  const claims = JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'));
  if (!claims || typeof claims !== 'object' || Array.isArray(claims)) throw new Error('Token claims are malformed.');
  return claims;
}

export function validateGitHubClaims(claims, context) {
  const target = TARGETS[context.proofTarget];
  if (!target) throw new Error('The proof target is not approved.');
  requireExact(context.environment, target.environment, 'Environment');
  requireExact(context.repository, FIXED.repository, 'Repository');
  requireExact(context.repositoryId, FIXED.repositoryId, 'Repository ID');
  requireExact(context.organization, FIXED.organization, 'Organization');
  requireExact(context.organizationId, FIXED.organizationId, 'Organization ID');
  requireExact(context.ref, 'refs/heads/main', 'Git ref');
  if (!/^[0-9a-f]{40}$/.test(context.sourceSha)) throw new Error('Source SHA is malformed.');

  const subject = `repo:${FIXED.organization}@${FIXED.organizationId}/adventures-studio@${FIXED.repositoryId}:environment:${target.environment}`;
  requireExact(claims.iss, FIXED.issuer, 'OIDC issuer');
  requireExact(claims.aud, FIXED.oidcAudience, 'OIDC audience');
  requireExact(claims.sub, subject, 'OIDC subject');
  requireExact(String(claims.repository_id), FIXED.repositoryId, 'OIDC repository ID');
  requireExact(String(claims.repository_owner_id), FIXED.organizationId, 'OIDC organization ID');
  requireExact(claims.repository, FIXED.repository, 'OIDC repository');
  requireExact(claims.repository_owner, FIXED.organization, 'OIDC organization');
  requireExact(claims.ref, context.ref, 'OIDC ref');
  requireExact(claims.sha, context.sourceSha, 'OIDC SHA');
  requireExact(claims.environment, target.environment, 'OIDC Environment');
  return { target, subject };
}

export function validateAzureEvidence(account, claims, context) {
  const { target, subject } = validateGitHubClaims(claims, context);
  if (!exactKeys(account, ['environmentName', 'homeTenantId', 'id', 'isDefault', 'name', 'state', 'tenantId', 'user']))
    throw new Error('Azure account evidence is malformed.');
  if (!exactKeys(account.user, ['name', 'type'])) throw new Error('Azure account identity evidence is malformed.');
  requireExact(account.id, FIXED.subscriptionId, 'Azure subscription');
  requireExact(account.tenantId, FIXED.tenantId, 'Azure tenant');
  requireExact(account.homeTenantId, FIXED.tenantId, 'Azure home tenant');
  requireExact(account.user.type, 'servicePrincipal', 'Azure account type');
  requireExact(account.user.name.toLowerCase(), target.clientId, 'Azure account client ID');

  return {
    classification: 'organization_federation_verified',
    proofTarget: context.proofTarget,
    repositoryId: FIXED.repositoryId,
    organizationId: FIXED.organizationId,
    sourceSha: context.sourceSha,
    environment: target.environment,
    tenantId: FIXED.tenantId,
    subscriptionId: FIXED.subscriptionId,
    clientId: target.clientId,
    principalId: target.principalId,
    issuerVerified: claims.iss === FIXED.issuer,
    audienceVerified: claims.aud === FIXED.oidcAudience,
    subjectVerified: claims.sub === subject,
    exitCode: 0,
  };
}

const required = (name) => {
  const value = process.env[name];
  if (!value || value.trim() !== value) throw new Error(`${name} is required.`);
  return value;
};

async function requestGitHubToken(fetchImpl = fetch) {
  const requestUrl = new URL(required('ACTIONS_ID_TOKEN_REQUEST_URL'));
  requestUrl.searchParams.set('audience', FIXED.oidcAudience);
  const response = await fetchImpl(requestUrl, {
    headers: { Authorization: `bearer ${required('ACTIONS_ID_TOKEN_REQUEST_TOKEN')}` },
    redirect: 'error',
  });
  if (!response.ok) throw new Error('GitHub OIDC token acquisition failed.');
  const body = await response.json();
  if (!exactKeys(body, ['value']) || typeof body.value !== 'string') throw new Error('GitHub OIDC response is malformed.');
  return body.value;
}

function contextFromEnvironment() {
  return {
    proofTarget: required('PROOF_TARGET'),
    environment: required('PROOF_ENVIRONMENT'),
    repository: required('GITHUB_REPOSITORY'),
    repositoryId: required('GITHUB_REPOSITORY_ID'),
    organization: required('GITHUB_REPOSITORY_OWNER'),
    organizationId: required('GITHUB_REPOSITORY_OWNER_ID'),
    ref: required('GITHUB_REF'),
    sourceSha: required('SOURCE_SHA'),
  };
}

export async function runProof(dependencies = {}) {
  const token = await requestGitHubToken(dependencies.fetchImpl);
  const claims = decodeClaims(token);
  const context = contextFromEnvironment();
  const { target } = validateGitHubClaims(claims, context);
  requireExact(required('EXPECTED_CLIENT_ID').toLowerCase(), target.clientId, 'Configured client ID');
  requireExact(required('EXPECTED_PRINCIPAL_ID').toLowerCase(), target.principalId, 'Configured principal ID');
  requireExact(required('EXPECTED_TENANT_ID').toLowerCase(), FIXED.tenantId, 'Configured tenant ID');
  requireExact(required('EXPECTED_SUBSCRIPTION_ID').toLowerCase(), FIXED.subscriptionId, 'Configured subscription ID');

  const exec = dependencies.execFileSyncImpl ?? execFileSync;
  const account = JSON.parse(exec('az', ['account', 'show', '--query',
    '{environmentName:environmentName,homeTenantId:homeTenantId,id:id,isDefault:isDefault,name:name,state:state,tenantId:tenantId,user:user}',
    '--output', 'json'], {
    encoding: 'utf8', maxBuffer: 64 * 1024, stdio: ['ignore', 'pipe', 'ignore'],
  }));
  const access = JSON.parse(exec('az', ['account', 'get-access-token', '--resource', 'https://management.azure.com/', '--query',
    '{accessToken:accessToken,subscription:subscription,tenant:tenant,tokenType:tokenType}', '--output', 'json'], {
    encoding: 'utf8', maxBuffer: 64 * 1024, stdio: ['ignore', 'pipe', 'ignore'],
  }));
  if (!exactKeys(access, ['accessToken', 'subscription', 'tenant', 'tokenType']))
    throw new Error('Azure token evidence is malformed.');
  requireExact(access.subscription, FIXED.subscriptionId, 'Azure token subscription');
  requireExact(access.tenant, FIXED.tenantId, 'Azure token tenant');
  requireExact(access.tokenType, 'Bearer', 'Azure token type');
  const azureClaims = decodeClaims(access.accessToken);
  requireExact(azureClaims.tid?.toLowerCase(), FIXED.tenantId, 'Azure token tenant claim');
  requireExact(azureClaims.oid?.toLowerCase(), target.principalId, 'Azure token principal claim');
  requireExact((azureClaims.appid ?? azureClaims.azp)?.toLowerCase(), target.clientId, 'Azure token client claim');
  requireExact(azureClaims.aud, FIXED.azureAudience, 'Azure token audience');

  const evidence = validateAzureEvidence(account, claims, context);
  process.stdout.write(`${JSON.stringify(evidence)}\n`);
  return evidence;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  runProof().catch(() => {
    process.stderr.write('organization_federation_proof_failed\n');
    process.exitCode = 1;
  });
}

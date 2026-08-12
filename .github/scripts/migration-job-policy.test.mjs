import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { validateExecutionEvidence, validateJobDefinition, validateRoleAssignmentScope } from './migration-job-policy.mjs';
import { validateApproval, validateDeployment, validateWhatIf } from './foundation-deployment-policy.mjs';
import { validateRbacWhatIf, validateRoleCatalog } from './rbac-boundary-policy.mjs';

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
test('five IaC boundaries preserve resource, identity-access, and authorization separation', () => {
  const foundationResources = readFileSync('infrastructure/container-apps-migrations/foundation-resources.bicep', 'utf8');
  const identityAccess = readFileSync('infrastructure/container-apps-migrations/identity-access.bicep', 'utf8');
  const foundationAccess = readFileSync('infrastructure/container-apps-migrations/foundation-access.bicep', 'utf8');
  const jobResource = readFileSync('infrastructure/container-apps-migrations/job-resource.bicep', 'utf8');
  const jobAccess = readFileSync('infrastructure/container-apps-migrations/job-access.bicep', 'utf8');
  assert.doesNotMatch(`${foundationResources}\n${jobResource}`, /Microsoft\.Authorization|roleAssignments|roleDefinitions/);
  assert.doesNotMatch(foundationResources, /federatedIdentityCredentials/);
  for (const line of foundationResources.split('\n').filter(line => /^resource .*userAssignedIdentities@/.test(line))) assert.match(line, / existing =/);
  assert.match(identityAccess, /federatedIdentityCredentials/);
  assert.doesNotMatch(identityAccess, /Microsoft\.(Network|App\/|ContainerRegistry|OperationalInsights|Authorization)/);
  const ordinaryReferences = `${identityAccess}\n${foundationAccess}\n${jobAccess}`.split('\n').filter(line => /^resource .*'(Microsoft\.(Network|App\/managedEnvironments|App\/jobs|ContainerRegistry|ManagedIdentity\/userAssignedIdentities@|OperationalInsights))/.test(line));
  assert.ok(ordinaryReferences.length > 0);
  for (const line of ordinaryReferences) assert.match(line, / existing =/, `access-template ordinary resource must be existing: ${line}`);
  assert.match(foundationAccess, /7f951dda-4ed3-4680-a7ca-43fe172d538d/);
  assert.match(foundationAccess, /8311e382-0749-4cb8-b61a-304f252e45ec/);
  assert.match(foundationAccess, /73c42c96-874c-492b-b04d-ab87d138a893/);
  assert.match(jobAccess, /resource starterAssignment[\s\S]*?scope: job/);
  assert.doesNotMatch(`${foundationResources}\n${identityAccess}\n${foundationAccess}\n${jobResource}\n${jobAccess}`, /configurator/i);
});

test('deployer action sets preserve authority separation', () => {
  const infrastructure = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/infrastructure-deployer.role.json'));
  const roleDefinition = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/rbac-role-definition-deployer.role.json'));
  const assignment = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/rbac-assignment-deployer.role.json'));
  const federation = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/identity-federation-deployer.role.json'));
  const infraActions = infrastructure.properties.permissions.flatMap(value => value.actions);
  const rbacActions = [...roleDefinition.properties.permissions, ...assignment.permissions].flatMap(value => value.actions);
  assert.ok(infraActions.some(value => value.endsWith('/write')));
  assert.ok(infraActions.every(value => !value.startsWith('Microsoft.Authorization/')));
  assert.ok(infraActions.every(value => !value.startsWith('Microsoft.ManagedIdentity/')));
  assert.deepEqual(assignment.permissions[0].actions.slice(0, 3), ['Microsoft.Resources/deployments/read', 'Microsoft.Resources/deployments/write', 'Microsoft.Resources/deployments/operationStatuses/read']);
  assert.ok(federation.permissions.flatMap(value => value.actions).filter(value => value.endsWith('/write')).every(value => value === 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/write'));
  assert.ok(rbacActions.filter(value => value.endsWith('/write')).every(value => value.startsWith('Microsoft.Authorization/') || value === 'Microsoft.Resources/deployments/write'));
  assert.ok(rbacActions.every(value => !value.startsWith('Microsoft.Network/') && !value.startsWith('Microsoft.App/jobs/write') && !value.startsWith('Microsoft.ContainerRegistry/registries/write')));
  assert.equal(roleDefinition.name, '78b75ed3-4333-4e87-a79c-d39bad7aaab3');
  assert.equal(roleDefinition.properties.roleName, 'AdventuresSuite Migration RBAC Role Definition Deployer');
  assert.deepEqual(roleDefinition.properties.permissions[0], {
    actions: [
      'Microsoft.Resources/deployments/read',
      'Microsoft.Resources/deployments/write',
      'Microsoft.Resources/deployments/validate/action',
      'Microsoft.Resources/deployments/whatIf/action',
      'Microsoft.Resources/deployments/operationStatuses/read',
      'Microsoft.Authorization/roleDefinitions/read',
      'Microsoft.Authorization/roleDefinitions/write',
    ],
    notActions: [], dataActions: [], notDataActions: [],
  });
  assert.deepEqual(roleDefinition.properties.assignableScopes, ['/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev']);
  assert.ok(!roleDefinition.properties.permissions[0].actions.includes('Microsoft.Resources/deployments/operations/read'));
  assert.equal(infrastructure.name, '4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54');
  assert.equal(infrastructure.properties.roleName, 'AdventuresSuite Migration Infrastructure Deployer');
  assert.deepEqual(infrastructure.properties.assignableScopes, ['/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev']);
  validateRoleCatalog(infrastructure);
  const identityReader = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/identity-reader.role.json'));
  assert.equal(identityReader.name, '9df6bf68-4db7-4d38-b7f1-7bb26a541199');
  assert.deepEqual(identityReader.properties.permissions[0].actions, ['Microsoft.ManagedIdentity/userAssignedIdentities/read']);
  validateRoleCatalog(identityReader);
});

test('old combined templates are absent and deployment order is documented', () => {
  assert.throws(() => readFileSync('infrastructure/container-apps-migrations/foundation.bicep'));
  assert.throws(() => readFileSync('infrastructure/container-apps-migrations/job.bicep'));
  const readme = readFileSync('infrastructure/container-apps-migrations/README.md', 'utf8');
  for (const name of ['foundation-resources.bicep', 'identity-access.bicep', 'foundation-access.bicep', 'job-resource.bicep', 'job-access.bicep']) assert.match(readme, new RegExp(name.replace('.', '\\.')));
  const positions = ['foundation-resources.bicep', 'identity-access.bicep', 'foundation-access.bicep', 'job-resource.bicep', 'job-access.bicep'].map(name => readme.indexOf(name));
  assert.ok(positions.every((value, index) => index === 0 || value > positions[index - 1]), 'deployment order must be explicit');
  assert.throws(() => readFileSync('infrastructure/container-apps-migrations/job-access.dev.bicepparam'));
});

test('bootstrap template creates exactly one fixed role definition and no assignment', () => {
  const template = readFileSync('infrastructure/container-apps-migrations/bootstrap-role-definition.bicep', 'utf8');
  assert.match(template, /loadJsonContent\('\.\/roles\/rbac-role-definition-deployer\.role\.json'\)/);
  assert.equal((template.match(/Microsoft\.Authorization\/roleDefinitions@/g) ?? []).length, 1);
  assert.doesNotMatch(template, /roleAssignments|Microsoft\.(Network|App\/|ContainerRegistry|OperationalInsights|ManagedIdentity)/);
});

test('third-party GitHub Actions are pinned to full commit SHAs', () => {
  for (const path of ['.github/workflows/database-migration-job.yml', '.github/workflows/deploy-companion-api-dev.yml', '.github/workflows/deploy-dev.yml', '.github/workflows/provision-migration-foundation-resources.yml', '.github/workflows/provision-migration-rbac-access.yml', '.github/workflows/manage-migration-foundation-rbac.yml', '.github/workflows/publish-companion-testflight.yml', '.github/workflows/validate-migration-container.yml', '.github/workflows/validate-pull-request.yml', '.github/workflows/validate-sql-migrations.yml']) {
    const workflow = readFileSync(path, 'utf8');
    const uses = workflow.split('\n').map(line => line.match(/^\s*uses:\s*([^\s#]+)/)?.[1]).filter(Boolean);
    assert.ok(uses.length > 0);
    for (const action of uses.filter(value => !value.startsWith('./'))) assert.match(action, /@[0-9a-f]{40}$/, `${path}: ${action} is not pinned`);
  }
});

test('deployer workflows preserve identity separation and proof-only mode', () => {
  const foundation = readFileSync('.github/workflows/provision-migration-foundation-resources.yml', 'utf8');
  const rbac = readFileSync('.github/workflows/provision-migration-rbac-access.yml', 'utf8');
  const proofRunner = readFileSync('.github/scripts/run-deployer-federation-proof.sh', 'utf8');
  for (const workflow of [foundation, rbac]) {
    assert.match(workflow, /^on:\n  workflow_dispatch:/m);
    assert.doesNotMatch(workflow, /^\s+(push|pull_request|schedule):/m);
    assert.match(workflow, /^permissions:\n  contents: read\n  id-token: write\n/m);
    assert.match(workflow, /WORKFLOW_REF: \$\{\{ github\.ref \}\}/);
    assert.match(workflow, /WORKFLOW_SHA: \$\{\{ github\.sha \}\}/);
    assert.match(workflow, /test "\$WORKFLOW_REF" = 'refs\/heads\/main'/);
    assert.match(workflow, /test "\$WORKFLOW_SHA" = "\$RELEASE_SHA"/);
    assert.match(workflow, /\[\[ "\$RELEASE_SHA" =~ \^\[0-9a-f\]\{40\}\$ \]\]/);
    assert.doesNotMatch(workflow, /az group show/);
    assert.doesNotMatch(workflow, /az account show/);
    assert.doesNotMatch(workflow, /client-secret|AZURE_CLIENT_SECRET|password|certificate/i);
    assert.match(workflow, /uses: azure\/login@[0-9a-f]{40}/);
    assert.match(workflow, /tenant-id: \$\{\{ vars\.WORKFORCE_TENANT_ID \}\}/);
    assert.doesNotMatch(workflow, /az role (assignment|definition) (create|update|delete)|az (resource|group|identity) (create|update)/i);
    assert.equal((workflow.match(/'eventName': 'migration-deployer-federation-proof'/g) ?? []).length, 1);
    assert.match(workflow, /if: always\(\)/);
    for (const stage of ['input_validation', 'checkout_integrity', 'oidc_authentication', 'arm_token_acquisition', 'arm_token_claim_validation', 'resource_read_probe', 'resource_read_denial_classification', 'deployment_validation_probe', 'deployment_validation_denial_classification', 'complete']) {
      assert.match(workflow, new RegExp(stage));
    }
    assert.doesNotMatch(workflow, /print\(.*ARM_PROOF_TOKEN|print\(.*claims|cat .*\.err|env\s*$|set -x/mi);
  }
  assert.match(proofRunner, /az account get-access-token/);
  assert.match(proofRunner, /az cloud show[\s\S]*?--query endpoints\.activeDirectoryResourceId/);
  assert.doesNotMatch(proofRunner, /management\.core\.windows\.net|graph\.microsoft\.com|storage\.azure\.com/);
  assert.doesNotMatch(proofRunner, /az rest/);
  assert.equal((proofRunner.match(/require-arm-authorization-denial\.sh/g) ?? []).length, 2);
  assert.doesNotMatch(proofRunner, /az account show/);
  assert.match(foundation, /environment: migration-foundation-deployment/);
  assert.match(foundation, /deploy-foundation/);
  assert.match(foundation, /run-foundation-deployment\.sh/);
  assert.doesNotMatch(foundation, /remove-temporary-access|prove-access-removed|cleanup_approval_id/);
  assert.match(foundation, /externalOwnerCleanupRequired/);
  assert.doesNotMatch(readFileSync('.github/scripts/run-foundation-deployment.sh', 'utf8'), /role assignment (create|delete)|role definition (create|delete)|Microsoft\.Authorization/i);
  assert.match(foundation, /vars\.MIGRATION_FOUNDATION_DEPLOYER_CLIENT_ID/);
  assert.doesNotMatch(foundation, /MIGRATION_RBAC_DEPLOYER|822c1c0c|d678e2ad/);
  assert.match(rbac, /environment: migration-rbac-deployment/);
  assert.match(rbac, /vars\.MIGRATION_RBAC_DEPLOYER_CLIENT_ID/);
  assert.doesNotMatch(rbac, /MIGRATION_FOUNDATION_DEPLOYER|b77b6201|223af00d/);
  assert.doesNotMatch(`${foundation}\n${rbac}`, /environment: (database-development|migration-publisher|migration-starter)|DATABASE_IMAGE_PUBLISHER_CLIENT_ID|DATABASE_JOB_STARTER_CLIENT_ID/);
});

test('deployer federation workflows parse semantically and keep runner context out of job env', () => {
  const paths = [
    '.github/workflows/provision-migration-foundation-resources.yml',
    '.github/workflows/provision-migration-rbac-access.yml',
    '.github/workflows/manage-migration-foundation-rbac.yml',
  ];
  const validator = String.raw`
    require 'yaml'
    ARGV.each do |path|
      workflow = YAML.safe_load(File.read(path), aliases: true)
      raise "workflow root must be a mapping" unless workflow.is_a?(Hash)
      jobs = workflow['jobs']
      raise "jobs must be a non-empty mapping" unless jobs.is_a?(Hash) && !jobs.empty?
      jobs.each do |job_name, job|
        raise "job must be a mapping: #{job_name}" unless job.is_a?(Hash)
        raise "job steps must be a non-empty array: #{job_name}" unless job['steps'].is_a?(Array) && !job['steps'].empty?
        environment = job['env'] || {}
        raise "job env must be a mapping: #{job_name}" unless environment.is_a?(Hash)
        environment.each do |name, value|
          raise "runner context is forbidden in job env: #{job_name}.#{name}" if value.to_s.include?('\${{ runner.')
        end
      end
    end
  `;
  const parsed = spawnSync('ruby', ['-e', validator, ...paths], { encoding: 'utf8' });
  assert.equal(parsed.status, 0, parsed.stderr || parsed.stdout);
  for (const path of paths) {
    const workflow = readFileSync(path, 'utf8');
    assert.doesNotMatch(workflow, /^    env:\n(?:^      .*\n)*^      [A-Z0-9_]+:\s*\$\{\{\s*runner\./m);
    assert.doesNotMatch(workflow, /\$\{\{\s*runner\./);
  }
});

test('federation proof source guards reject non-main, source mismatch, and malformed SHAs', () => {
  const allowed = (ref, workflowSha, releaseSha) => ref === 'refs/heads/main' && /^[0-9a-f]{40}$/.test(releaseSha) && workflowSha === releaseSha;
  const sha = 'a'.repeat(40);
  assert.equal(allowed('refs/heads/main', sha, sha), true);
  assert.equal(allowed('refs/heads/feature/test', sha, sha), false);
  assert.equal(allowed('refs/heads/main', 'b'.repeat(40), sha), false);
  assert.equal(allowed('refs/heads/main', sha.toUpperCase(), sha.toUpperCase()), false);
  assert.equal(allowed('refs/heads/main', 'abc', 'abc'), false);
});

test('ARM denial classifier uses bounded structured HTTP evidence and fails closed', () => {
  const directory = mkdtempSync(join(tmpdir(), 'migration-arm-denial-'));
  const fakeBin = join(directory, 'bin');
  const fakeCurl = join(fakeBin, 'curl');
  const authorizationConfig = join(directory, 'authorization.conf');
  const script = '.github/scripts/require-arm-authorization-denial.sh';
  const fake = `#!/usr/bin/env bash
set -euo pipefail
output=''
while [ "$#" -gt 0 ]; do
  case "$1" in
    --output) output="$2"; shift 2 ;;
    --write-out) shift 2 ;;
    *) shift ;;
  esac
done
if [ "\${FAKE_CURL_EXIT-0}" = '63' ]; then
  head -c 65537 /dev/zero >"$output"
  exit 63
fi
printf '%s' "\${FAKE_RESPONSE_BODY-}" >"$output"
printf '%s' "\${FAKE_HTTP_STATUS-403}"
printf '%s' "\${FAKE_TRANSPORT_ERROR-}" >&2
exit "\${FAKE_CURL_EXIT-0}"
`;
  const classify = (status, body, curlExit = '0', transportError = '') => {
    const prefix = join(directory, `evidence-${Math.random().toString(16).slice(2)}`);
    const result = spawnSync('bash', [script, authorizationConfig, 'GET', 'https://management.azure.com/example?api-version=1', '', prefix], {
      encoding: 'utf8',
      env: { ...process.env, PATH: `${fakeBin}:${process.env.PATH}`, FAKE_HTTP_STATUS: status, FAKE_RESPONSE_BODY: body, FAKE_CURL_EXIT: curlExit, FAKE_TRANSPORT_ERROR: transportError },
    });
    return { status: result.status, classification: result.stdout.trim(), stderr: result.stderr };
  };
  try {
    spawnSync('mkdir', ['-p', fakeBin]);
    writeFileSync(fakeCurl, fake);
    chmodSync(fakeCurl, 0o700);
    writeFileSync(authorizationConfig, 'header = "Authorization: Bearer secret-never-print"\n', { mode: 0o600 });
    assert.deepEqual(classify('403', '{"error":{"code":"AuthorizationFailed"}}'), { status: 0, classification: 'authorization_failed', stderr: '' });
    for (const [status, body, expected] of [
      ['200', '{}', 'unexpected_success'], ['302', '{}', 'redirect'], ['401', '{}', 'authentication_failed'],
      ['404', '{}', 'resource_not_found'], ['408', '{}', 'request_timeout'], ['409', '{}', 'conflict'],
      ['429', '{}', 'throttled'], ['500', '{}', 'server_error'], ['503', '{}', 'server_error'],
      ['400', '{}', 'unexpected_http_status'], ['403', 'not-json', 'malformed_json'],
      ['403', '{}', 'missing_error_code'], ['403', '{"error":{}}', 'missing_error_code'],
      ['403', '{"error":{"code":"InvalidAuthenticationToken"}}', 'unexpected_error_code'],
      ['403', 'ERROR: (AuthorizationFailed) Azure CLI human output', 'malformed_json'],
    ]) assert.equal(classify(status, body).classification, expected);
    assert.equal(classify('403', '', '6', 'could not resolve secret host').classification, 'network_failed');
    assert.equal(classify('403', '', '28', 'timeout with secret').classification, 'transport_timeout');
    assert.equal(classify('403', '', '63').classification, 'oversized_response');
    assert.equal(classify('not-status', '{}').classification, 'malformed_or_ambiguous');
    for (const result of [classify('403', 'Bearer abc.def.ghi'), classify('403', '', '7', 'access_token=secret')]) {
      assert.doesNotMatch(`${result.classification}\n${result.stderr}`, /abc|secret|token|Bearer/i);
    }
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('federation proof evidence is stage-bounded, exactly-once, and subscription-context independent', () => {
  const runner = readFileSync('.github/scripts/run-deployer-federation-proof.sh', 'utf8');
  const workflows = [
    readFileSync('.github/workflows/provision-migration-foundation-resources.yml', 'utf8'),
    readFileSync('.github/workflows/provision-migration-rbac-access.yml', 'utf8'),
  ];
  assert.doesNotMatch(runner, /az account show/);
  assert.match(runner, /claims\.get\('tid'\)/);
  assert.match(runner, /trap cleanup EXIT/);
  assert.match(runner, /set \+x/);
  assert.match(runner, /umask 077/);
  assert.match(runner, /"\$authorization_config" "\$request_body"/);
  assert.match(runner, /"\$\{read_prefix\}\.body" "\$\{read_prefix\}\.status" "\$\{read_prefix\}\.transport\.err"/);
  for (const claim of ['tid', 'oid', 'client_id', 'aud']) assert.match(runner, new RegExp(`claim_mismatch_${claim}`));
  for (const workflow of workflows) {
    assert.equal((workflow.match(/print\(json\.dumps\(envelope/g) ?? []).length, 1);
    assert.equal((workflow.match(/migration-deployer-federation-proof/g) ?? []).length, 1);
    for (const failure of ['input_validation_failed', 'checkout_integrity_failed', 'oidc_authentication_failed', 'operation_failed', 'malformed_or_ambiguous']) {
      assert.match(workflow, new RegExp(failure));
    }
    assert.match(workflow, /if not re\.fullmatch/);
    assert.doesNotMatch(workflow, /observed.*claim|raw.*error|request.*header/i);
  }
});

test('federation proof acquires an ARM token for the explicit tenant without subscription context or leakage', () => {
  const directory = mkdtempSync(join(tmpdir(), 'migration-federation-runner-'));
  const fakeBin = join(directory, 'bin');
  const fakeAz = join(fakeBin, 'az');
  const fakeCurl = join(fakeBin, 'curl');
  const stateFile = join(directory, 'proof.state');
  const prefix = join(directory, 'proof');
  const expectedTenant = 'd7add2bb-ac03-49a8-9377-d0bf6a012f2f';
  const expectedPrincipal = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8';
  const expectedClient = '223af00d-69e5-4302-9ac5-6b338f3ea2e5';
  const jwt = claims => {
    const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
    return `${encode({ alg: 'none', typ: 'JWT' })}.${encode(claims)}.signature`;
  };
  const publicCloudAudience = 'https://management.core.windows.net/';
  const issuer = `https://sts.windows.net/${expectedTenant}/`;
  const validToken = jwt({ tid: expectedTenant, oid: expectedPrincipal, appid: expectedClient, iss: issuer, aud: publicCloudAudience });
  const fake = `#!/usr/bin/env bash
set -euo pipefail
if [ "\${1-} \${2-}" = 'cloud show' ]; then
  printf '%s\n' "$FAKE_CLOUD_ARM_AUDIENCE"
  exit 0
fi
if [ "\${1-} \${2-}" = 'account get-access-token' ]; then
  [ "\${FAKE_TOKEN_MODE-}" != 'acquisition_failure' ] || exit 19
  tenant=''
  resource_type=''
  output=''
  while [ "$#" -gt 0 ]; do
    case "$1" in
      --tenant) tenant="$2"; shift 2 ;;
      --resource-type) resource_type="$2"; shift 2 ;;
      --output) output="$2"; shift 2 ;;
      *) shift ;;
    esac
  done
  [ "$tenant" = "\${EXPECTED_FAKE_TENANT-}" ] || exit 20
  [ "$resource_type" = 'arm' ] || exit 21
  [ "$output" = 'json' ] || exit 22
  if [ "\${FAKE_TOKEN_MODE-}" = 'malformed_response' ]; then
    printf '{not-json'
  else
    printf '{"accessToken":"%s"}\n' "$FAKE_ARM_TOKEN"
  fi
  exit 0
fi
if [ "\${1-} \${2-}" = 'account show' ]; then
  exit 88
fi
exit 89
`;
  const curl = `#!/usr/bin/env bash
set -euo pipefail
output=''
while [ "$#" -gt 0 ]; do
  case "$1" in
    --output) output="$2"; shift 2 ;;
    --write-out) shift 2 ;;
    *) shift ;;
  esac
done
if [ "\${FAKE_ARM_SUCCESS-}" = '1' ]; then
  printf '{}' >"$output"
  printf '200'
else
  printf '{"error":{"code":"AuthorizationFailed"}}' >"$output"
  printf '403'
fi
`;
  const run = (overrides = {}, remove = []) => {
    const environment = {
      ...process.env,
      PATH: `${fakeBin}:${process.env.PATH}`,
      APPROVED_TENANT_ID: expectedTenant,
      EXPECTED_FAKE_TENANT: expectedTenant,
      EXPECTED_PRINCIPAL_ID: expectedPrincipal,
      AZURE_CLIENT_ID: expectedClient,
      APPROVED_SUBSCRIPTION_ID: '5ace9cdd-06d1-47d9-8214-1e7c756d076a',
      TARGET_RESOURCE_GROUP: 'rg-adventures-suite-dev',
      FAKE_CLOUD_ARM_AUDIENCE: publicCloudAudience,
      FAKE_ARM_TOKEN: validToken,
      ...overrides,
    };
    for (const name of remove) delete environment[name];
    writeFileSync(stateFile, 'stage=oidc_authentication\n');
    const result = spawnSync('bash', ['.github/scripts/run-deployer-federation-proof.sh', 'migration-foundation-deployment', prefix, stateFile], { encoding: 'utf8', env: environment });
    const state = Object.fromEntries(readFileSync(stateFile, 'utf8').trim().split('\n').map(line => line.split('=', 2)));
    return { result, state };
  };
  try {
    spawnSync('mkdir', ['-p', fakeBin]);
    writeFileSync(fakeAz, fake);
    writeFileSync(fakeCurl, curl);
    chmodSync(fakeAz, 0o700);
    chmodSync(fakeCurl, 0o700);

    const success = run();
    assert.equal(success.result.status, 0);
    assert.deepEqual(success.state, { stage: 'complete', classification: 'complete', exit_code: '0' });
    assert.equal(success.result.stdout, '');
    assert.doesNotMatch(success.result.stderr, new RegExp(validToken.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));

    const missingTenant = run({}, ['APPROVED_TENANT_ID']);
    assert.notEqual(missingTenant.result.status, 0);
    assert.equal(missingTenant.state.stage, 'arm_token_acquisition');
    assert.equal(missingTenant.state.classification, 'operation_failed');

    const incorrectTenant = run({ APPROVED_TENANT_ID: '00000000-0000-0000-0000-000000000000' });
    assert.notEqual(incorrectTenant.result.status, 0);
    assert.equal(incorrectTenant.state.stage, 'arm_token_acquisition');

    const acquisitionFailure = run({ FAKE_TOKEN_MODE: 'acquisition_failure' });
    assert.notEqual(acquisitionFailure.result.status, 0);
    assert.deepEqual({ stage: acquisitionFailure.state.stage, classification: acquisitionFailure.state.classification }, { stage: 'arm_token_acquisition', classification: 'operation_failed' });

    const malformed = run({ FAKE_TOKEN_MODE: 'malformed_response' });
    assert.notEqual(malformed.result.status, 0);
    assert.deepEqual({ stage: malformed.state.stage, classification: malformed.state.classification }, { stage: 'arm_token_claim_validation', classification: 'malformed_token' });

    const trailingSlash = run({ FAKE_CLOUD_ARM_AUDIENCE: publicCloudAudience.slice(0, -1) });
    assert.equal(trailingSlash.result.status, 0);
    const tokenWithoutTrailingSlash = run({ FAKE_ARM_TOKEN: jwt({ tid: expectedTenant, oid: expectedPrincipal, appid: expectedClient, iss: issuer, aud: publicCloudAudience.slice(0, -1) }) });
    assert.equal(tokenWithoutTrailingSlash.result.status, 0);

    const wrongTenant = run({ FAKE_ARM_TOKEN: jwt({ tid: 'wrong', oid: expectedPrincipal, appid: expectedClient, iss: issuer, aud: publicCloudAudience }) });
    assert.equal(wrongTenant.state.classification, 'claim_mismatch_tid');
    const wrongObject = run({ FAKE_ARM_TOKEN: jwt({ tid: expectedTenant, oid: 'wrong', appid: expectedClient, iss: issuer, aud: publicCloudAudience }) });
    assert.equal(wrongObject.state.classification, 'claim_mismatch_oid');
    const wrongClient = run({ FAKE_ARM_TOKEN: jwt({ tid: expectedTenant, oid: expectedPrincipal, appid: 'wrong', iss: issuer, aud: publicCloudAudience }) });
    assert.equal(wrongClient.state.classification, 'claim_mismatch_client_id');

    const rejectedAudiences = [
      'https://graph.microsoft.com/',
      'https://storage.azure.com/',
      'api://custom-api',
      '',
      ['https://management.core.windows.net/'],
      ['https://management.core.windows.net/', 'https://graph.microsoft.com/'],
      42,
      'https://management.core.windows.net//',
      'https://attacker.example/https://management.core.windows.net/',
    ];
    const audienceFailures = rejectedAudiences.map(aud => run({ FAKE_ARM_TOKEN: jwt({ tid: expectedTenant, oid: expectedPrincipal, appid: expectedClient, iss: issuer, aud }) }));
    for (const failure of audienceFailures) assert.equal(failure.state.classification, 'claim_mismatch_aud');
    const cloudDisagreement = run({ FAKE_CLOUD_ARM_AUDIENCE: 'https://management.azure.example/', FAKE_ARM_TOKEN: validToken });
    assert.equal(cloudDisagreement.state.classification, 'claim_mismatch_aud');

    const unexpectedSuccess = run({ FAKE_ARM_SUCCESS: '1' });
    assert.notEqual(unexpectedSuccess.result.status, 0);
    assert.deepEqual({ stage: unexpectedSuccess.state.stage, classification: unexpectedSuccess.state.classification }, { stage: 'resource_read_denial_classification', classification: 'unexpected_success' });
    for (const value of [success, trailingSlash, tokenWithoutTrailingSlash, missingTenant, incorrectTenant, acquisitionFailure, malformed, wrongTenant, wrongObject, wrongClient, ...audienceFailures, cloudDisagreement, unexpectedSuccess]) {
      assert.doesNotMatch(`${value.result.stdout}\n${value.result.stderr}`, /accessToken|Bearer|eyJ|signature/);
      for (const suffix of [
        '-token.json', '-token.err', '-audience.txt', '-authorization.conf', '-request.json',
        '-read.body', '-read.status', '-read.transport.err', '-write.body', '-write.status', '-write.transport.err',
      ]) assert.throws(() => readFileSync(`${prefix}${suffix}`));
    }
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('federation proof reporter emits exactly one sanitized envelope for every stage', () => {
  const workflow = readFileSync('.github/workflows/provision-migration-foundation-resources.yml', 'utf8');
  const start = workflow.indexOf("          python3 - <<'PY'\n") + "          python3 - <<'PY'\n".length;
  const end = workflow.indexOf('\n          PY', start);
  assert.ok(start > 0 && end > start);
  const reporter = workflow.slice(start, end).split('\n').map(line => line.replace(/^          /, '')).join('\n');
  const directory = mkdtempSync(join(tmpdir(), 'migration-federation-envelope-'));
  const stateFile = join(directory, 'proof.state');
  const baseEnvironment = {
    ...process.env,
    RELEASE_SHA: 'a'.repeat(40),
    APPROVAL_ID: 'approval-0001',
    PROOF_ENVIRONMENT: 'migration-foundation-deployment',
    PROOF_STATE_FILE: stateFile,
    INPUT_OUTCOME: 'success',
    CHECKOUT_OUTCOME: 'success',
    INTEGRITY_OUTCOME: 'success',
    OIDC_OUTCOME: 'success',
    PROOF_OUTCOME: 'success',
    OPERATION: 'proof',
    DEPLOYMENT_IDENTITY_OUTCOME: '',
    DEPLOYMENT_OUTCOME: '',
  };
  const report = (overrides = {}, state) => {
    if (state) writeFileSync(stateFile, `stage=${state.stage}\nclassification=${state.classification}\nexit_code=${state.exitCode}\n`);
    else writeFileSync(stateFile, '');
    const result = spawnSync('python3', ['-c', reporter], { encoding: 'utf8', env: { ...baseEnvironment, ...overrides } });
    const lines = result.stdout.trim().split('\n').filter(Boolean);
    assert.equal(lines.length, 1);
    return { result, envelope: JSON.parse(lines[0]) };
  };
  try {
    assert.deepEqual(report().envelope, {
      eventName: 'migration-deployer-federation-proof',
      approvalId: 'approval-0001',
      releaseSha: 'a'.repeat(40),
      environment: 'migration-foundation-deployment',
      stage: 'complete',
      classification: 'complete',
      exitCode: 0,
      externalOwnerCleanupRequired: false,
    });
    assert.equal(report({ INPUT_OUTCOME: 'failure' }).envelope.stage, 'input_validation');
    assert.equal(report({ CHECKOUT_OUTCOME: 'failure' }).envelope.stage, 'checkout_integrity');
    assert.equal(report({ INTEGRITY_OUTCOME: 'failure' }).envelope.stage, 'checkout_integrity');
    assert.equal(report({ OIDC_OUTCOME: 'failure' }).envelope.stage, 'oidc_authentication');
    for (const stage of ['arm_token_acquisition', 'arm_token_claim_validation', 'resource_read_probe', 'resource_read_denial_classification', 'deployment_validation_probe', 'deployment_validation_denial_classification']) {
      const classification = stage.endsWith('denial_classification') ? 'subscription_resolution_failed' : 'operation_failed';
      const value = report({ PROOF_OUTCOME: 'failure' }, { stage, classification, exitCode: 1 });
      assert.equal(value.envelope.stage, stage);
      assert.equal(value.envelope.classification, classification);
      assert.equal(value.result.status, 1);
    }
    const redacted = report({ INPUT_OUTCOME: 'failure', APPROVAL_ID: 'token=value', RELEASE_SHA: 'Bearer abc.def.ghi' });
    assert.equal(redacted.envelope.approvalId, '<invalid>');
    assert.equal(redacted.envelope.releaseSha, '<invalid>');
    assert.doesNotMatch(redacted.result.stdout, /token=value|Bearer|abc\.def/i);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('foundation what-if accepts only the four exact reviewed resources', () => {
  const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';
  const changes = [
    ['Microsoft.Network/virtualNetworks/subnets', `${scope}/providers/Microsoft.Network/virtualNetworks/vnet-adventures-suite-dev/subnets/snet-container-apps-migrations`],
    ['Microsoft.OperationalInsights/workspaces', `${scope}/providers/Microsoft.OperationalInsights/workspaces/log-adventures-suite-migrations-dev`],
    ['Microsoft.ContainerRegistry/registries', `${scope}/providers/Microsoft.ContainerRegistry/registries/advsuitemigrationsdev`],
    ['Microsoft.App/managedEnvironments', `${scope}/providers/Microsoft.App/managedEnvironments/cae-adventures-suite-migrations-dev`],
  ].map(([resourceType, resourceId]) => ({ resourceType, resourceId, changeType: 'Create', after: { properties: { publicNetworkAccess: 'Disabled' } } }));
  const document = { changes };
  assert.deepEqual(validateWhatIf(document), { classification: 'what_if_approved', resourceCount: 4 });
  for (const changeType of ['Delete', 'Deploy', 'Modify', 'NoChange', 'Unsupported']) {
    const drift = structuredClone(document); drift.changes[0].changeType = changeType;
    assert.throws(() => validateWhatIf(drift), /unexpected_what_if_operation/);
  }
  const outside = structuredClone(document); outside.changes[0].resourceId = `${scope}/providers/Microsoft.Storage/storageAccounts/unreviewed`;
  assert.throws(() => validateWhatIf(outside), /unexpected_what_if_resource/);
  const role = structuredClone(document); role.changes[0].after = { type: 'Microsoft.Authorization/roleAssignments' };
  assert.throws(() => validateWhatIf(role), /forbidden_what_if_content/);
  const identity = structuredClone(document); identity.changes[0].after = { operation: 'Microsoft.ManagedIdentity/userAssignedIdentities/write' };
  assert.throws(() => validateWhatIf(identity), /forbidden_what_if_content/);
  const publicAccess = structuredClone(document); publicAccess.changes[2].after.properties.publicNetworkAccess = 'Enabled';
  assert.throws(() => validateWhatIf(publicAccess), /forbidden_what_if_content/);
  const externalEnvironment = structuredClone(document); externalEnvironment.changes[3].after = { properties: { vnetConfiguration: { internal: false } } };
  assert.throws(() => validateWhatIf(externalEnvironment), /forbidden_what_if_content/);
});

test('foundation deployment result requires terminal success and exact sanitized outputs', () => {
  const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';
  const value = output => ({ type: 'String', value: output });
  const outputs = {
    registryResourceId: value(`${scope}/providers/Microsoft.ContainerRegistry/registries/advsuitemigrationsdev`),
    registryLoginServer: value('advsuitemigrationsdev.azurecr.io'),
    logWorkspaceResourceId: value(`${scope}/providers/Microsoft.OperationalInsights/workspaces/log-adventures-suite-migrations-dev`),
    environmentResourceId: value(`${scope}/providers/Microsoft.App/managedEnvironments/cae-adventures-suite-migrations-dev`),
    migrationIdentityResourceId: value(`${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-job-dev`),
    migrationIdentityPrincipalId: value('ffc9a4bd-67c4-44af-82dc-b7f663f8bea5'),
    migrationIdentityClientId: value('d0da8236-91dc-4454-8a3d-19d08a406e5d'),
    pullIdentityResourceId: value(`${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-pull-dev`),
    publisherIdentityResourceId: value(`${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-publisher-dev`),
    starterIdentityResourceId: value(`${scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-starter-dev`),
  };
  const document = { properties: { provisioningState: 'Succeeded', outputs } };
  const trusted = JSON.parse(readFileSync('infrastructure/container-apps-migrations/foundation-identity-catalog.dev.json', 'utf8'));
  assert.equal(validateDeployment(document, trusted).classification, 'deployment_complete');
  const failed = structuredClone(document); failed.properties.provisioningState = 'Failed';
  assert.throws(() => validateDeployment(failed, trusted), /deployment_not_succeeded/);
  const missing = structuredClone(document); delete missing.properties.outputs.registryResourceId;
  assert.throws(() => validateDeployment(missing, trusted), /unexpected_deployment_outputs/);
  const leaked = structuredClone(document); leaked.properties.outputs.secret = value('credential');
  assert.throws(() => validateDeployment(leaked, trusted), /unexpected_deployment_outputs/);
});

test('deterministic role definitions reject broad scopes, wildcards, RBAC, identity mutation, and broad substitutions', () => {
  const infrastructure = JSON.parse(readFileSync('infrastructure/container-apps-migrations/roles/infrastructure-deployer.role.json'));
  for (const mutate of [
    role => { role.properties.assignableScopes = ['/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a']; },
    role => { role.properties.permissions[0].actions.push('Microsoft.Resources/*'); },
    role => { role.properties.permissions[0].actions.push('Microsoft.Authorization/roleAssignments/write'); },
    role => { role.properties.permissions[0].actions.push('Microsoft.ManagedIdentity/userAssignedIdentities/write'); },
  ]) {
    const drift = structuredClone(infrastructure); mutate(drift);
    assert.throws(() => validateRoleCatalog(drift));
  }
  const roleTemplate = readFileSync('infrastructure/container-apps-migrations/deployer-role-definitions.bicep', 'utf8');
  assert.match(roleTemplate, /loadJsonContent/);
  assert.match(roleTemplate, /4bfa5b8d|infrastructureRole\.name/);
  assert.doesNotMatch(roleTemplate, /Owner|Contributor|\*/);
});

test('RBAC boundary what-if and workflows prevent substitution, self-management, and residual access', () => {
  const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';
  const roleChanges = ['4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54', '9df6bf68-4db7-4d38-b7f1-7bb26a541199'].map(id => ({ resourceId: `${scope}/providers/Microsoft.Authorization/roleDefinitions/${id}`, changeType: 'Create' }));
  const ignoredResources = [
    { resourceId: `${scope}/providers/Microsoft.Network/virtualNetworks/vnet-adventures-suite-dev`, changeType: 'Ignore' },
    { resourceId: `${scope}/providers/Microsoft.Sql/servers/adventures-suite-dev-sql`, changeType: 'Ignore' },
  ];
  const liveShapedChanges = [...roleChanges, ...ignoredResources];
  assert.equal(validateRbacWhatIf('bootstrap', { properties: { changes: liveShapedChanges } }).resourceCount, 2);
  const broad = structuredClone(roleChanges); broad[0].after = { roleName: 'Owner' };
  assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: broad } }), /broad_role_substitution/);
  for (const targetIndex of [0, 1]) {
    const noChange = structuredClone(liveShapedChanges); noChange[targetIndex].changeType = 'NoChange';
    assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: noChange } }), /unexpected_rbac_operation/);
  }
  for (const changeType of ['Modify', 'Delete', 'Deploy']) {
    const unexpectedOperation = structuredClone(liveShapedChanges); unexpectedOperation[0].changeType = changeType;
    assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: unexpectedOperation } }), /unexpected_rbac_operation/);
  }
  const replacement = structuredClone(liveShapedChanges); replacement[0].changeType = 'Modify'; replacement[0].delta = [{ path: 'properties', propertyChangeType: 'Modify' }];
  assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: replacement } }), /unexpected_rbac_operation/);
  const duplicate = structuredClone(liveShapedChanges); duplicate.splice(1, 0, structuredClone(duplicate[0]));
  assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: duplicate } }), /unexpected_rbac_resource/);
  const missing = structuredClone(liveShapedChanges); missing.splice(1, 1);
  assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: missing } }), /missing_rbac_resource/);
  const additional = structuredClone(liveShapedChanges); additional.push({ resourceId: `${scope}/providers/Microsoft.Authorization/roleDefinitions/00000000-0000-0000-0000-000000000000`, changeType: 'Create' });
  assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: additional } }), /unexpected_rbac_resource/);
  const malformedIgnore = structuredClone(liveShapedChanges); malformedIgnore.push({ resourceId: 'not-an-arm-resource-id', changeType: 'Ignore' });
  assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: malformedIgnore } }), /malformed_what_if/);
  const lowercaseIgnore = structuredClone(liveShapedChanges); lowercaseIgnore[2].changeType = 'ignore';
  assert.throws(() => validateRbacWhatIf('bootstrap', { properties: { changes: lowercaseIgnore } }), /unexpected_rbac_resource/);
  const foundationWorkflow = readFileSync('.github/workflows/provision-migration-foundation-resources.yml', 'utf8');
  const rbacWorkflow = readFileSync('.github/workflows/manage-migration-foundation-rbac.yml', 'utf8');
  const rbacRunner = readFileSync('.github/scripts/run-rbac-boundary-operation.sh', 'utf8');
  assert.doesNotMatch(foundationWorkflow, /role assignment (create|delete)|role definition (create|delete)|Microsoft\.Authorization/i);
  assert.doesNotMatch(rbacWorkflow, /MIGRATION_FOUNDATION_DEPLOYER_CLIENT_ID|223af00d-69e5-4302-9ac5-6b338f3ea2e5/);
  assert.match(rbacRunner, /5c14d19b-04c7-4dfa-83ed-9447d0ea3c33/);
  assert.match(rbacRunner, /fa329695-3907-4852-94f5-fda8a26a4698/);
  assert.match(rbacRunner, /assignment_inspection[\s\S]*for assignment_id in \$deletion_plan[\s\S]*role assignment delete/);
  assert.match(rbacRunner, /residue_verification/);
  assert.match(rbacRunner, /azure_error_limit=65536/);
  assert.match(rbacRunner, /classify-azure-error\.py/);
  assert.match(rbacRunner, /stage=%s\\nclassification=%s\\nazure_error_code=%s\\nassignment_timestamp_utc=%s\\nauthority_deadline_utc=%s\\nexit_code=%s/);
  assert.match(rbacRunner, /rm -f "\$error_file"/);
  assert.match(rbacWorkflow, /azureErrorCode/);
  assert.match(rbacWorkflow, /azure_error_unclassified/);
  assert.match(rbacWorkflow, /allowed_error_codes = \{'AuthorizationFailed', 'InvalidTemplate', 'InvalidTemplateDeployment', 'DeploymentFailed'\}/);
  assert.doesNotMatch(`${foundationWorkflow}\n${rbacWorkflow}\n${rbacRunner}`, /\bOwner\b|\bContributor\b|AZURE_CLIENT_SECRET|client-secret|set -x/i);
});

function classifyAzureError(evidence) {
  const directory = mkdtempSync(join(tmpdir(), 'azure-error-policy-'));
  const evidencePath = join(directory, 'stderr.txt');
  try {
    writeFileSync(evidencePath, evidence);
    const result = spawnSync('python3', ['.github/scripts/classify-azure-error.py', evidencePath], { encoding: 'utf8' });
    assert.equal(result.status, 0, result.stderr);
    const [classification, azureErrorCode = ''] = result.stdout.replace(/\n$/, '').split('\n');
    return { classification, azureErrorCode, output: result.stdout };
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
}

test('RBAC Azure error evidence classifies one authorization failure without leaking its message', () => {
  const result = classifyAzureError('ERROR: (AuthorizationFailed) token=must-not-escape\nCode: AuthorizationFailed\nMessage: sensitive request details\n');
  assert.deepEqual({ classification: result.classification, azureErrorCode: result.azureErrorCode }, { classification: 'azure_authorization_failed', azureErrorCode: 'AuthorizationFailed' });
  assert.doesNotMatch(result.output, /token|request|sensitive|Message/);
});

test('RBAC Azure error evidence classifies one template validation failure', () => {
  assert.deepEqual(classifyAzureError('ERROR: (InvalidTemplateDeployment) omitted\nCode: InvalidTemplateDeployment\n'), {
    classification: 'azure_template_validation_failed', azureErrorCode: 'InvalidTemplateDeployment', output: 'azure_template_validation_failed\nInvalidTemplateDeployment\n',
  });
});

test('Azure error evidence classifies one deployment failure', () => {
  assert.deepEqual(classifyAzureError('ERROR: (DeploymentFailed) omitted\nCode: DeploymentFailed\n'), {
    classification: 'azure_deployment_failed', azureErrorCode: 'DeploymentFailed', output: 'azure_deployment_failed\nDeploymentFailed\n',
  });
});

test('RBAC Azure error evidence rejects malformed codes', () => {
  assert.deepEqual(classifyAzureError('Code: Authorization Failed\n').classification, 'azure_error_unclassified');
});

test('RBAC Azure error evidence rejects ambiguous codes', () => {
  assert.deepEqual(classifyAzureError('Code: AuthorizationFailed\nCode: InvalidTemplateDeployment\n').classification, 'azure_error_unclassified');
});

test('RBAC Azure error evidence rejects oversized buffers', () => {
  assert.deepEqual(classifyAzureError(Buffer.alloc((64 * 1024) + 1, 65)).classification, 'azure_error_unclassified');
});

test('RBAC Azure error evidence rejects unrecognized codes', () => {
  const result = classifyAzureError('Code: SubscriptionNotFound\n');
  assert.deepEqual({ classification: result.classification, azureErrorCode: result.azureErrorCode }, { classification: 'azure_error_unclassified', azureErrorCode: '' });
});

test('RBAC boundary accepts the live-shaped root what-if envelope and one legacy envelope only', () => {
  const live = JSON.parse(readFileSync('.github/scripts/fixtures/rbac-bootstrap-what-if-root.json', 'utf8'));
  assert.deepEqual(validateRbacWhatIf('bootstrap', live), { classification: 'bootstrap_what_if_approved', resourceCount: 2 });
  assert.deepEqual(validateRbacWhatIf('bootstrap', { properties: { changes: live.changes } }), { classification: 'bootstrap_what_if_approved', resourceCount: 2 });
  for (const malformed of [
    {},
    { changes: null },
    { properties: { changes: {} } },
    { changes: live.changes, properties: { changes: live.changes } },
    { changes: live.changes, properties: { changes: [] } },
  ]) {
    assert.throws(() => validateRbacWhatIf('bootstrap', malformed), /malformed_what_if/);
  }
});

test('RBAC boundary ignores benign metadata outside actionable structured RBAC fields', () => {
  const live = JSON.parse(readFileSync('.github/scripts/fixtures/rbac-bootstrap-what-if-ownership.json', 'utf8'));
  assert.deepEqual(validateRbacWhatIf('bootstrap', live), { classification: 'bootstrap_what_if_approved', resourceCount: 2 });
  live.changes[2].after.properties = { roleName: 'Owner' };
  assert.deepEqual(validateRbacWhatIf('bootstrap', live), { classification: 'bootstrap_what_if_approved', resourceCount: 2 });
  live.changes[0].after.tags = { roleName: 'Owner', ownership: 'adventures-suite' };
  assert.deepEqual(validateRbacWhatIf('bootstrap', live), { classification: 'bootstrap_what_if_approved', resourceCount: 2 });
});

test('RBAC boundary rejects structured Owner and Contributor substitutions on actionable targets', () => {
  const scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev';
  const ownerId = '8e3af657-a8ff-443c-a75c-2fe8c4bcb635';
  const contributorId = 'b24988ac-6180-42a0-ab88-20f7382dd24c';
  const bootstrap = JSON.parse(readFileSync('.github/scripts/fixtures/rbac-bootstrap-what-if-ownership.json', 'utf8'));
  const assignments = {
    changes: ['5c14d19b-04c7-4dfa-83ed-9447d0ea3c33', 'fa329695-3907-4852-94f5-fda8a26a4698'].map(id => ({
      resourceId: `${scope}/providers/Microsoft.Authorization/roleAssignments/${id}`,
      changeType: 'Create',
      after: { properties: { roleDefinitionId: `${scope}/providers/Microsoft.Authorization/roleDefinitions/4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54` } },
    })),
  };
  const cases = [
    ['bootstrap', bootstrap, { roleName: 'OWNER' }],
    ['bootstrap', bootstrap, { properties: { roleName: 'contributor' } }],
    ['bootstrap', bootstrap, { roleDefinitionName: 'Owner' }],
    ['bootstrap', bootstrap, { properties: { roleDefinitionId: ownerId } }],
    ['bootstrap', bootstrap, { name: contributorId }],
    ['bootstrap', bootstrap, { id: `${scope}/providers/Microsoft.Authorization/roleDefinitions/${ownerId}` }],
    ['assignment', assignments, { roleName: 'Contributor' }],
    ['assignment', assignments, { roleDefinitionName: 'owner' }],
    ['assignment', assignments, { roleDefinitionId: contributorId }],
    ['assignment', assignments, { properties: { roleDefinitionId: `/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/providers/Microsoft.Authorization/roleDefinitions/${ownerId}` } }],
  ];
  for (const [mode, source, after] of cases) {
    const document = structuredClone(source);
    document.changes[0].after = after;
    assert.throws(() => validateRbacWhatIf(mode, document), /broad_role_substitution/);
  }
});

test('RBAC boundary requires complete ignored ARM resource IDs under the exact approved scope', () => {
  const live = JSON.parse(readFileSync('.github/scripts/fixtures/rbac-bootstrap-what-if-root.json', 'utf8'));
  const validIgnoredIds = [
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/adventures-suite-dev-sql',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/RG-ADVENTURES-SUITE-DEV/providers/Microsoft.Network/privateDnsZones/privatelink.database.windows.net/virtualNetworkLinks/link-adventures-suite-dev-sql',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/microsoft.insights/actiongroups/Application Insights Smart Detection',
  ];
  for (const resourceId of validIgnoredIds) {
    const document = structuredClone(live);
    document.changes[2].resourceId = resourceId;
    assert.equal(validateRbacWhatIf('bootstrap', document).resourceCount, 2);
  }
  const invalidIgnoredIds = [
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/name/extraType',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/invalid namespace/servers/name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft./servers/name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providersX/Microsoft.Sql/servers/name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/invalid type/name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql//name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/..',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/%2e%2e',
    '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-prod/providers/Microsoft.Sql/servers/name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev-lookalike/providers/Microsoft.Sql/servers/name',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/name?api-version=1',
    '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/name#fragment',
  ];
  for (const resourceId of invalidIgnoredIds) {
    const document = structuredClone(live);
    document.changes[2].resourceId = resourceId;
    assert.throws(() => validateRbacWhatIf('bootstrap', document), /malformed_what_if/);
  }
  const wrongCase = structuredClone(live);
  wrongCase.changes[2].changeType = 'ignore';
  assert.throws(() => validateRbacWhatIf('bootstrap', wrongCase), /unexpected_rbac_resource/);
});

test('foundation deployment mode is exact-approval bound and never emits raw evidence', () => {
  const workflow = readFileSync('.github/workflows/provision-migration-foundation-resources.yml', 'utf8');
  const runner = readFileSync('.github/scripts/run-foundation-deployment.sh', 'utf8');
  assert.match(workflow, /operation:[\s\S]*deploy-foundation/);
  assert.match(workflow, /foundation-deploy-\*/);
  assert.match(workflow, /TEMPLATE_SHA256/);
  assert.match(workflow, /PARAMETERS_SHA256/);
  assert.match(runner, /sha256sum "\$template"/);
  assert.match(runner, /sha256sum "\$parameters"/);
  assert.match(runner, /deployment group validate/);
  assert.match(runner, /deployment group what-if/);
  assert.match(runner, /deployment group create/);
  assert.match(runner, /deployment group show/);
  assert.match(runner, /trap cleanup EXIT/);
  assert.match(runner, /azure_error_limit=65536/);
  assert.match(runner, /classify-azure-error\.py/);
  assert.match(runner, /foundation-authority-window\.mjs active/);
  assert.match(runner, /foundation-identity-catalog\.dev\.json/);
  assert.doesNotMatch(`${workflow}\n${runner}`, /cat .*\.json|cat .*\.err|set -x|--debug|Bearer|accessToken|client-secret/i);
});

test('foundation access and cleanup are Owner-operated and proof remains separately dispatchable', () => {
  const foundation = readFileSync('.github/workflows/provision-migration-foundation-resources.yml', 'utf8');
  const rbac = readFileSync('.github/workflows/manage-migration-foundation-rbac.yml', 'utf8');
  const documentation = `${readFileSync('docs/development/container-apps-migration-permissions.md', 'utf8')}\n${readFileSync('infrastructure/container-apps-migrations/README.md', 'utf8')}`;
  assert.doesNotMatch(`${foundation}\n${rbac}`, /az role assignment (create|delete)|assign-foundation-access|remove-foundation-access/);
  assert.doesNotMatch(foundation, /remove-temporary-access|prove-access-removed|cleanup_approval_id|migration-foundation-access-cleanup|migration-foundation-loss-of-access/);
  assert.match(foundation, /options:[\s\S]*- proof[\s\S]*- deploy-foundation/);
  assert.match(foundation, /run-deployer-federation-proof\.sh/);
  assert.match(foundation, /externalOwnerCleanupRequired/);
  assert.doesNotMatch(foundation, /\bcleanupRequired\b|cleanup completed|cleanupComplete/i);
  assert.doesNotMatch(rbac, /assign-foundation-access|remove-foundation-access|role assignment (create|delete)/);
  assert.match(documentation, /fd462691-dc24-4127-afd9-e15321dc9050/);
  assert.match(documentation, /5c14d19b-04c7-4dfa-83ed-9447d0ea3c33/);
  assert.match(documentation, /fa329695-3907-4852-94f5-fda8a26a4698/);
  assert.match(documentation, /conclusively absent\s+assignment is already clean/);
  assert.match(documentation, /complete direct-and-inherited post-readback/);
  assert.match(documentation, /separately dispatched/);
});

test('foundation deployment approval rejects missing approval and changed SHA or artifacts', () => {
  const valid = {
    ref: 'refs/heads/main', releaseSha: 'a'.repeat(40), workflowSha: 'a'.repeat(40),
    approvalId: 'foundation-deploy-operation-0001', templateSha: 'b'.repeat(64), actualTemplateSha: 'b'.repeat(64),
    parametersSha: 'c'.repeat(64), actualParametersSha: 'c'.repeat(64),
    subscriptionId: '5ace9cdd-06d1-47d9-8214-1e7c756d076a', resourceGroup: 'rg-adventures-suite-dev',
    clientId: '223af00d-69e5-4302-9ac5-6b338f3ea2e5', principalId: 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8',
  };
  assert.equal(validateApproval(valid), true);
  for (const mutation of [
    value => { value.approvalId = ''; }, value => { value.ref = 'refs/heads/feature'; },
    value => { value.workflowSha = 'd'.repeat(40); }, value => { value.actualTemplateSha = 'd'.repeat(64); },
    value => { value.actualParametersSha = 'd'.repeat(64); }, value => { value.clientId = 'wrong'; },
  ]) {
    const drift = structuredClone(valid); mutation(drift);
    assert.throws(() => validateApproval(drift));
  }
});

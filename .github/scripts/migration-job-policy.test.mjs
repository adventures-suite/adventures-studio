import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
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
  const infraActions = infrastructure.permissions.flatMap(value => value.actions);
  const rbacActions = [...roleDefinition.permissions, ...assignment.permissions].flatMap(value => value.actions);
  assert.ok(infraActions.some(value => value.endsWith('/write')));
  assert.ok(infraActions.every(value => !value.startsWith('Microsoft.Authorization/')));
  assert.ok(infraActions.every(value => !value.startsWith('Microsoft.ManagedIdentity/')));
  assert.deepEqual(assignment.permissions[0].actions.slice(0, 3), ['Microsoft.Resources/deployments/read', 'Microsoft.Resources/deployments/write', 'Microsoft.Resources/deployments/operationStatuses/read']);
  assert.ok(federation.permissions.flatMap(value => value.actions).filter(value => value.endsWith('/write')).every(value => value === 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/write'));
  assert.ok(rbacActions.filter(value => value.endsWith('/write')).every(value => value.startsWith('Microsoft.Authorization/') || value === 'Microsoft.Resources/deployments/write'));
  assert.ok(rbacActions.every(value => !value.startsWith('Microsoft.Network/') && !value.startsWith('Microsoft.App/jobs/write') && !value.startsWith('Microsoft.ContainerRegistry/registries/write')));
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

test('third-party GitHub Actions are pinned to full commit SHAs', () => {
  for (const path of ['.github/workflows/database-migration-job.yml', '.github/workflows/deploy-companion-api-dev.yml', '.github/workflows/deploy-dev.yml', '.github/workflows/provision-migration-foundation-resources.yml', '.github/workflows/provision-migration-rbac-access.yml', '.github/workflows/publish-companion-testflight.yml', '.github/workflows/validate-migration-container.yml', '.github/workflows/validate-pull-request.yml', '.github/workflows/validate-sql-migrations.yml']) {
    const workflow = readFileSync(path, 'utf8');
    const uses = workflow.split('\n').map(line => line.match(/^\s*uses:\s*([^\s#]+)/)?.[1]).filter(Boolean);
    assert.ok(uses.length > 0);
    for (const action of uses.filter(value => !value.startsWith('./'))) assert.match(action, /@[0-9a-f]{40}$/, `${path}: ${action} is not pinned`);
  }
});

test('deployer federation workflows are isolated proof-only controls', () => {
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
    assert.doesNotMatch(workflow, /az deployment (group|sub|mg|tenant) create|az role (assignment|definition) (create|update)|az (resource|group|identity) (create|update)|bicep/i);
    assert.equal((workflow.match(/'eventName': 'migration-deployer-federation-proof'/g) ?? []).length, 1);
    assert.match(workflow, /if: always\(\)/);
    for (const stage of ['input_validation', 'checkout_integrity', 'oidc_authentication', 'arm_token_acquisition', 'arm_token_claim_validation', 'resource_read_probe', 'resource_read_denial_classification', 'deployment_validation_probe', 'deployment_validation_denial_classification', 'complete']) {
      assert.match(workflow, new RegExp(stage));
    }
    assert.doesNotMatch(workflow, /print\(.*ARM_PROOF_TOKEN|print\(.*claims|cat .*\.err|env\s*$|set -x/mi);
  }
  assert.match(proofRunner, /az account get-access-token/);
  assert.match(proofRunner, /az rest --method get/);
  assert.match(proofRunner, /require-arm-authorization-denial\.sh/);
  assert.doesNotMatch(proofRunner, /az account show/);
  assert.match(foundation, /environment: migration-foundation-deployment/);
  assert.match(foundation, /vars\.MIGRATION_FOUNDATION_DEPLOYER_CLIENT_ID/);
  assert.doesNotMatch(foundation, /MIGRATION_RBAC_DEPLOYER|822c1c0c|d678e2ad/);
  assert.match(rbac, /environment: migration-rbac-deployment/);
  assert.match(rbac, /vars\.MIGRATION_RBAC_DEPLOYER_CLIENT_ID/);
  assert.doesNotMatch(rbac, /MIGRATION_FOUNDATION_DEPLOYER|b77b6201|223af00d/);
  assert.doesNotMatch(`${foundation}\n${rbac}`, /environment: (database-development|migration-publisher|migration-starter)|DATABASE_IMAGE_PUBLISHER_CLIENT_ID|DATABASE_JOB_STARTER_CLIENT_ID/);
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

test('ARM denial classifier rejects inconclusive and non-authorization results', () => {
  const directory = mkdtempSync(join(tmpdir(), 'migration-arm-denial-'));
  const errorFile = join(directory, 'arm.err');
  const script = '.github/scripts/require-arm-authorization-denial.sh';
  const classify = (message, exitCode = '1') => {
    writeFileSync(errorFile, message);
    const result = spawnSync('bash', [script, errorFile, exitCode], { encoding: 'utf8' });
    return { status: result.status, classification: result.stdout.trim(), stderr: result.stderr };
  };
  try {
    assert.deepEqual(classify('ERROR: (AuthorizationFailed) The client does not have authorization.\n'), { status: 0, classification: 'authorization_failed', stderr: '' });
    assert.equal(classify('ERROR: (SubscriptionNotFound) Subscription was not found.\nAuthorizationFailed\n').classification, 'subscription_resolution_failed');
    assert.equal(classify('ERROR: (InvalidAuthenticationToken) Authentication failed.\n').classification, 'authentication_failed');
    assert.equal(classify('ERROR: (ResourceNotFound) HTTP 404.\n').classification, 'resource_not_found');
    assert.equal(classify('ERROR: (TooManyRequests) HTTP 429.\n').classification, 'throttled');
    assert.equal(classify('ERROR: connection timed out while resolving host.\n').classification, 'network_failed');
    assert.equal(classify('not-json and not an ARM error\n').classification, 'malformed_or_ambiguous');
    assert.equal(classify('ERROR: (AuthorizationFailed) This would be an unexpected HTTP success.\n', '0').classification, 'unexpected_success');
    for (const message of ['Bearer abc.def.ghi', 'access_token=secret', 'Authorization: token']) {
      const result = classify(message);
      assert.equal(result.classification, 'malformed_or_ambiguous');
      assert.doesNotMatch(result.classification, /abc|secret|token/i);
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
  assert.match(runner, /rm -f "\$read_error" "\$write_error"/);
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

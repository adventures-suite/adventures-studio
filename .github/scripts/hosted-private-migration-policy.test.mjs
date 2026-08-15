import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');

const namedStep = (workflow, name) => workflow.match(
  new RegExp(`      - name: ${name.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\\\$&')}\\n(?<body>[\\s\\S]*?)(?=\\n      - (?:name:|uses:|if:)|$)`),
)?.groups?.body;

test('hosted migration workflow preserves exact manual and identity boundaries', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  assert.match(workflow, /workflow_dispatch:/);
  assert.doesNotMatch(workflow, /^\s+(push|pull_request):/m);
  assert.match(workflow, /environment: database-development/);
  assert.match(workflow, /^\s+group: private-sql-migration-vnet$/m);
  assert.match(workflow, /labels: adventures-suite-private-sql/);
  assert.match(workflow, /allow-no-subscriptions: true/);
  assert.match(workflow, /ADVENTURESSUITE_MIGRATION_CREDENTIAL_MODE: github-oidc-azure-cli/);
  assert.match(workflow, /cancel-in-progress: false/);
  assert.doesNotMatch(workflow, /retry|continue-on-error|az deployment|az role assignment/i);
  const uses = [...workflow.matchAll(/uses:\s*[^@\s]+@([^\s#]+)/g)].map(match => match[1]);
  assert.equal(uses.length, 2);
  assert.ok(uses.every(reference => /^[0-9a-f]{40}$/.test(reference)));
});

test('runner group selection is one fixed literal and never an expression', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  const groupSelectors = workflow.match(/^\s+group:.*$/gm) ?? [];
  assert.deepEqual(groupSelectors, ['      group: private-sql-migration-vnet']);
  assert.doesNotMatch(workflow, /PRIVATE_MIGRATION_RUNNER_GROUP|readiness-guard/);
  assert.doesNotMatch(workflow, /^\s+group:.*\$\{\{/m);
});

test('job environment excludes runtime-only contexts and paths derive from RUNNER_TEMP', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  const jobEnvironment = workflow.match(/^    env:\n(?<body>(?:^      .+\n)+)    steps:/m)?.groups?.body;
  assert.ok(jobEnvironment, 'operate job env must remain statically inspectable');
  const contexts = [...jobEnvironment.matchAll(/\$\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\./g)]
    .map(match => match[1]);
  const jobEnvContextAllowlist = new Set(['github', 'needs', 'strategy', 'matrix', 'vars', 'secrets', 'inputs']);
  assert.ok(contexts.every(context => jobEnvContextAllowlist.has(context)),
    'job env contains a context unavailable before runner allocation');
  assert.doesNotMatch(workflow, /^\s+(?:ARTIFACT_DIRECTORY|WORK_DIRECTORY|VERIFIED_DIRECTORY):.*\$\{\{/m);

  const initialization = workflow.match(
    /- name: Initialize trusted runner-temporary paths\n(?<body>[\s\S]*?)\n      - uses:/,
  )?.groups?.body;
  assert.ok(initialization, 'trusted path initialization must be the first runner step');
  assert.match(initialization, /test -n "\$\{RUNNER_TEMP:-\}"/);
  assert.match(initialization, /ARTIFACT_DIRECTORY=%s\/migration\/artifact/);
  assert.match(initialization, /WORK_DIRECTORY=%s\/migration/);
  assert.match(initialization, /VERIFIED_DIRECTORY=%s\/migration\/verified/);
  assert.equal((initialization.match(/printf '[^']+' "\$RUNNER_TEMP"/g) ?? []).length, 3);
  assert.match(initialization, />> "\$GITHUB_ENV"/);
  assert.doesNotMatch(initialization, /\$\{\{\s*(?:inputs|vars|github|env|needs|strategy|matrix|job|steps|runner)\./);
});

test('proof-only path has no SQL command and package verification is exact', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  const proof = await read('.github/scripts/prove-private-sql-network.sh');
  const verify = await read('.github/scripts/verify-reviewed-migration-package.sh');
  assert.match(workflow, /if: inputs\.operation == 'proof-only'/);
  assert.match(workflow, /sqlCommandAttempted\":false/);
  assert.doesNotMatch(proof, /sqlcmd|Invoke-Sqlcmd|AdventuresSuite\.DatabaseMigrator|SELECT\s/i);
  for (const binding of ['sourceSha', 'packageSha256', 'orderedMigrationCatalogSha256',
    'buildRunId', 'dependencyLocks', 'gh attestation verify', '--source-digest'])
    assert.ok(verify.includes(binding), `missing ${binding}`);
  assert.match(proof, /private_sql_network_proof_failed/);
  assert.match(proof, /sqlCommandAttempted/);
});

test('GitHub token is scoped only to the two gh-using package steps', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  const retrieval = namedStep(workflow, 'Retrieve exact package artifact');
  const verification = namedStep(workflow, 'Verify package, locks, catalog, digest, and attestation');

  assert.ok(retrieval, 'package retrieval step must remain present');
  assert.ok(verification, 'package verification step must remain present');
  assert.match(retrieval, /^        env:\n          GH_TOKEN: \$\{\{ github\.token \}\}$/m);
  assert.match(verification, /^        env:\n          GH_TOKEN: \$\{\{ github\.token \}\}$/m);
  assert.equal((workflow.match(/GH_TOKEN:/g) ?? []).length, 2);

  const steps = workflow.split(/^      - /m).slice(1);
  for (const step of steps) {
    if (step.startsWith('name: Retrieve exact package artifact\n') ||
        step.startsWith('name: Verify package, locks, catalog, digest, and attestation\n'))
      continue;
    assert.doesNotMatch(step, /GH_TOKEN|github\.token/,
      'GitHub token must not reach networking, SQL, migration, cleanup, or unrelated steps');
  }

  assert.match(workflow,
    /^    permissions: \{ contents: read, actions: read, attestations: read, id-token: write \}$/m);
  assert.deepEqual(workflow.match(/^\s*permissions:.*$/gm), [
    'permissions: { contents: read }',
    '    permissions: { contents: read, actions: read, attestations: read, id-token: write }',
  ]);
});

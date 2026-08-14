import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');

test('hosted migration workflow preserves exact manual and identity boundaries', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  assert.match(workflow, /workflow_dispatch:/);
  assert.doesNotMatch(workflow, /^\s+(push|pull_request):/m);
  assert.match(workflow, /environment: database-development/);
  assert.match(workflow, /RUNNER_GROUP: \$\{\{ vars\.PRIVATE_MIGRATION_RUNNER_GROUP \}\}/);
  assert.match(workflow, /test "\$RUNNER_GROUP" = 'private-sql-migration-vnet'/);
  assert.match(workflow, /runner-group: \$\{\{ steps\.validate-runner-group\.outputs\.runner-group \}\}/);
  assert.match(workflow, /'runner-group=private-sql-migration-vnet' >> "\$GITHUB_OUTPUT"/);
  assert.match(workflow, /if: needs\.readiness-guard\.result == 'success' && needs\.readiness-guard\.outputs\.runner-group == 'private-sql-migration-vnet'/);
  assert.match(workflow, /group: \$\{\{ needs\.readiness-guard\.outputs\.runner-group \}\}/);
  assert.doesNotMatch(workflow, /^\s+group: \$\{\{ vars\./m);
  assert.doesNotMatch(workflow, /^\s+group: private-sql-migration-vnet$/m);
  assert.match(workflow, /labels: adventures-suite-private-sql/);
  assert.match(workflow, /allow-no-subscriptions: true/);
  assert.match(workflow, /ADVENTURESSUITE_MIGRATION_CREDENTIAL_MODE: github-oidc-azure-cli/);
  assert.match(workflow, /cancel-in-progress: false/);
  assert.doesNotMatch(workflow, /retry|continue-on-error|az deployment|az role assignment/i);
  const uses = [...workflow.matchAll(/uses:\s*[^@\s]+@([^\s#]+)/g)].map(match => match[1]);
  assert.equal(uses.length, 2);
  assert.ok(uses.every(reference => /^[0-9a-f]{40}$/.test(reference)));
});

test('runner selection is emitted only by the protected fail-closed readiness job', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  const guard = workflow.slice(workflow.indexOf('  readiness-guard:'), workflow.indexOf('\n  operate:'));
  assert.match(guard, /environment: database-development/);
  assert.match(guard, /runs-on: ubuntu-24\.04/);
  assert.match(guard, /permissions: \{ contents: read \}/);
  assert.doesNotMatch(guard, /id-token|uses:|checkout|artifact|azure\/login|\baz\s|sqlcmd|DatabaseMigrator|curl|\bnc\s|\.github\/scripts/i);
  assert.match(guard, /test "\$RUNNER_GROUP" = 'private-sql-migration-vnet'\n\s+printf/);
  assert.equal((guard.match(/>> "\$GITHUB_OUTPUT"/g) ?? []).length, 1);
  assert.doesNotMatch(workflow, /group: \$\{\{ (?:inputs|github|vars|env|matrix|strategy)\./);
});

test('missing or wrong runner configuration emits no usable group output', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  const guard = workflow.slice(workflow.indexOf('  readiness-guard:'), workflow.indexOf('\n  operate:'));
  const script = guard.match(/        run: \|\n([\s\S]+)$/)[1]
    .split('\n').map(line => line.replace(/^ {10}/, '')).join('\n');
  const directory = await mkdtemp(join(tmpdir(), 'runner-group-guard-'));
  try {
    for (const runnerGroup of ['', 'Default', 'private-sql-migration-vnet-extra']) {
      const output = join(directory, `output-${runnerGroup || 'missing'}`);
      await writeFile(output, '');
      const result = spawnSync('bash', ['-c', script], {
        env: {
          GITHUB_OUTPUT: output,
          GITHUB_REF: 'refs/heads/main',
          GITHUB_SHA: 'a'.repeat(40),
          SOURCE_SHA: 'a'.repeat(40),
          RUNNER_READY: 'private-sql-vnet-runner-v1',
          RUNNER_GROUP: runnerGroup
        },
        encoding: 'utf8'
      });
      assert.notEqual(result.status, 0);
      assert.equal(await readFile(output, 'utf8'), '');
    }
    const output = join(directory, 'output-valid');
    await writeFile(output, '');
    const result = spawnSync('bash', ['-c', script], {
      env: {
        GITHUB_OUTPUT: output,
        GITHUB_REF: 'refs/heads/main',
        GITHUB_SHA: 'a'.repeat(40),
        SOURCE_SHA: 'a'.repeat(40),
        RUNNER_READY: 'private-sql-vnet-runner-v1',
        RUNNER_GROUP: 'private-sql-migration-vnet'
      },
      encoding: 'utf8'
    });
    assert.equal(result.status, 0);
    assert.equal(await readFile(output, 'utf8'), 'runner-group=private-sql-migration-vnet\n');
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
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

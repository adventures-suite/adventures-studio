import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');

test('hosted migration workflow preserves exact manual and identity boundaries', async () => {
  const workflow = await read('.github/workflows/private-migration-runner.yml');
  assert.match(workflow, /workflow_dispatch:/);
  assert.doesNotMatch(workflow, /^\s+(push|pull_request):/m);
  assert.match(workflow, /environment: database-development/);
  assert.match(workflow, /group: private-sql-migration-vnet/);
  assert.match(workflow, /labels: adventures-suite-private-sql/);
  assert.match(workflow, /allow-no-subscriptions: true/);
  assert.match(workflow, /ADVENTURESSUITE_MIGRATION_CREDENTIAL_MODE: github-oidc-azure-cli/);
  assert.match(workflow, /cancel-in-progress: false/);
  assert.doesNotMatch(workflow, /retry|continue-on-error|az deployment|az role assignment/i);
  const uses = [...workflow.matchAll(/uses:\s*[^@\s]+@([^\s#]+)/g)].map(match => match[1]);
  assert.equal(uses.length, 2);
  assert.ok(uses.every(reference => /^[0-9a-f]{40}$/.test(reference)));
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

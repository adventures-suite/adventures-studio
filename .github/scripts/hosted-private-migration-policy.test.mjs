import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { chmod, mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');

const namedStep = (workflow, name) => workflow.match(
  new RegExp(`      - name: ${name.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\\\$&')}\\n(?<body>[\\s\\S]*?)(?=\\n      - (?:name:|uses:|if:)|$)`),
)?.groups?.body;

const sourceSha = '0123456789abcdef0123456789abcdef01234567';
const packageBase = `adventures-suite-database-migrator-${sourceSha}`;

const runPackageVerifier = async evidenceFiles => {
  const root = await mkdtemp(path.join(tmpdir(), 'migration-package-verifier-'));
  try {
    const artifactDirectory = path.join(root, 'artifact');
    const packageDirectory = path.join(artifactDirectory, 'database-migrator-package');
    const payloadDirectory = path.join(root, 'payload');
    const verifiedDirectory = path.join(root, 'verified');
    const stubDirectory = path.join(root, 'bin');
    await Promise.all([
      mkdir(packageDirectory, { recursive: true }),
      mkdir(payloadDirectory),
      mkdir(stubDirectory),
    ]);
    for (const executable of ['AdventuresSuite.DatabaseMigrator', 'run-reviewed-migration-operation.sh']) {
      const executablePath = path.join(payloadDirectory, executable);
      await writeFile(executablePath, '#!/usr/bin/env bash\nexit 0\n');
      await chmod(executablePath, 0o755);
    }
    const packagePath = path.join(packageDirectory, `${packageBase}.tar.gz`);
    execFileSync('tar', ['-czf', packagePath, '-C', payloadDirectory, '.']);
    const packageSha256 = createHash('sha256').update(await readFile(packagePath)).digest('hex');
    const evidence = JSON.stringify({
      schemaVersion: 1,
      sourceSha,
      packageSha256,
      orderedMigrationCatalogSha256: 'a'.repeat(64),
      buildRunId: '123',
      toolchain: { dotnetSdkVersion: '10.0.302', runtimeIdentifier: 'linux-x64', selfContained: true },
      dependencyLocks: Array.from({ length: 6 }, (_, index) => ({ path: `lock-${index}`, sha256: 'b'.repeat(64) })),
      attestation: { required: true },
    });
    for (const entry of evidenceFiles) {
      await writeFile(path.join(packageDirectory, entry.name), entry.content ?? evidence);
    }
    for (const command of ['gh', 'node']) {
      const stub = path.join(stubDirectory, command);
      await writeFile(stub, '#!/usr/bin/env bash\nexit 0\n');
      await chmod(stub, 0o755);
    }
    const bashEnvironment = path.join(root, 'bash-env');
    await writeFile(bashEnvironment, `
if ! type mapfile >/dev/null 2>&1; then
  mapfile() {
    local name line index=0
    if [ "$1" = '-t' ]; then name="$2"; else name="$1"; fi
    eval "$name=()"
    while IFS= read -r line; do
      eval "$name[$index]=\\"$line\\""
      index=$((index + 1))
    done
  }
fi
`);
    return spawnSync('/bin/bash', ['.github/scripts/verify-reviewed-migration-package.sh'], {
      cwd: new URL('../..', import.meta.url),
      encoding: 'utf8',
      env: {
        ...process.env,
        PATH: `${stubDirectory}:${process.env.PATH}`,
        BASH_ENV: bashEnvironment,
        ARTIFACT_DIRECTORY: artifactDirectory,
        EXPECTED_SOURCE_SHA: sourceSha,
        EXPECTED_PACKAGE_SHA256: packageSha256,
        EXPECTED_CATALOG_SHA256: 'a'.repeat(64),
        EXPECTED_RUN_ID: '123',
        VERIFIED_DIRECTORY: verifiedDirectory,
      },
    });
  } finally {
    await rm(root, { recursive: true, force: true });
  }
};

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

test('package verifier accepts the exact published sibling evidence layout', async () => {
  const result = await runPackageVerifier([{ name: `${packageBase}.evidence.json` }]);
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /"classification":"migration_package_verified"/);
});

test('package verifier rejects missing, duplicate, substituted, malformed, or incorrectly named evidence', async () => {
  const cases = [
    [],
    [{ name: `${packageBase}.evidence.json` }, { name: 'duplicate.evidence.json' }],
    [{ name: `adventures-suite-database-migrator-${'f'.repeat(40)}.evidence.json` }],
    [{ name: `${packageBase}.evidence.json`, content: '{malformed' }],
    [{ name: `${packageBase}.tar.gz.evidence.json` }],
  ];
  for (const evidenceFiles of cases) {
    const result = await runPackageVerifier(evidenceFiles);
    assert.notEqual(result.status, 0, `unexpected success for ${evidenceFiles.map(file => file.name)}`);
    assert.doesNotMatch(result.stdout, /migration_package_verified/);
    assert.match(result.stderr, /"classification":"migration_package_verification_failed"/);
  }
});

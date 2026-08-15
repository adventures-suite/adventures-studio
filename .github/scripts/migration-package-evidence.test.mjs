import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { createEvidence, REQUIRED_LOCK_PATHS, validateEvidenceLocks } from './migration-package-evidence.mjs';

async function fixture() {
  const root = await mkdtemp(join(tmpdir(), 'migration-package-evidence-'));
  for (const path of REQUIRED_LOCK_PATHS) {
    await mkdir(join(root, path, '..'), { recursive: true });
    await writeFile(join(root, path), `{"path":"${path}"}\n`);
  }
  await writeFile(join(root, 'package.tar.gz'), 'immutable-package');
  await writeFile(join(root, 'catalog.txt'), '0007.sql\n0008.sql\n');
  return { root };
}

test('creates exact deterministic package contract', async () => {
  const { root } = await fixture();
  const evidence = await createEvidence({ sourceSha: 'a'.repeat(40), buildRunId: '123',
    sdkVersion: '10.0.302', packagePath: join(root, 'package.tar.gz'),
    catalogPath: join(root, 'catalog.txt'), lockPaths: REQUIRED_LOCK_PATHS,
    artifactName: 'database-migrator-a', rootDir: root });
  assert.match(evidence.packageSha256, /^[0-9a-f]{64}$/);
  assert.match(evidence.orderedMigrationCatalogSha256, /^[0-9a-f]{64}$/);
  assert.equal(evidence.toolchain.selfContained, true);
  assert.equal(evidence.attestation.required, true);
});

for (const [name, value, message] of [
  ['sourceSha', 'main', /source SHA/], ['buildRunId', '0', /run ID/],
  ['sdkVersion', '10.0.x', /SDK version/]
]) test(`rejects invalid ${name}`, async () => {
  const { root } = await fixture();
  const input = { sourceSha: 'a'.repeat(40), buildRunId: '123', sdkVersion: '10.0.302',
    packagePath: join(root, 'package.tar.gz'), catalogPath: join(root, 'catalog.txt'),
    lockPaths: REQUIRED_LOCK_PATHS, artifactName: 'database-migrator-a', rootDir: root, [name]: value };
  await assert.rejects(() => createEvidence(input), message);
});

test('rejects missing, duplicate, or non-runtime-lock dependency evidence', async () => {
  const { root } = await fixture();
  const base = { sourceSha: 'a'.repeat(40), buildRunId: '123', sdkVersion: '10.0.302',
    packagePath: join(root, 'package.tar.gz'), catalogPath: join(root, 'catalog.txt'),
    artifactName: 'database-migrator-a', rootDir: root };
  await assert.rejects(() => createEvidence({ ...base, lockPaths: [] }), /dependency lock/);
  await assert.rejects(() => createEvidence({ ...base, lockPaths: [...REQUIRED_LOCK_PATHS, REQUIRED_LOCK_PATHS[0]] }), /dependency lock/);
  await assert.rejects(() => createEvidence({ ...base, lockPaths: [join(root, 'catalog.txt')] }), /dependency lock/);
});

test('accepts only the exact ordered six-path lock catalog and matching digests', async () => {
  const { root } = await fixture();
  const evidence = await createEvidence({ sourceSha: 'a'.repeat(40), buildRunId: '123',
    sdkVersion: '10.0.302', packagePath: join(root, 'package.tar.gz'),
    catalogPath: join(root, 'catalog.txt'), lockPaths: REQUIRED_LOCK_PATHS,
    artifactName: 'database-migrator-a', rootDir: root });
  assert.equal(await validateEvidenceLocks(evidence, root), true);

  for (let index = 0; index < REQUIRED_LOCK_PATHS.length; index++) {
    const missing = structuredClone(evidence);
    missing.dependencyLocks.splice(index, 1);
    await assert.rejects(() => validateEvidenceLocks(missing, root), /schema|catalog/);
  }
  for (const mutate of [
    locks => locks.push(structuredClone(locks[0])),
    locks => { locks[1] = structuredClone(locks[0]); },
    locks => { locks[0].path = 'src/Substituted/packages.linux-x64.lock.json'; },
    locks => locks.push({ path: 'src/Extra/packages.linux-x64.lock.json', sha256: 'a'.repeat(64) }),
    locks => { locks[0].path = '../packages.linux-x64.lock.json'; },
    locks => { locks[0].path = '/tmp/packages.linux-x64.lock.json'; },
    locks => { locks[0].path = locks[0].path.toUpperCase(); },
    locks => locks.reverse(),
    locks => { locks[0].unexpected = true; },
    locks => { locks[0].sha256 = 'f'.repeat(64); }
  ]) {
    const invalid = structuredClone(evidence);
    mutate(invalid.dependencyLocks);
    await assert.rejects(() => validateEvidenceLocks(invalid, root), /schema|catalog|digest/);
  }
  for (const target of [
    evidence,
    evidence.toolchain,
    evidence.attestation
  ]) {
    const invalid = structuredClone(evidence);
    const selected = target === evidence ? invalid
      : target === evidence.toolchain ? invalid.toolchain : invalid.attestation;
    selected.unexpected = true;
    await assert.rejects(() => validateEvidenceLocks(invalid, root), /schema/);
  }
});

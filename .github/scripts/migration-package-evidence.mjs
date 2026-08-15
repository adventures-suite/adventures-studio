import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { join, resolve } from 'node:path';

const SHA40 = /^[0-9a-f]{40}$/;
const SHA256 = /^[0-9a-f]{64}$/;
const RUN_ID = /^[1-9][0-9]*$/;
const SDK = /^10\.0\.[0-9]{3}$/;
export const REQUIRED_LOCK_PATHS = [
  'src/AdventuresSuite.Authorization/packages.linux-x64.lock.json',
  'src/AdventuresSuite.Companion.Application/packages.linux-x64.lock.json',
  'src/AdventuresSuite.Companion.Contracts/packages.linux-x64.lock.json',
  'src/AdventuresSuite.Companion.SqlServer/packages.linux-x64.lock.json',
  'src/AdventuresSuite.DatabaseMigrator/packages.linux-x64.lock.json',
  'src/AdventuresSuite.Identity/packages.linux-x64.lock.json'
];

const hash = value => createHash('sha256').update(value).digest('hex');

const exactKeys = (value, expected) => value && typeof value === 'object'
  && !Array.isArray(value)
  && Object.keys(value).sort().join('\n') === [...expected].sort().join('\n');

export async function validateEvidenceLocks(evidence, rootDir = '.') {
  if (!exactKeys(evidence, ['schemaVersion', 'sourceSha', 'packageSha256',
    'orderedMigrationCatalogSha256', 'toolchain', 'dependencyLocks', 'buildRunId',
    'artifactName', 'attestation'])
    || !exactKeys(evidence.toolchain,
      ['dotnetSdkVersion', 'runtimeIdentifier', 'selfContained'])
    || !exactKeys(evidence.attestation, ['required', 'provider', 'predicateType'])
    || !Array.isArray(evidence.dependencyLocks)
    || evidence.dependencyLocks.length !== REQUIRED_LOCK_PATHS.length)
    throw new Error('migration package evidence schema is not approved');

  for (let index = 0; index < REQUIRED_LOCK_PATHS.length; index++) {
    const record = evidence.dependencyLocks[index];
    if (!exactKeys(record, ['path', 'sha256'])
      || record.path !== REQUIRED_LOCK_PATHS[index]
      || !SHA256.test(record.sha256))
      throw new Error('dependency lock evidence is not the exact approved ordered catalog');
    const actual = hash(await readFile(join(rootDir, record.path)));
    if (actual !== record.sha256)
      throw new Error('dependency lock digest does not match');
  }
  return true;
}

export async function createEvidence({ sourceSha, buildRunId, sdkVersion, packagePath,
  catalogPath, lockPaths, artifactName, rootDir = '.' }) {
  if (!SHA40.test(sourceSha)) throw new Error('source SHA must be 40 lowercase hexadecimal characters');
  if (!RUN_ID.test(buildRunId)) throw new Error('build run ID must be a positive integer');
  if (!SDK.test(sdkVersion)) throw new Error('SDK version must be an exact .NET 10 feature-band version');
  if (!artifactName || /[\r\n]/.test(artifactName)) throw new Error('artifact name is invalid');
  if (!Array.isArray(lockPaths)
      || ![...lockPaths].sort().every((path, index) => path === REQUIRED_LOCK_PATHS[index])
      || lockPaths.length !== REQUIRED_LOCK_PATHS.length)
    throw new Error('dependency lock paths must exactly match the migrator project graph');

  const packageBytes = await readFile(packagePath);
  const catalogBytes = await readFile(catalogPath);
  const dependencyLocks = [];
  for (const path of [...lockPaths].sort()) {
    dependencyLocks.push({ path, sha256: hash(await readFile(join(rootDir, path))) });
  }
  const evidence = {
    schemaVersion: 1,
    sourceSha,
    packageSha256: hash(packageBytes),
    orderedMigrationCatalogSha256: hash(catalogBytes),
    toolchain: { dotnetSdkVersion: sdkVersion, runtimeIdentifier: 'linux-x64', selfContained: true },
    dependencyLocks,
    buildRunId,
    artifactName,
    attestation: {
      required: true,
      provider: 'GitHub artifact attestations',
      predicateType: 'https://slsa.dev/provenance/v1'
    }
  };
  if (!SHA256.test(evidence.packageSha256) || !SHA256.test(evidence.orderedMigrationCatalogSha256))
    throw new Error('computed evidence hash is invalid');
  return evidence;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.argv[2] === '--verify-locks') {
    if (process.argv.length !== 5 || process.argv[4] !== '--root')
      throw new Error('lock verification arguments are invalid');
    const evidence = JSON.parse(await readFile(process.argv[3], 'utf8'));
    await validateEvidenceLocks(evidence, '.');
    process.exit(0);
  }
  const values = new Map();
  const locks = [];
  for (let index = 2; index < process.argv.length; index += 2) {
    const key = process.argv[index];
    const value = process.argv[index + 1];
    if (!key?.startsWith('--') || value === undefined) throw new Error('arguments must be name/value pairs');
    if (key === '--lock') locks.push(value); else if (values.has(key)) throw new Error(`duplicate ${key}`); else values.set(key, value);
  }
  const required = ['--source-sha', '--build-run-id', '--sdk-version', '--package', '--catalog', '--artifact-name', '--output'];
  if (required.some(key => !values.has(key))) throw new Error('required package evidence argument is missing');
  const evidence = await createEvidence({
    sourceSha: values.get('--source-sha'), buildRunId: values.get('--build-run-id'),
    sdkVersion: values.get('--sdk-version'), packagePath: values.get('--package'),
    catalogPath: values.get('--catalog'), lockPaths: locks, artifactName: values.get('--artifact-name')
  });
  await writeFile(values.get('--output'), `${JSON.stringify(evidence, null, 2)}\n`, { flag: 'wx' });
}

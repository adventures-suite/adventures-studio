#!/usr/bin/env node
import { createReadStream } from 'node:fs';
import { ManagedIdentityCredential } from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';
import { assertAnonymousDescriptor, assertBoundedEvidence, importFictionalOrFutureKey } from './key-custody-importer.mjs';

const allowed = new Set(['--operation-id','--vault-id','--github-key-id','--importer-client-id','--started-utc']);
const values = new Map();
for (let index = 2; index < process.argv.length; index += 2) {
  const key = process.argv[index];
  const value = process.argv[index + 1];
  if (!allowed.has(key) || typeof value !== 'string') throw new Error('closed-arguments');
  values.set(key, value);
}
if (values.size !== allowed.size) throw new Error('closed-arguments');

const fd = 3;
assertAnonymousDescriptor(fd);
const controller = new AbortController();
const deadline = setTimeout(() => controller.abort(new DOMException('Timed out', 'TimeoutError')), 5 * 60 * 1000);
for (const event of ['SIGINT','SIGTERM','SIGHUP']) process.once(event, () => controller.abort(new DOMException('Cancelled', 'AbortError')));

try {
  const vaultId = values.get('--vault-id');
  const vaultName = vaultId.split('/').at(-1);
  const importerClientId = values.get('--importer-client-id');
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(importerClientId)) throw new Error('exact-importer-client-id-required');
  const client = new SecretClient(`https://${vaultName}.vault.azure.net`, new ManagedIdentityCredential(importerClientId));
  const evidence = await importFictionalOrFutureKey({
    input: createReadStream(null, { fd, autoClose: true, signal: controller.signal }),
    secretClient: client,
    operationId: values.get('--operation-id'),
    vaultId,
    githubKeyId: values.get('--github-key-id'),
    startedUtc: values.get('--started-utc'),
    signal: controller.signal
  });
  assertBoundedEvidence(evidence);
  process.stdout.write(`${JSON.stringify(evidence)}\n`);
} catch (error) {
  const safeCodes = new Set(['closed-arguments','exact-importer-client-id-required','nonpersistent-descriptor-required','operation-id-binding','vault-id-binding','github-key-id-binding','bounded-timestamp-required','pem-size-bound','pem-empty','pkcs8-rsa-required','rsa-strength','immutable-version-readback']);
  const code = safeCodes.has(error?.code) ? error.code : error?.name === 'AbortError' ? 'key-import-cancelled' : error?.name === 'TimeoutError' ? 'key-import-timeout' : 'key-import-service-failure';
  process.stderr.write(`${code}\n`);
  process.exitCode = 1;
} finally {
  clearTimeout(deadline);
}

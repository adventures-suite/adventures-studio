import { createHash, createPrivateKey, createPublicKey } from 'node:crypto';
import { fstatSync } from 'node:fs';

export const DestinationSecretName = 'github-app-4590229-private-key';
export const DestinationContentType = 'application/x-pem-file';
const MaxPemBytes = 16 * 1024;
const ImmutableSecretId = /^https:\/\/[a-z0-9-]+\.vault\.azure\.net\/secrets\/github-app-4590229-private-key\/[0-9a-f]{32}$/;

function fail(code) {
  const error = new Error(code);
  error.code = code;
  return error;
}

export function assertAnonymousDescriptor(fd, stat = fstatSync(fd)) {
  if (!Number.isInteger(fd) || fd < 3 || (!stat.isFIFO() && !stat.isSocket())) {
    throw fail('nonpersistent-descriptor-required');
  }
}

async function readBounded(stream, signal) {
  const chunks = [];
  let length = 0;
  try {
    for await (const chunk of stream) {
      if (signal?.aborted) throw signal.reason ?? new DOMException('Aborted', 'AbortError');
      const bytes = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
      length += bytes.length;
      if (length === 0 || length > MaxPemBytes) throw fail('pem-size-bound');
      chunks.push(bytes);
    }
    if (length === 0) throw fail('pem-empty');
    return Buffer.concat(chunks, length);
  } finally {
    for (const chunk of chunks) chunk.fill(0);
  }
}

function validateAndFingerprint(pem) {
  let privateKey;
  try {
    privateKey = createPrivateKey({ key: pem, format: 'pem', type: 'pkcs8' });
    if (privateKey.asymmetricKeyType !== 'rsa') throw fail('pkcs8-rsa-required');
    const details = privateKey.asymmetricKeyDetails;
    if (!details || details.modulusLength < 2048) throw fail('rsa-strength');
    const publicDer = createPublicKey(privateKey).export({ format: 'der', type: 'spki' });
    return `SHA256:${createHash('sha256').update(publicDer).digest('base64url')}`;
  } catch (error) {
    if (error?.code && String(error.code).startsWith('pkcs8')) throw error;
    throw fail('pkcs8-rsa-required');
  }
}

function timestamp(value) {
  const parsed = new Date(value);
  if (!Number.isFinite(parsed.valueOf()) || parsed.toISOString() !== value) throw fail('bounded-timestamp-required');
  return value;
}

export async function importFictionalOrFutureKey({ input, secretClient, operationId, vaultId, githubKeyId, startedUtc, now = () => new Date().toISOString(), signal }) {
  if (!/^broker-key-[a-z0-9]{16,64}$/.test(operationId)) throw fail('operation-id-binding');
  if (!/^\/subscriptions\/[0-9a-f-]{36}\/resourceGroups\/rg-adventures-suite-dev\/providers\/Microsoft\.KeyVault\/vaults\/kv-adventures-runner-dev$/i.test(vaultId)) throw fail('vault-id-binding');
  if (!/^[0-9]{1,20}$/.test(githubKeyId)) throw fail('github-key-id-binding');
  timestamp(startedUtc);
  if (signal?.aborted) throw signal.reason ?? new DOMException('Aborted', 'AbortError');

  let pem;
  try {
    pem = await readBounded(input, signal);
    if (signal?.aborted) throw signal.reason ?? new DOMException('Aborted', 'AbortError');
    const fingerprint = validateAndFingerprint(pem);
    const response = await secretClient.setSecret(DestinationSecretName, pem.toString('utf8'), {
      contentType: DestinationContentType,
      enabled: true,
      tags: { purpose: 'runner-broker-github-app-key', githubAppId: '4590229' },
      abortSignal: signal
    });
    const immutableVersionUri = response?.id;
    if (typeof immutableVersionUri !== 'string' || !ImmutableSecretId.test(immutableVersionUri)) throw fail('immutable-version-readback');
    return Object.freeze({
      schemaVersion: 1,
      operationId,
      vaultId,
      secretName: DestinationSecretName,
      immutableVersionUri,
      githubKeyId,
      publicKeyFingerprint: fingerprint,
      startedUtc,
      completedUtc: timestamp(now())
    });
  } finally {
    pem?.fill(0);
  }
}

export function assertBoundedEvidence(evidence) {
  const keys = ['schemaVersion','operationId','vaultId','secretName','immutableVersionUri','githubKeyId','publicKeyFingerprint','startedUtc','completedUtc'];
  if (!evidence || Object.keys(evidence).sort().join('|') !== keys.sort().join('|')) throw fail('evidence-schema');
  if (JSON.stringify(evidence).length > 2048) throw fail('evidence-size');
  if (!ImmutableSecretId.test(evidence.immutableVersionUri)) throw fail('immutable-version-readback');
}

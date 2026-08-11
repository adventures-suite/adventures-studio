import { createHash } from 'node:crypto';

function fail(message) { throw new Error(message); }
function sameSet(actual, expected) {
  return actual.length === expected.length && [...actual].sort().every((value, index) => value === [...expected].sort()[index]);
}

export function validateJobDefinition(job, expected) {
  const configuration = job?.properties?.configuration ?? fail('missing Job configuration');
  const template = job?.properties?.template ?? fail('missing Job template');
  const containers = template.containers ?? [];
  if (containers.length !== 1) fail('unexpected container count');
  const container = containers[0];
  if (container.image !== expected.image) fail('image digest drift');
  if (job.properties.environmentId !== expected.environmentId) fail('environment drift');
  if (configuration.triggerType !== 'Manual') fail('trigger drift');
  if (configuration.replicaTimeout !== 900) fail('timeout drift');
  if (configuration.replicaRetryLimit !== 0) fail('retry drift');
  if (configuration.manualTriggerConfig?.parallelism !== 1 ||
      configuration.manualTriggerConfig?.replicaCompletionCount !== 1) fail('replica drift');
  if (container.name !== 'database-migrator' ||
      JSON.stringify(container.command) !== JSON.stringify(['/app/container-entrypoint.sh']) ||
      JSON.stringify(container.args) !== JSON.stringify(['--verify-execution-channel'])) fail('entrypoint drift');
  const identities = Object.keys(job?.identity?.userAssignedIdentities ?? {});
  if (job?.identity?.type !== 'UserAssigned') fail('identity type drift');
  if (!sameSet(identities, [expected.migrationIdentityId, expected.pullIdentityId])) fail('identity drift');
  const registries = configuration.registries ?? [];
  if (registries.length !== 1 || registries[0].identity !== expected.pullIdentityId) fail('registry identity drift');
  if ((configuration.secrets ?? []).length !== 0) fail('unexpected secrets');
  const envNames = (container.env ?? []).map(item => item.name);
  if (envNames.includes('ADVENTURESSUITE_MIGRATION_OPERATION_ID') ||
      envNames.includes('ADVENTURESSUITE_ARTIFACT_SHA256')) fail('persistent operation value drift');
  const env = Object.fromEntries((container.env ?? []).map(item => [item.name, item.value]));
  if (env.ADVENTURESSUITE_RELEASE_SHA !== expected.releaseSha ||
      env.ADVENTURESSUITE_IMAGE_DIGEST !== expected.imageDigest) fail('release identity drift');
  return true;
}

export function validateRoleAssignmentScope(actualScope, expectedJobId) {
  if (actualScope !== expectedJobId) fail('starter role scope is broader than the exact Job');
  return true;
}

function collectJson(value, found) {
  if (Array.isArray(value)) for (const item of value) collectJson(item, found);
  else if (value && typeof value === 'object') {
    if (value.eventName === 'migration-job-completion') found.push(value);
    for (const nested of Object.values(value)) collectJson(nested, found);
  } else if (typeof value === 'string' && value.includes('migration-job-completion')) {
    const start = value.indexOf('{');
    if (start >= 0) {
      try { collectJson(JSON.parse(value.slice(start)), found); } catch { /* not an envelope */ }
    }
  }
}

export function validateExecutionEvidence(status, logsText, expected) {
  if (status?.properties?.status !== 'Succeeded') fail('exact execution did not succeed');
  if (!logsText?.trim()) fail('execution logs are missing');
  if (Buffer.byteLength(logsText) > 1024 * 1024) fail('execution logs exceed the evidence bound');
  const envelopes = [];
  try { collectJson(JSON.parse(logsText), envelopes); } catch {
    for (const line of logsText.split(/\r?\n/).filter(Boolean)) {
      try { collectJson(JSON.parse(line), envelopes); } catch { /* bounded non-JSON log line */ }
    }
  }
  if (envelopes.length !== 1) fail(`expected one completion envelope; found ${envelopes.length}`);
  const envelope = envelopes[0];
  const checksum = createHash('sha256').update(JSON.stringify(envelope.payload)).digest('hex');
  if (checksum !== envelope.envelopeChecksum) fail('completion envelope checksum mismatch');
  const payload = envelope.payload;
  if (payload.operationId !== expected.operationId || payload.releaseSha !== expected.releaseSha ||
      payload.imageDigest !== expected.imageDigest) fail('completion envelope identity mismatch');
  if (payload.processExitCode !== 0 || payload.classification !== expected.classification) fail('completion envelope result mismatch');
  return envelope;
}

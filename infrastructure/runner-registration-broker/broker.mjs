import { createHash } from 'node:crypto';

export const States = Object.freeze({ Approved:'Approved', Issuing:'Issuing', Issued:'Issued', Cleaning:'Cleaning', Closed:'Closed', Failed:'Failed' });
const transitions = new Map([[States.Approved,new Set([States.Issuing,States.Failed])],[States.Issuing,new Set([States.Issued,States.Failed])],[States.Issued,new Set([States.Cleaning,States.Failed])],[States.Cleaning,new Set([States.Closed,States.Failed])],[States.Closed,new Set()],[States.Failed,new Set()]]);
const purposes = new Set(['private-migration','sql-administrator-baseline']);
const shaPattern = /^[0-9a-f]{40}$/;
const operationPattern = /^[A-Za-z0-9_-]{16,64}$/;

export function sha256(value) { return createHash('sha256').update(value, 'utf8').digest('hex'); }
export function transition(record, next) {
  if (!transitions.get(record.state)?.has(next)) throw new Error('transition-denied');
  return Object.freeze({ ...record, state: next });
}
export function validateBinding(value, now = new Date()) {
  if (value.repositoryId !== 1317655952 || value.ownerId !== 316268438 || value.repository !== 'adventures-suite/adventures-studio') throw new Error('repository-binding');
  if (!shaPattern.test(value.sourceSha) || value.ref !== 'refs/heads/main' || value.event !== 'workflow_dispatch') throw new Error('source-binding');
  for (const key of ['workflowRef','workflowSha','environment','operationId','purpose','runnerName','workDirectory','oidcAudience']) if (typeof value[key] !== 'string' || value[key].length === 0) throw new Error(`${key}-binding`);
  if (!shaPattern.test(value.workflowSha) || value.environment !== 'database-development' || !operationPattern.test(value.operationId) || !purposes.has(value.purpose) || value.workDirectory !== '_work') throw new Error('operation-binding');
  if (!Number.isSafeInteger(value.runnerGroupId) || value.runnerGroupId <= 0) throw new Error('runner-group-binding');
  const expectedName = `as-${value.purpose}-${value.operationId}`;
  const expectedLabels = Object.freeze(['self-hosted','linux','x64',`operation-${value.operationId}`,`purpose-${value.purpose}`]);
  if (value.runnerName !== expectedName || JSON.stringify(value.labels) !== JSON.stringify(expectedLabels)) throw new Error('runner-substitution');
  const approved = Date.parse(value.approvedUtc), deadline = Date.parse(value.deadlineUtc);
  if (!Number.isFinite(approved) || !Number.isFinite(deadline) || deadline-approved !== 45*60*1000 || now.getTime() >= deadline) throw new Error('deadline-binding');
  return Object.freeze({ ...value, labels: expectedLabels });
}
export class RegistrationBroker {
  constructor(store, github, evidence) { this.store=store; this.github=github; this.evidence=evidence; }
  async redeem(binding, signal) {
    const exact = validateBinding(binding); signal?.throwIfAborted();
    const issuing = await this.store.compareExchange(exact.operationId, States.Approved, States.Issuing);
    if (!issuing) throw new Error('already-redeemed-or-ambiguous');
    try {
      const encoded = await this.github.generateJitConfiguration(exact, signal);
      signal?.throwIfAborted();
      if (typeof encoded !== 'string' || encoded.length < 16 || encoded.length > 131072) throw new Error('invalid-jit-response');
      if (!await this.store.compareExchange(exact.operationId, States.Issuing, States.Issued)) throw new Error('issued-state-ambiguous');
      return encoded;
    } catch (error) {
      await this.store.failIfCurrent(exact.operationId, States.Issuing, 'responseLost');
      throw error;
    }
  }
  async cleanup(binding, signal) {
    const exact=validateBinding(binding); signal?.throwIfAborted();
    if (!await this.store.compareExchange(exact.operationId, States.Issued, States.Cleaning)) throw new Error('cleanup-state-ambiguous');
    const runners=await this.github.listRunners(signal); const expectedLabels=[...exact.labels].sort(); const matches=runners.filter(r=>r.name===exact.runnerName && Array.isArray(r.labels) && r.labels.length===expectedLabels.length && new Set(r.labels.map(x=>x.name)).size===expectedLabels.length && JSON.stringify(r.labels.map(x=>x.name).sort())===JSON.stringify(expectedLabels));
    if (matches.length>1) { await this.store.failIfCurrent(exact.operationId,States.Cleaning,'ambiguous'); throw new Error('runner-match-ambiguous'); }
    let deleteAttempts=0;
    if (matches.length===1) { deleteAttempts=1; await this.github.deleteRunner(matches[0].id,signal); }
    const readback=await this.github.listRunners(signal); const residue=readback.filter(r=>r.name===exact.runnerName).length;
    if (residue!==0) { await this.store.failIfCurrent(exact.operationId,States.Cleaning,'runnerLost'); throw new Error('registration-residue'); }
    if (!await this.store.compareExchange(exact.operationId,States.Cleaning,States.Closed)) throw new Error('close-state-ambiguous');
    return this.evidence.closed(exact, matches[0]?.id ?? null, deleteAttempts, residue);
  }
}

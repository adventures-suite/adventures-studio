import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const roleUuid = '927117fa-ab5d-42a2-b39e-762663171fa4';
const roleDefinitionId = `/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/providers/Microsoft.Authorization/roleDefinitions/${roleUuid}`;
const armId = /^\/subscriptions\/[0-9a-f-]{36}\/resourceGroups\/[A-Za-z0-9._()-]+\/providers\/[A-Za-z0-9.]+\/[A-Za-z0-9.]+\/[A-Za-z0-9._()-]+(?:\/[A-Za-z0-9.]+\/[A-Za-z0-9._()-]+)*$/i;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const sha256 = value => createHash('sha256').update(value).digest('hex');
const fail = code => { throw new Error(code); };
const exactKeys = (value, keys) => JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...keys].sort());
const same = (left, right) => left.toLowerCase() === right.toLowerCase();
function deriveType(id) {
  const segments=id.split('/'); const providerIndex=segments.findIndex(x=>x.toLowerCase()==='providers');
  if(providerIndex<0 || (segments.length-providerIndex-2)%2!==0) fail('catalog-id-pairs');
  return [segments[providerIndex+1],...segments.slice(providerIndex+2).filter((_,index)=>index%2===0)].join('/');
}

function readBound(path, expectedHash) {
  if (!/^[0-9a-f]{64}$/.test(expectedHash)) fail('checksum-format');
  const bytes = readFileSync(path);
  if (sha256(bytes) !== expectedHash) fail('checksum-mismatch');
  return JSON.parse(bytes);
}

export function validateCatalog(catalog) {
  if (!exactKeys(catalog, ['schemaVersion', 'subscriptionId', 'resourceGroupId', 'resources']) || catalog.schemaVersion !== 1 || catalog.resources?.length !== 23) fail('catalog-shape');
  if (catalog.subscriptionId !== '5ace9cdd-06d1-47d9-8214-1e7c756d076a' || catalog.resourceGroupId !== `/subscriptions/${catalog.subscriptionId}/resourceGroups/rg-adventures-suite-dev`) fail('catalog-scope');
  const ids = new Set(); const orders = new Set();
  for (const entry of catalog.resources) {
    if (!exactKeys(entry, ['id', 'type', 'parentId', 'cleanupParentId', 'dependencyOrder'])) fail('catalog-entry-shape');
    if (typeof entry.id !== 'string' || !armId.test(entry.id) || !entry.id.startsWith(`${catalog.resourceGroupId}/providers/`)) fail('catalog-id');
    if (typeof entry.type !== 'string' || !/^[A-Za-z0-9.]+\/[A-Za-z0-9.]+(?:\/[A-Za-z0-9.]+)*$/.test(entry.type)) fail('catalog-type');
    if (!same(deriveType(entry.id),entry.type)) fail('catalog-type-id-mismatch');
    if (ids.has(entry.id.toLowerCase()) || orders.has(entry.dependencyOrder) || !Number.isInteger(entry.dependencyOrder) || entry.dependencyOrder < 1 || entry.dependencyOrder > 23) fail('catalog-duplicate');
    ids.add(entry.id.toLowerCase()); orders.add(entry.dependencyOrder);
    if (entry.parentId !== null && (!armId.test(entry.parentId) || !entry.id.toLowerCase().startsWith(`${entry.parentId.toLowerCase()}/`))) fail('catalog-parent');
    if (!armId.test(entry.cleanupParentId) || !entry.id.toLowerCase().startsWith(entry.cleanupParentId.toLowerCase())) fail('catalog-cleanup-parent');
  }
  for (const entry of catalog.resources) {
    if (entry.parentId !== null && !ids.has(entry.parentId.toLowerCase()) && !entry.id.includes('/virtualNetworks/vnet-adventures-suite-dev/subnets/')) fail('catalog-parent-missing');
    if (!ids.has(entry.cleanupParentId.toLowerCase())) fail('catalog-cleanup-parent-missing');
  }
  return catalog;
}

export function validateInventory(catalog, inventory, { final = false } = {}) {
  validateCatalog(catalog);
  if (!exactKeys(inventory, ['schemaVersion', 'operationId', 'sourceSha', 'catalogSha256', 'entries', 'result']) || inventory.schemaVersion !== 1 || !/^broker-foundation-(inventory|residue)-[a-z0-9]{16,64}$/.test(inventory.operationId) || !/^[0-9a-f]{40}$/.test(inventory.sourceSha) || !/^[0-9a-f]{64}$/.test(inventory.catalogSha256) || inventory.entries?.length !== 23) fail('inventory-shape');
  const expected = new Map(catalog.resources.map(x => [x.id.toLowerCase(), x])); const seen = new Set();
  for (const entry of inventory.entries) {
    if (!exactKeys(entry, ['id', 'type', 'state']) || typeof entry.id !== 'string' || typeof entry.type !== 'string') fail('inventory-entry-shape');
    const key = entry.id.toLowerCase(); const wanted = expected.get(key);
    if (!wanted || seen.has(key)) fail('inventory-unknown-or-duplicate');
    seen.add(key);
    if (!same(entry.type, wanted.type)) fail('inventory-wrong-type');
    const allowed = wanted.type === 'Microsoft.KeyVault/vaults' ? ['VerifiedPresent', 'VerifiedAbsent', 'SoftDeletedRetained', 'Failure', 'Ambiguous'] : ['VerifiedPresent', 'VerifiedAbsent', 'Failure', 'Ambiguous'];
    if (!allowed.includes(entry.state) || entry.state === 'Failure' || entry.state === 'Ambiguous') fail('inventory-unverified');
    if (final && entry.state === 'VerifiedPresent') fail('inventory-residue');
    if (final && wanted.type === 'Microsoft.KeyVault/vaults' && entry.state !== 'SoftDeletedRetained' && entry.state !== 'VerifiedAbsent') fail('vault-disposition');
  }
  const states=new Map(inventory.entries.map(x=>[x.id.toLowerCase(),x.state]));
  for(const entry of catalog.resources){
    if(states.get(entry.id.toLowerCase())==='VerifiedPresent' && entry.parentId!==null && expected.has(entry.parentId.toLowerCase()) && states.get(entry.parentId.toLowerCase())!=='VerifiedPresent') fail('inventory-parent-absent');
  }
  if (seen.size !== 23 || inventory.result !== (final ? 'Clean' : 'Verified')) fail('inventory-incomplete');
  return inventory;
}

function guidBytes(value) {
  const hex = value.replaceAll('-', '');
  return Buffer.from(hex.match(/../g).map(x => Number.parseInt(x, 16)));
}
function formatUuid(bytes) {
  const hex = bytes.toString('hex');
  return `${hex.slice(0,8)}-${hex.slice(8,12)}-${hex.slice(12,16)}-${hex.slice(16,20)}-${hex.slice(20)}`;
}
export function deterministicAssignmentId(scope, principalId) {
  const namespace = guidBytes('11fb06fb-712d-4ddd-98c7-e71bbd588830');
  const digest = createHash('sha1').update(namespace).update(`${scope}-${principalId}-${roleDefinitionId}`).digest().subarray(0,16);
  digest[6] = (digest[6] & 0x0f) | 0x50; digest[8] = (digest[8] & 0x3f) | 0x80;
  return `${scope}/providers/Microsoft.Authorization/roleAssignments/${formatUuid(digest)}`;
}

export function expectedCleanupScopes(catalog, inventory) {
  validateInventory(catalog, inventory);
  const present = new Set(inventory.entries.filter(x => x.state === 'VerifiedPresent').map(x => x.id.toLowerCase()));
  return [...new Set(catalog.resources.filter(x => present.has(x.id.toLowerCase())).map(x => x.cleanupParentId))]
    .filter(id => present.has(id.toLowerCase()))
    .sort((a,b) => catalog.resources.find(x => same(x.cleanupParentId,a)).dependencyOrder - catalog.resources.find(x => same(x.cleanupParentId,b)).dependencyOrder);
}

export function validateAssignmentPlan(catalog, inventory, plan) {
  const scopes = expectedCleanupScopes(catalog, inventory);
  if (!exactKeys(plan, ['schemaVersion','operationId','catalogSha256','inventorySha256','cleanupPrincipalId','cleanupRoleDefinitionId','assignments']) || plan.schemaVersion !== 1 || !/^broker-foundation-assign-cleanup-[a-z0-9]{16,64}$/.test(plan.operationId) || !uuid.test(plan.cleanupPrincipalId) || !same(plan.cleanupRoleDefinitionId, roleDefinitionId) || !/^[0-9a-f]{64}$/.test(plan.catalogSha256) || !/^[0-9a-f]{64}$/.test(plan.inventorySha256) || !Array.isArray(plan.assignments)) fail('assignment-plan-shape');
  if (plan.assignments.length !== scopes.length) fail('assignment-plan-count');
  const seen = new Set();
  for (let index=0; index<scopes.length; index++) {
    const assignment = plan.assignments[index]; const scope = scopes[index];
    if (!exactKeys(assignment,['scope','principalId','roleDefinitionId','assignmentId']) || !same(assignment.scope,scope) || !same(assignment.principalId,plan.cleanupPrincipalId) || !same(assignment.roleDefinitionId,roleDefinitionId) || seen.has(assignment.scope.toLowerCase())) fail('assignment-plan-binding');
    if (same(assignment.scope,catalog.resourceGroupId) || /^\/subscriptions\/[0-9a-f-]{36}$/i.test(assignment.scope) || !catalog.resources.some(x => same(x.cleanupParentId, assignment.scope))) fail('assignment-plan-scope');
    if (!same(assignment.assignmentId,deterministicAssignmentId(scope,plan.cleanupPrincipalId))) fail('assignment-plan-id');
    seen.add(assignment.scope.toLowerCase());
  }
  return plan;
}

function classifyShow(result) {
  if (result.error || !Number.isInteger(result.status)) return 'failure';
  if (result.status === 0) return 'present';
  const bounded = String(result.stderr ?? '').slice(0,4096);
  const codes = [...bounded.matchAll(/\(([A-Za-z][A-Za-z0-9]{2,63})\)/g)].map(x=>x[1]);
  return codes.length === 1 && ['ResourceNotFound','NotFound'].includes(codes[0]) ? 'absent' : 'ambiguous';
}

export function collectInventory(catalog, operationId, sourceSha, catalogHash, spawn = spawnSync) {
  validateCatalog(catalog);
  const entries = catalog.resources.map(expected => {
    const result = spawn('az',['resource','show','--ids',expected.id,'--query','{id:id,type:type}','-o','json','--only-show-errors'],{encoding:'utf8',maxBuffer:8192});
    const classification = classifyShow(result);
    if (classification === 'absent') return {id:expected.id,type:expected.type,state:'VerifiedAbsent'};
    if (classification === 'failure') return {id:expected.id,type:expected.type,state:'Failure'};
    if (classification === 'ambiguous') return {id:expected.id,type:expected.type,state:'Ambiguous'};
    try {
      const actual=JSON.parse(result.stdout);
      return same(actual.id,expected.id) && same(actual.type,expected.type) ? {id:expected.id,type:expected.type,state:'VerifiedPresent'} : {id:expected.id,type:expected.type,state:'Ambiguous'};
    } catch { return {id:expected.id,type:expected.type,state:'Ambiguous'}; }
  });
  return {schemaVersion:1,operationId,sourceSha,catalogSha256:catalogHash,entries,result:entries.some(x=>x.state==='Failure')?'Failure':entries.some(x=>x.state==='Ambiguous')?'Ambiguous':'Verified'};
}

export function executeCleanup(catalog, inventory, plan, spawn = spawnSync, sleep = milliseconds => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)),0,0,milliseconds)) {
  validateAssignmentPlan(catalog,inventory,plan);
  const scopes = expectedCleanupScopes(catalog,inventory);
  for (const scope of scopes) {
    const before=spawn('az',['resource','show','--ids',scope,'--query','{id:id,type:type}','-o','json','--only-show-errors'],{encoding:'utf8',maxBuffer:8192});
    const beforeState=classifyShow(before);
    if (beforeState==='absent') continue;
    if (beforeState!=='present') fail('cleanup-predelete-ambiguous');
    let actual; try { actual=JSON.parse(before.stdout); } catch { fail('cleanup-predelete-malformed'); }
    const expected=catalog.resources.find(x=>same(x.id,scope));
    if (!expected || !same(actual.id,expected.id) || !same(actual.type,expected.type)) fail('cleanup-predelete-substitution');
    const deletion=spawn('az',['resource','delete','--ids',scope,'--only-show-errors'],{encoding:'utf8',maxBuffer:4096});
    if (deletion.status!==0) fail('cleanup-delete-failed');
    let absent=false;
    for(let attempt=0;attempt<30;attempt++) {
      const readback=spawn('az',['resource','show','--ids',scope,'--query','{id:id,type:type}','-o','json','--only-show-errors'],{encoding:'utf8',maxBuffer:8192});
      const state=classifyShow(readback);
      if(state==='absent'){absent=true;break;}
      if(state!=='present') fail('cleanup-poll-ambiguous');
      if(attempt<29)sleep(10000);
    }
    if(!absent) fail('cleanup-delete-timeout');
  }
  return {schemaVersion:1,operationId:plan.operationId,state:'CleanupComplete',deletedScopeCount:scopes.length,vaultDisposition:'SoftDeletedRetainedNoPurge'};
}

export function collectFinalResidue(catalog, priorInventory, operationId, sourceSha, catalogHash, spawn = spawnSync) {
  validateInventory(catalog,priorInventory);
  const evidence=collectInventory(catalog,operationId,sourceSha,catalogHash,spawn);
  const vault=catalog.resources.find(x=>x.type==='Microsoft.KeyVault/vaults');
  const priorVault=priorInventory.entries.find(x=>same(x.id,vault.id));
  const finalVault=evidence.entries.find(x=>same(x.id,vault.id));
  if(priorVault.state==='VerifiedPresent' && finalVault.state==='VerifiedAbsent') finalVault.state='SoftDeletedRetained';
  evidence.result=evidence.entries.some(x=>x.state==='Ambiguous')?'Ambiguous':evidence.entries.some(x=>x.state==='VerifiedPresent')?'Failure':'Clean';
  return evidence;
}

if (process.argv[1]?.endsWith('foundation-authority-policy.mjs')) {
  try {
    const [command, ...args] = process.argv.slice(2);
    if (command === 'inventory' && args.length === 5) {
      const [catalogPath,catalogHash,operationId,sourceSha,outputKind] = args;
      if (outputKind !== 'json' || !/^[0-9a-f]{40}$/.test(sourceSha) || !/^broker-foundation-inventory-[a-z0-9]{16,64}$/.test(operationId)) fail('inventory-arguments');
      const catalog=validateCatalog(readBound(catalogPath,catalogHash)); const evidence=collectInventory(catalog,operationId,sourceSha,catalogHash);
      process.stdout.write(`${JSON.stringify(evidence)}\n`); if(evidence.result!=='Verified') process.exitCode=1;
    } else if (command === 'validate-plan' && args.length === 6) {
      const [catalogPath,catalogHash,inventoryPath,inventoryHash,planPath,planHash]=args;
      const catalog=validateCatalog(readBound(catalogPath,catalogHash)); const inventory=readBound(inventoryPath,inventoryHash); const plan=readBound(planPath,planHash);
      if (inventory.catalogSha256!==catalogHash || plan.catalogSha256!==catalogHash || plan.inventorySha256!==inventoryHash) fail('cross-checksum-binding');
      validateAssignmentPlan(catalog,inventory,plan); process.stdout.write('{"result":"AssignmentPlanVerified"}\n');
    } else if (command === 'cleanup' && args.length === 6) {
      const [catalogPath,catalogHash,inventoryPath,inventoryHash,planPath,planHash]=args;
      const catalog=validateCatalog(readBound(catalogPath,catalogHash)); const inventory=readBound(inventoryPath,inventoryHash); const plan=readBound(planPath,planHash);
      if (inventory.catalogSha256!==catalogHash || plan.catalogSha256!==catalogHash || plan.inventorySha256!==inventoryHash) fail('cross-checksum-binding');
      process.stdout.write(`${JSON.stringify(executeCleanup(catalog,inventory,plan))}\n`);
    } else if (command === 'residue' && args.length === 8) {
      const [catalogPath,catalogHash,inventoryPath,inventoryHash,operationId,sourceSha,assignmentsPath,assignmentsHash]=args;
      const catalog=validateCatalog(readBound(catalogPath,catalogHash)); const inventory=readBound(inventoryPath,inventoryHash); const assignments=readBound(assignmentsPath,assignmentsHash);
      if(inventory.catalogSha256!==catalogHash || !exactKeys(assignments,['schemaVersion','roleAssignmentsRemoved','remainingAssignmentCount']) || assignments.schemaVersion!==1 || assignments.roleAssignmentsRemoved!==true || assignments.remainingAssignmentCount!==0) fail('assignment-residue');
      const evidence=collectFinalResidue(catalog,inventory,operationId,sourceSha,catalogHash); validateInventory(catalog,evidence,{final:true});
      process.stdout.write(`${JSON.stringify({...evidence,assignmentEvidenceSha256:assignmentsHash})}\n`);
    } else fail('command-arguments');
  } catch (error) { process.stderr.write(`${error.message}\n`); process.exitCode=1; }
}

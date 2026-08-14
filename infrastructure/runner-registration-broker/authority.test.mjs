import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import {
  collectFinalResidue, collectInventory, deterministicAssignmentId, executeCleanup,
  expectedCleanupScopes, validateAssignmentPlan, validateCatalog, validateInventory
} from './foundation-authority-policy.mjs';

const catalog=JSON.parse(readFileSync(new URL('./foundation-resource-catalog.json',import.meta.url)));
const catalogSha=createHash('sha256').update(readFileSync(new URL('./foundation-resource-catalog.json',import.meta.url))).digest('hex');
const principal='11111111-2222-4333-8444-555555555555';
const roleId=`/subscriptions/${catalog.subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/927117fa-ab5d-42a2-b39e-762663171fa4`;
const inventory=(present=[])=>({schemaVersion:1,operationId:'broker-foundation-inventory-0123456789abcdef',sourceSha:'a'.repeat(40),catalogSha256:catalogSha,entries:catalog.resources.map(x=>({id:x.id,type:x.type,state:present.includes(x.id)?'VerifiedPresent':'VerifiedAbsent'})),result:'Verified'});
const plan=value=>{const scopes=expectedCleanupScopes(catalog,value);return {schemaVersion:1,operationId:'broker-foundation-assign-cleanup-0123456789abcdef',catalogSha256:catalogSha,inventorySha256:'b'.repeat(64),cleanupPrincipalId:principal,cleanupRoleDefinitionId:roleId,assignments:scopes.map(scope=>({scope,principalId:principal,roleDefinitionId:roleId,assignmentId:deterministicAssignmentId(scope,principal)}))};};
const parents=()=>catalog.resources.filter(x=>x.id===x.cleanupParentId).map(x=>x.id);
const storageAccount=catalog.resources.find(x=>x.type==='Microsoft.Storage/storageAccounts');
const storageChildren=catalog.resources.filter(x=>x.type.startsWith('Microsoft.Storage/storageAccounts/'));
const absentResult=code=>({status:1,stdout:'',stderr:`(${code})`});
const inventoryWith=(responseForId,calls=[])=>collectInventory(catalog,'broker-foundation-inventory-0123456789abcdef','a'.repeat(40),catalogSha,(command,args)=>{const id=args[args.indexOf('--ids')+1];calls.push(id);return responseForId(id);});
const cliFixtures=JSON.parse(readFileSync(new URL('./fixtures/azure-cli-error-prefixes.json',import.meta.url)));

test('catalog binds the complete exact 23-resource graph and thirteen cleanup parents',()=>{assert.equal(validateCatalog(catalog).resources.length,23);assert.equal(parents().length,13);assert.deepEqual(catalog.resources.map(x=>x.dependencyOrder),Array.from({length:23},(_,i)=>i+1));});
test('empty, early, middle, and complete deployments derive only verified-present cleanup parents',()=>{const cleanupParents=parents();for(const count of [0,1,6,13]){const selected=new Set(cleanupParents.slice(0,count));const present=catalog.resources.filter(x=>selected.has(x.cleanupParentId)).map(x=>x.id);const value=inventory(present);validateInventory(catalog,value);assert.equal(expectedCleanupScopes(catalog,value).length,count);validateAssignmentPlan(catalog,value,plan(value));}});
test('inventory rejects unknown, duplicate, malformed, ambiguous, and wrong-type resources',()=>{const mutations=[v=>v.entries[0].id+='/other',v=>v.entries[1].id=v.entries[0].id,v=>v.entries[0].id='not-an-arm-id',v=>v.entries[0].state='Ambiguous',v=>v.entries[0].type='Microsoft.Network/virtualNetworks'];for(const mutate of mutations){const value=inventory();mutate(value);assert.throws(()=>validateInventory(catalog,value));}});
test('assignment policy rejects broader scopes, absent targets, duplicate, missing, additional, and mismatched bindings',()=>{const value=inventory(parents());const mutations=[p=>p.assignments[0].scope=catalog.resourceGroupId,p=>p.assignments[0].scope=`/subscriptions/${catalog.subscriptionId}`,p=>p.assignments[0].scope=catalog.resources.find(x=>x.id!==x.cleanupParentId).id,p=>p.assignments.push(p.assignments[0]),p=>p.assignments.pop(),p=>p.assignments[0].principalId='22222222-2222-4333-8444-555555555555',p=>p.assignments[0].roleDefinitionId=p.assignments[0].roleDefinitionId.replace('9271','9272'),p=>p.assignments[0].assignmentId=p.assignments[0].assignmentId.replace(/.$/,'0')];for(const mutate of mutations){const candidate=structuredClone(plan(value));mutate(candidate);assert.throws(()=>validateAssignmentPlan(catalog,value,candidate));}});
test('cleanup treats verified absence idempotently and deletes present parents in dependency order',()=>{const value=inventory(parents().slice(0,4));const calls=[];const deleted=new Set();const spawn=(command,args)=>{calls.push(args.join(' '));const id=args[args.indexOf('--ids')+1];if(args[1]==='delete'){deleted.add(id);return {status:0,stdout:'',stderr:''};}if(deleted.has(id))return {status:1,stdout:'',stderr:'(ResourceNotFound)'};const expected=catalog.resources.find(x=>x.id===id);return {status:0,stdout:JSON.stringify({id,type:expected.type}),stderr:''};};const result=executeCleanup(catalog,value,plan(value),spawn,()=>{});assert.equal(result.deletedScopeCount,4);assert.deepEqual(calls.filter(x=>x.includes('resource delete')).map(x=>x.split('--ids ')[1].split(' --')[0]),expectedCleanupScopes(catalog,value));});
test('cleanup fails closed on deletion failure, timeout, and substituted pre-delete evidence',()=>{const value=inventory([parents()[0]]);for(const mode of ['delete','timeout','substitute']){const spawn=(command,args)=>{if(args[1]==='delete')return {status:mode==='delete'?1:0,stdout:'',stderr:''};const id=args[args.indexOf('--ids')+1];return {status:0,stdout:JSON.stringify({id:mode==='substitute'?`${id}/other`:id,type:'Microsoft.Web/sites'}),stderr:''};};assert.throws(()=>executeCleanup(catalog,value,plan(value),spawn,()=>{}),mode==='delete'?/cleanup-delete-failed/:mode==='timeout'?/cleanup-delete-timeout/:/cleanup-predelete-substitution/);}});
test('final residue covers all 23 resources and retains soft-deleted Key Vault without purge',()=>{const value=inventory(parents());const spawn=()=>({status:1,stdout:'',stderr:'(ResourceNotFound)'});const final=collectFinalResidue(catalog,value,'broker-foundation-residue-0123456789abcdef','a'.repeat(40),catalogSha,spawn);assert.equal(final.entries.length,23);assert.equal(final.entries.find(x=>x.type==='Microsoft.KeyVault/vaults').state,'SoftDeletedRetained');assert.equal(final.result,'Clean');validateInventory(catalog,final,{final:true});});
test('complete residue and retained live resources fail final verification',()=>{const value=inventory(parents());const target=catalog.resources[0];const spawn=(command,args)=>{const id=args[args.indexOf('--ids')+1];return id===target.id?{status:0,stdout:JSON.stringify({id,type:target.type}),stderr:''}:{status:1,stdout:'',stderr:'(ResourceNotFound)'};};const final=collectFinalResidue(catalog,value,'broker-foundation-residue-0123456789abcdef','a'.repeat(40),catalogSha,spawn);assert.equal(final.result,'Failure');assert.throws(()=>validateInventory(catalog,final,{final:true}));});
test('inventory collection emits bounded ambiguous evidence rather than raw Azure errors',()=>{const huge=`(AuthorizationFailed)${'secret'.repeat(2000)}`;const result=collectInventory(catalog,'broker-foundation-inventory-0123456789abcdef','a'.repeat(40),catalogSha,()=>({status:1,stdout:'',stderr:huge}));assert.equal(result.result,'Ambiguous');assert.equal(JSON.stringify(result).includes('secret'),false);assert.equal(result.entries.every(x=>x.state==='Ambiguous'),true);});
test('inventory distinguishes bounded process failure without retaining raw evidence',()=>{const result=collectInventory(catalog,'broker-foundation-inventory-0123456789abcdef','a'.repeat(40),catalogSha,()=>({status:null,error:new Error('sensitive')}));assert.equal(result.result,'Failure');assert.equal(JSON.stringify(result).includes('sensitive'),false);assert.equal(result.entries.every(x=>x.state==='Failure'),true);});

test('absent Storage account proves the exact four-child ParentResourceNotFound chain absent in parent-first order',()=>{
  const calls=[];
  const result=inventoryWith(id=>storageChildren.some(x=>x.id===id)?absentResult('ParentResourceNotFound'):absentResult('ResourceNotFound'),calls);
  assert.equal(result.result,'Verified');
  assert.equal(result.entries.find(x=>x.id===storageAccount.id).state,'VerifiedAbsent');
  assert.deepEqual(storageChildren.map(x=>result.entries.find(entry=>entry.id===x.id).state),Array(4).fill('VerifiedAbsent'));
  for(const child of storageChildren) assert.ok(calls.indexOf(child.parentId)<calls.indexOf(child.id));
});

test('ParentResourceNotFound requires the exact catalog parent to be already conclusively absent',()=>{
  const child=storageChildren.find(x=>x.parentId===storageAccount.id);
  const cases=[
    {name:'present',parent:{status:0,stdout:JSON.stringify({id:storageAccount.id,type:storageAccount.type}),stderr:''}},
    {name:'ambiguous',parent:absentResult('AuthorizationFailed')},
    {name:'failed',parent:{status:null,error:new Error('process failure')}},
    {name:'unprocessed external parent',child:catalog.resources.find(x=>x.type==='Microsoft.Network/virtualNetworks/subnets')}
  ];
  for(const scenario of cases){
    const target=scenario.child??child;
    const result=inventoryWith(id=>id===storageAccount.id&&scenario.parent?scenario.parent:id===target.id?absentResult('ParentResourceNotFound'):absentResult('ResourceNotFound'));
    assert.equal(result.entries.find(x=>x.id===target.id).state,'Ambiguous',scenario.name);
  }
});

test('ParentResourceNotFound rejects substituted catalog relationships and wrong child identity or type',()=>{
  const child=storageChildren.find(x=>x.parentId===storageAccount.id);
  for(const field of ['id','type']){
    const result=inventoryWith(id=>id===child.id?{status:0,stdout:JSON.stringify({id:field==='id'?`${child.id}/other`:child.id,type:field==='type'?'Microsoft.Storage/storageAccounts':child.type}),stderr:''}:absentResult('ResourceNotFound'));
    assert.equal(result.entries.find(x=>x.id===child.id).state,'Ambiguous');
  }
  const substituted=structuredClone(catalog); const candidate=substituted.resources.find(x=>x.id===child.id); candidate.parentId=catalog.resources.find(x=>x.type==='Microsoft.KeyVault/vaults').id;
  assert.throws(()=>validateCatalog(substituted),/catalog-parent/);
});

test('ParentResourceNotFound rejects malformed, nested, competing, additional, and unexpected codes without raw evidence',()=>{
  const child=storageChildren.find(x=>x.parentId===storageAccount.id);
  const errors=[
    '(Parent Resource Not Found)',
    '{"error":{"code":"ParentResourceNotFound","innererror":{"code":"ResourceNotFound"}}}',
    '(ParentResourceNotFound) (ResourceNotFound)',
    '(ParentResourceNotFound) {"code":"ParentResourceNotFound"}',
    '(AuthorizationFailed)',
    `(ParentResourceNotFound)${'sensitive'.repeat(1000)}`
  ];
  for(const stderr of errors){
    const result=inventoryWith(id=>id===child.id?{status:1,stdout:'',stderr}:absentResult('ResourceNotFound'));
    assert.equal(result.entries.find(x=>x.id===child.id).state,'Ambiguous');
    assert.equal(JSON.stringify(result).includes('sensitive'),false);
  }
});

test('existing ResourceNotFound and NotFound codes remain conclusive absence signals',()=>{
  for(const code of ['ResourceNotFound','NotFound']){
    const result=inventoryWith(()=>absentResult(code));
    assert.equal(result.result,'Verified');
    assert.equal(result.entries.every(x=>x.state==='VerifiedAbsent'),true);
  }
});

test('exact live Azure CLI prefix fixtures preserve all three allowlisted absence classifications',()=>{
  assert.deepEqual(cliFixtures.map(x=>x.code),['ResourceNotFound','NotFound','ParentResourceNotFound']);
  for(const fixture of cliFixtures.slice(0,2)){
    const result=inventoryWith(()=>({status:3,stdout:'',stderr:fixture.stderr}));
    assert.equal(result.result,'Verified');
    assert.equal(result.entries.every(x=>x.state==='VerifiedAbsent'),true);
  }
  const parentMissing=cliFixtures.find(x=>x.code==='ResourceNotFound');
  const childMissing=cliFixtures.find(x=>x.code==='ParentResourceNotFound');
  const result=inventoryWith(id=>storageChildren.some(x=>x.id===id)?{status:3,stdout:'',stderr:childMissing.stderr}:{status:3,stdout:'',stderr:parentMissing.stderr});
  assert.equal(result.result,'Verified');
  assert.equal(storageChildren.every(x=>result.entries.find(entry=>entry.id===x.id).state==='VerifiedAbsent'),true);
});

test('Azure CLI prefix parsing rejects altered prefixes, whitespace variants, multiple records, contamination, and oversized evidence',()=>{
  const malformed=[
    ' ERROR: (ResourceNotFound) sanitized\n',
    'ERROR :(ResourceNotFound) sanitized\n',
    'ERROR:  (ResourceNotFound) sanitized\n',
    'Error: (ResourceNotFound) sanitized\n',
    'ERROR: (ResourceNotFound) sanitized\nERROR: (NotFound) second\n',
    `ERROR: (ResourceNotFound) ${'x'.repeat(4096)}`,
    'ERROR: (AuthorizationFailed) sanitized\n'
  ];
  for(const stderr of malformed){
    const result=inventoryWith(()=>({status:3,stdout:'',stderr}));
    assert.equal(result.result,'Ambiguous');
    assert.equal(result.entries.every(x=>x.state==='Ambiguous'),true);
  }
  for(const stdout of ['unexpected','\n']){
    const result=inventoryWith(()=>({status:3,stdout,stderr:'ERROR: (ResourceNotFound) sanitized\n'}));
    assert.equal(result.result,'Ambiguous');
  }
});

function runInventoryShell(mode) {
  const directory=mkdtempSync(join(tmpdir(),'broker-azure-cli-stub-'));
  try {
    const stubPath=join(directory,'az');
    writeFileSync(stubPath,`#!/usr/bin/env node
const { readFileSync } = require('node:fs');
const args = process.argv.slice(2);
const idIndex = args.indexOf('--ids');
if (args[0] !== 'resource' || args[1] !== 'show' || idIndex < 0 || args[idIndex + 1] === undefined) process.exit(64);
const catalog = JSON.parse(readFileSync(process.env.BROKER_TEST_CATALOG,'utf8'));
const entry = catalog.resources.find(candidate => candidate.id === args[idIndex + 1]);
if (!entry) process.exit(65);
const catalogIds = new Set(catalog.resources.map(candidate => candidate.id.toLowerCase()));
const mode = process.env.BROKER_TEST_MODE;
const child = entry.parentId !== null && catalogIds.has(entry.parentId.toLowerCase());
const code = mode === 'not-found' ? 'NotFound' : mode === 'parent-chain' && child ? 'ParentResourceNotFound' : 'ResourceNotFound';
if (mode === 'stdout-contamination') process.stdout.write('sensitive-stub-message');
if (mode === 'altered-prefix') process.stderr.write('ERROR :(ResourceNotFound) sensitive-stub-message\\n');
else if (mode === 'leading-whitespace') process.stderr.write(' ERROR: (ResourceNotFound) sensitive-stub-message\\n');
else if (mode === 'multiple-records') process.stderr.write('ERROR: (ResourceNotFound) sensitive-stub-message\\nERROR: (ResourceNotFound) second\\n');
else if (mode === 'competing-codes') process.stderr.write('ERROR: (ResourceNotFound) sensitive-stub-message (NotFound)\\n');
else if (mode === 'oversized') process.stderr.write('ERROR: (ResourceNotFound) ' + 'sensitive-stub-message'.repeat(300) + '\\n');
else if (mode === 'unexpected-code') process.stderr.write('ERROR: (AuthorizationFailed) sensitive-stub-message\\n');
else process.stderr.write('ERROR: (' + code + ') sensitive-stub-message\\n');
process.exit(3);
`,{mode:0o700});
    const runner=fileURLToPath(new URL('./inventory-foundation-residue.sh',import.meta.url));
    const catalogPath=fileURLToPath(new URL('./foundation-resource-catalog.json',import.meta.url));
    return spawnSync('bash',[runner,catalogPath,catalogSha,'broker-foundation-inventory-0123456789abcdef','a'.repeat(40)],{encoding:'utf8',env:{...process.env,PATH:`${directory}:${process.env.PATH}`,BROKER_TEST_CATALOG:catalogPath,BROKER_TEST_MODE:mode}});
  } finally { rmSync(directory,{recursive:true,force:true}); }
}

test('inventory shell runner accepts exact prefixed ResourceNotFound and NotFound through stubbed az',()=>{
  for(const mode of ['resource-not-found','not-found']){
    const execution=runInventoryShell(mode); assert.equal(execution.status,0,`${mode}: ${execution.stderr}`); assert.equal(execution.stderr,'');
    const evidence=JSON.parse(execution.stdout); assert.equal(evidence.result,'Verified'); assert.equal(evidence.entries.length,23); assert.equal(evidence.entries.every(entry=>entry.state==='VerifiedAbsent'),true);
    assert.equal(execution.stdout.includes('sensitive-stub-message'),false);
  }
});

test('inventory shell runner accepts exact child ParentResourceNotFound only after absent catalog parents',()=>{
  const execution=runInventoryShell('parent-chain'); assert.equal(execution.status,0,execution.stderr); assert.equal(execution.stderr,'');
  const evidence=JSON.parse(execution.stdout); assert.equal(evidence.result,'Verified'); assert.equal(evidence.entries.length,23); assert.equal(evidence.entries.every(entry=>entry.state==='VerifiedAbsent'),true);
  assert.equal(execution.stdout.includes('sensitive-stub-message'),false);
});

test('inventory shell runner rejects contaminated or malformed stubbed az evidence without raw-message leakage',()=>{
  for(const mode of ['stdout-contamination','altered-prefix','leading-whitespace','multiple-records','competing-codes','oversized','unexpected-code']){
    const execution=runInventoryShell(mode); assert.equal(execution.status,1,mode); assert.equal(execution.stderr,'',mode);
    const evidence=JSON.parse(execution.stdout); assert.equal(evidence.result,'Ambiguous',mode); assert.equal(evidence.entries.every(entry=>entry.state==='Ambiguous'),true,mode);
    assert.equal(execution.stdout.includes('sensitive-stub-message'),false,mode);
  }
});

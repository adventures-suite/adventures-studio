import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, mkdtempSync, readFileSync, rmSync, writeFileSync, mkdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const dir = new URL('.', import.meta.url).pathname;
const cleanup = join(dir, 'foundation-cleanup.sh');
const residue = join(dir, 'verify-foundation-residue.sh');
const subscription = '11111111-2222-4333-8444-555555555555';
const rg = `/subscriptions/${subscription}/resourceGroups/fictional-broker-test`;
const resources = [
  `${rg}/providers/Microsoft.Web/sites/func-adventures-suite-runner-broker-dev`,
  `${rg}/providers/Microsoft.Web/serverfarms/plan-adventures-suite-runner-broker-dev`,
  `${rg}/providers/Microsoft.Insights/components/appi-adventures-suite-runner-broker-dev`,
  `${rg}/providers/Microsoft.OperationalInsights/workspaces/log-adventures-suite-runner-broker-dev`,
  `${rg}/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-blob-dev`,
  `${rg}/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-queue-dev`,
  `${rg}/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-table-dev`,
  `${rg}/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-kv-dev`,
  `${rg}/providers/Microsoft.Storage/storageAccounts/stadvsrunnerbrokerdev`,
  `${rg}/providers/Microsoft.KeyVault/vaults/kv-adventures-runner-dev`,
  `${rg}/providers/Microsoft.Network/privateDnsZones/privatelink.queue.core.windows.net`,
  `${rg}/providers/Microsoft.Network/privateDnsZones/privatelink.table.core.windows.net`,
  `${rg}/providers/Microsoft.Network/virtualNetworks/fictional-vnet/subnets/snet-runner-broker-integration`
];
function fixture(mode='cleanup-success') {
  const root=mkdtempSync(join(tmpdir(),'broker-authority-')); const bin=join(root,'bin'); mkdirSync(bin);
  const binding=join(root,'binding.json'); writeFileSync(binding,JSON.stringify({operationId:'broker-foundation-cleanup-0123456789abcdef',sourceSha:'a'.repeat(40),subscriptionId:subscription,resourceGroupId:rg,resources}));
  const checksum=createHash('sha256').update(readFileSync(binding)).digest('hex'); const log=join(root,'az.log'); const count=join(root,'count');
  const fake=`#!/bin/sh\nprintf '%s\\n' "$*" >> "$FAKE_AZ_LOG"\ncase "$FAKE_AZ_MODE" in\n cleanup-success) exit 0 ;;\n cleanup-fail) if [ "$3" = resource ] && [ "$4" = delete ]; then n=$(cat "$FAKE_AZ_COUNT" 2>/dev/null || printf 0); n=$((n+1)); printf '%s' "$n" > "$FAKE_AZ_COUNT"; [ "$n" -eq 3 ] && exit 1; fi; exit 0 ;;\n residue-absent) exit 1 ;;\n residue-present) case "$*" in *func-adventures-suite-runner-broker-dev*) exit 0;; *) exit 1;; esac ;;\nesac\nexit 1\n`;
  writeFileSync(join(bin,'az'),fake,{mode:0o700}); chmodSync(join(bin,'az'),0o700);
  const assignments=join(root,'assignments.json'); writeFileSync(assignments,JSON.stringify({schemaVersion:1,roleAssignmentsRemoved:true,remainingAssignmentCount:0})); const assignmentsSha=()=>createHash('sha256').update(readFileSync(assignments)).digest('hex');
  return {root,binding,checksum,log,assignments,assignmentsSha,env:{...process.env,PATH:`${bin}:/usr/bin:/bin`,FAKE_AZ_MODE:mode,FAKE_AZ_LOG:log,FAKE_AZ_COUNT:count},dispose:()=>rmSync(root,{recursive:true,force:true})};
}
test('cleanup issues the exact ordered one-shot resource operations',()=>{const f=fixture();try{const r=spawnSync('bash',[cleanup,f.binding,f.checksum],{env:f.env,encoding:'utf8'});assert.equal(r.status,0,r.stderr);const lines=readFileSync(f.log,'utf8').trim().split('\n');assert.equal(lines.length,26);assert.equal(lines.filter(x=>x.includes(' resource delete ')).length,13);for(let i=0;i<13;i++)assert.ok(lines[i*2+1].endsWith(`resource delete --ids ${resources[i]}`));}finally{f.dispose();}});
test('cleanup stops after the first delete failure without retry',()=>{const f=fixture('cleanup-fail');try{const r=spawnSync('bash',[cleanup,f.binding,f.checksum],{env:f.env,encoding:'utf8'});assert.notEqual(r.status,0);const lines=readFileSync(f.log,'utf8').trim().split('\n');assert.equal(lines.filter(x=>x.includes(' resource delete ')).length,3);}finally{f.dispose();}});
test('cleanup rejects checksum and resource substitution before Azure',()=>{for(const mutate of ['checksum','resource']){const f=fixture();try{if(mutate==='resource'){const value=JSON.parse(readFileSync(f.binding));value.resources[0]+='/extra';writeFileSync(f.binding,JSON.stringify(value));f.checksum=createHash('sha256').update(readFileSync(f.binding)).digest('hex');}const r=spawnSync('bash',[cleanup,f.binding,mutate==='checksum'?'0'.repeat(64):f.checksum],{env:f.env,encoding:'utf8'});assert.equal(r.status,1);assert.equal(readFileSync(f.log,{encoding:'utf8',flag:'a+'}), '');}finally{f.dispose();}}});
test('independent residue readback accepts only complete absence and zero assignments',()=>{const f=fixture('residue-absent');try{const r=spawnSync('bash',[residue,f.binding,f.checksum,'2026-08-14T12:00:00Z',f.assignments,f.assignmentsSha()],{env:f.env,encoding:'utf8'});assert.equal(r.status,0,r.stderr);assert.match(r.stdout,/"result":"Clean"/);assert.match(r.stdout,/"vaultDisposition":"SoftDeletedRetained"/);}finally{f.dispose();}});
test('residue or assignment ambiguity fails closed',()=>{for(const mode of ['residue-present','residue-absent']){const f=fixture(mode);try{if(mode==='residue-absent')writeFileSync(f.assignments,JSON.stringify({schemaVersion:1,roleAssignmentsRemoved:false,remainingAssignmentCount:1}));const r=spawnSync('bash',[residue,f.binding,f.checksum,'2026-08-14T12:00:00Z',f.assignments,f.assignmentsSha()],{env:f.env,encoding:'utf8'});assert.equal(r.status,1);}finally{f.dispose();}}});

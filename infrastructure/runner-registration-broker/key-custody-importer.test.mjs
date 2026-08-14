import test from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { Readable } from 'node:stream';
import { spawn, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { assertAnonymousDescriptor, assertBoundedEvidence, DestinationContentType, DestinationSecretName, importFictionalOrFutureKey } from './key-custody-importer.mjs';

const vaultId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.KeyVault/vaults/kv-adventures-runner-dev';
const startedUtc = '2026-08-13T12:00:00.000Z';
const completedUtc = '2026-08-13T12:00:01.000Z';
function fictionalPem() { return generateKeyPairSync('rsa', { modulusLength: 2048, privateKeyEncoding: { format: 'pem', type: 'pkcs8' }, publicKeyEncoding: { format: 'pem', type: 'spki' } }).privateKey; }
function args(overrides={}) { return { input: Readable.from([Buffer.from(fictionalPem())]), operationId:'broker-key-0123456789abcdef', vaultId, githubKeyId:'1234567', startedUtc, now:()=>completedUtc, ...overrides }; }

test('fictional key import emits only bounded immutable metadata', async()=>{let observed;const evidence=await importFictionalOrFutureKey(args({secretClient:{async setSecret(name,value,options){observed={name,value,options};return {id:'https://kv-adventures-runner-dev.vault.azure.net/secrets/github-app-4590229-private-key/0123456789abcdef0123456789abcdef'};}}}));assert.equal(observed.name,DestinationSecretName);assert.equal(observed.options.contentType,DestinationContentType);assert.match(observed.value,/BEGIN PRIVATE/);assertBoundedEvidence(evidence);assert.equal(JSON.stringify(evidence).includes('PRIVATE KEY'),false);assert.match(evidence.publicKeyFingerprint,/^SHA256:/);});
test('versionless or wrong secret readback fails closed',async()=>{for(const id of ['https://kv-adventures-runner-dev.vault.azure.net/secrets/github-app-4590229-private-key','https://kv-adventures-runner-dev.vault.azure.net/secrets/other/0123456789abcdef0123456789abcdef'])await assert.rejects(importFictionalOrFutureKey(args({secretClient:{async setSecret(){return {id};}}})),/immutable-version-readback/);});
test('malformed weak and oversized material fail before import',async()=>{let calls=0;const secretClient={async setSecret(){calls++;}};await assert.rejects(importFictionalOrFutureKey(args({secretClient,input:Readable.from(['not-a-key'])})),/pkcs8-rsa-required/);await assert.rejects(importFictionalOrFutureKey(args({secretClient,input:Readable.from([Buffer.alloc(16385)])})),/pem-size-bound/);assert.equal(calls,0);});
test('cancellation remains distinct and never imports',async()=>{const controller=new AbortController();controller.abort();let calls=0;await assert.rejects(importFictionalOrFutureKey(args({signal:controller.signal,secretClient:{async setSecret(){calls++;}}})),e=>e.name==='AbortError');assert.equal(calls,0);});
test('wrong operation vault key and timestamp bindings fail before import',async()=>{const secretClient={async setSecret(){throw new Error('should-not-run');}};for(const patch of [{operationId:'wrong'},{vaultId:'/wrong'},{githubKeyId:'x'},{startedUtc:'not-time'}])await assert.rejects(importFictionalOrFutureKey(args({secretClient,...patch})));});
test('descriptor gate accepts only inherited anonymous pipe or socket',()=>{assert.doesNotThrow(()=>assertAnonymousDescriptor(3,{isFIFO:()=>true,isSocket:()=>false}));assert.throws(()=>assertAnonymousDescriptor(0,{isFIFO:()=>true,isSocket:()=>false}),/nonpersistent/);assert.throws(()=>assertAnonymousDescriptor(3,{isFIFO:()=>false,isSocket:()=>false}),/nonpersistent/);});
test('failure evidence contains only a stable code',async()=>{const secretClient={async setSecret(){const error=new Error('sensitive response body');error.code='service-failure';throw error;}};await assert.rejects(importFictionalOrFutureKey(args({secretClient})),e=>e.code==='service-failure');});
test('source and aggregate buffers are zeroized after success',async()=>{const source=Buffer.from(fictionalPem());await importFictionalOrFutureKey(args({input:Readable.from([source]),secretClient:{async setSecret(){return {id:'https://kv-adventures-runner-dev.vault.azure.net/secrets/github-app-4590229-private-key/0123456789abcdef0123456789abcdef'};}}}));assert.equal(source.every(byte=>byte===0),true);});

const sessionScript = new URL('./key-custody-session.sh', import.meta.url).pathname;
function executable(path, body) { writeFileSync(path, body, {mode:0o700}); chmodSync(path,0o700); }
function lifecycle(mode='success') {
  const root=mkdtempSync('/private/tmp/broker-key-lifecycle-');
  const fakeBin=join(root,'bin');mkdirSync(fakeBin);
  const operationId='broker-key-0123456789abcdef';
  const mountPath=join(root,`adventures-suite-key-custody-${operationId}`);mkdirSync(mountPath);
  const pemPath=join(mountPath,'adventures-suite-runner-broker-dev.123.private-key.pem');writeFileSync(pemPath,fictionalPem(),{mode:0o600});
  const state=join(root,'mounted');writeFileSync(state,'mounted');
  const importStarted=join(root,'import-started');
  const unmounted=join(root,'unmounted'), unmountFailure=join(root,'unmount-failure'), residue=join(root,'residue');
  executable(join(fakeBin,'mount'),`#!/bin/sh\n[ -f "$CUSTODY_STATE" ] && printf '/dev/disk9 on %s (apfs, local)\\n' "$KEY_CUSTODY_TEST_MOUNT_PATH"\n`);
  executable(join(fakeBin,'diskutil'),`#!/bin/sh\nif [ "$1" = info ]; then printf '   Device Node: %s\\n   Mount Point: %s\\n' /dev/disk9 "$KEY_CUSTODY_TEST_MOUNT_PATH"; exit 0; fi\nif [ -f "$CUSTODY_UNMOUNT_FAILURE" ]; then exit 1; fi\nrm -f "$CUSTODY_STATE"; rmdir "$KEY_CUSTODY_TEST_MOUNT_PATH" 2>/dev/null || true; : > "$CUSTODY_UNMOUNTED"\n`);
  executable(join(fakeBin,'hdiutil'),'#!/bin/sh\nexit 0\n');
  executable(join(fakeBin,'stat'),'#!/bin/sh\n/usr/bin/stat -c %s "$3" 2>/dev/null || /usr/bin/stat -f %z "$3"\n');
  executable(join(fakeBin,'pgrep'),'#!/bin/sh\n[ -f "$CUSTODY_RESIDUE" ]\n');
  executable(join(fakeBin,'node'),`#!/bin/sh\ncat <&3 >/dev/null\n: > "$CUSTODY_IMPORT_STARTED"\ncase "$CUSTODY_IMPORT_MODE" in\n  interrupt) while :; do sleep 1; done ;;\n  success) printf '{"fictional":true}\\n' ;;\n  timeout) printf 'key-import-timeout\\n' >&2; exit 1 ;;\n  *) printf 'key-import-service-failure\\n' >&2; exit 1 ;;\nesac\n`);
  if(mode==='unmount-failure')writeFileSync(unmountFailure,'1');
  if(mode==='residue')writeFileSync(residue,'1');
  const env={...process.env,PATH:`${fakeBin}:/usr/bin:/bin`,KEY_CUSTODY_TEST_MODE:'fictional',KEY_CUSTODY_TEST_MOUNT_ROOT:root,KEY_CUSTODY_TEST_MOUNT_PATH:mountPath,CUSTODY_STATE:state,CUSTODY_UNMOUNTED:unmounted,CUSTODY_UNMOUNT_FAILURE:unmountFailure,CUSTODY_RESIDUE:residue,CUSTODY_IMPORT_MODE:mode,CUSTODY_IMPORT_STARTED:importStarted};
  const cli=[operationId,'import',pemPath,'--operation-id',operationId,'--vault-id',vaultId,'--github-key-id','1234567','--importer-client-id','01234567-89ab-4def-8123-456789abcdef','--started-utc',startedUtc];
  return {root,pemPath,mountPath,unmounted,importStarted,env,cli,cleanup:()=>rmSync(root,{recursive:true,force:true})};
}
function runLifecycle(mode, mutate=x=>x) { const fixture=lifecycle(mode);try{return {fixture,result:spawnSync('bash',[sessionScript,...mutate([...fixture.cli])],{env:fixture.env,encoding:'utf8',timeout:5000})};}catch(error){fixture.cleanup();throw error;} }

test('real custody session succeeds then overwrites unmounts and proves absence',()=>{const {fixture,result}=runLifecycle('success');try{assert.equal(result.status,0,result.stderr);assert.equal(existsSync(fixture.pemPath),false);assert.equal(existsSync(fixture.mountPath),false);assert.equal(existsSync(fixture.unmounted),true);assert.match(result.stdout,/"state":"Absent"/);}finally{fixture.cleanup();}});
test('real custody session importer failure and timeout still clean completely',()=>{for(const mode of ['failure','timeout']){const {fixture,result}=runLifecycle(mode);try{assert.equal(result.status,1);assert.match(result.stderr,mode==='timeout'?/key-import-timeout/:/key-import-service-failure/);assert.match(result.stderr,/key-import-failed/);assert.equal(existsSync(fixture.pemPath),false);assert.equal(existsSync(fixture.mountPath),false);}finally{fixture.cleanup();}}});
test('real custody session rejects missing duplicate malformed and additional arguments and cleans',()=>{const mutations=[x=>x.slice(0,-2),x=>[...x.slice(0,-2),'--operation-id','duplicate'],x=>[...x.slice(0,-2),'--unknown','value'],x=>[...x,'--extra','value']];for(const mutate of mutations){const {fixture,result}=runLifecycle('success',mutate);try{assert.equal(result.status,1);assert.match(result.stderr,/closed-import-arguments/);assert.equal(existsSync(fixture.pemPath),false);assert.equal(existsSync(fixture.mountPath),false);}finally{fixture.cleanup();}}});
test('real custody session interruption cleans and returns bounded failure',async()=>{const fixture=lifecycle('interrupt');try{const child=spawn('bash',[sessionScript,...fixture.cli],{env:fixture.env,stdio:['ignore','pipe','pipe']});let stderr='';child.stderr.setEncoding('utf8');child.stderr.on('data',x=>stderr+=x);for(let attempt=0;attempt<50&&!existsSync(fixture.importStarted);attempt++)await new Promise(resolve=>setTimeout(resolve,20));assert.equal(existsSync(fixture.importStarted),true);child.kill('SIGTERM');const status=await new Promise(resolve=>child.once('exit',resolve));assert.notEqual(status,0);assert.match(stderr,/key-import-interrupted/);assert.equal(existsSync(fixture.pemPath),false);assert.equal(existsSync(fixture.mountPath),false);}finally{fixture.cleanup();}});
test('real custody session fails boundedly on unmount failure or detected residue',()=>{for(const mode of ['unmount-failure','residue']){const {fixture,result}=runLifecycle(mode);try{assert.equal(result.status,1);assert.match(result.stderr,/custody-cleanup-failed/);assert.equal(existsSync(fixture.pemPath),false);}finally{fixture.cleanup();}}});

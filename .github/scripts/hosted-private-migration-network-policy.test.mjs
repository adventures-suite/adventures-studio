import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { validateParameters, validateTemplate } from './hosted-private-migration-network-policy.mjs';

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const valid = JSON.parse(execFileSync('az', [
  'bicep', 'build', '--file',
  path.join(repositoryRoot, 'infrastructure/github-hosted-private-migration-network/main.bicep'),
  '--stdout',
], { encoding: 'utf8' }));
const compiledParameters = JSON.parse(execFileSync('az', [
  'bicep', 'build-params', '--file',
  path.join(repositoryRoot, 'infrastructure/github-hosted-private-migration-network/main.dev.bicepparam'),
  '--stdout', '--no-restore',
], { encoding: 'utf8' }));
const validParameters = JSON.parse(compiledParameters.parametersJson);
const clone = () => structuredClone(valid);
const reject = (mutate, pattern) => {
  const candidate = clone();
  mutate(candidate);
  assert.throws(() => validateTemplate(candidate), pattern);
};

test('accepts the exact reviewed hosted-runner network', () => assert.equal(validateTemplate(clone()), true));
test('accepts the exact reviewed parameter artifact', () => assert.equal(validateParameters(structuredClone(validParameters)), true));
test('rejects parameter additions', () => {
  const candidate = structuredClone(validParameters);
  candidate.parameters.extra = { value: 'forbidden' };
  assert.throws(() => validateParameters(candidate), /parameter_set_invalid/);
});
test('rejects parameter drift', () => {
  const candidate = structuredClone(validParameters);
  candidate.parameters.githubBusinessId.value = '1';
  assert.throws(() => validateParameters(candidate), /parameter_value_invalid/);
});
test('rejects CIDR drift', () => reject((x) => { x.variables.subnetAddressPrefix = '10.40.4.0/27'; }, /network_address_invalid/));
test('rejects VNet drift', () => reject((x) => { x.parameters.virtualNetworkName.defaultValue = 'lookalike'; }, /parameter_binding_invalid/));
test('rejects missing delegation', () => reject((x) => { x.resources.find((r) => r.type.toLowerCase().endsWith('/subnets')).properties.delegations = []; }, /subnet_delegation_invalid/));
test('rejects business ID drift', () => reject((x) => { x.parameters.githubBusinessId.defaultValue = '1'; }, /parameter_binding_invalid/));
const rules = (template) => template.resources.find((r) => r.type.toLowerCase().endsWith('networksecuritygroups')).properties.securityRules;
test('rejects any AzurePlatformDNS allow rule', () => reject((x) => { rules(x).find((r) => r.name === 'AllowHttpsOutbound').properties.destinationAddressPrefix = 'AzurePlatformDNS'; }, /dns_rule_forbidden/));
test('rejects an explicit DNS rule', () => reject((x) => { const rule = rules(x).find((r) => r.name === 'AllowHttpsOutbound').properties; rule.destinationPortRange = '53'; rule.destinationAddressPrefix = '168.63.129.16/32'; }, /dns_rule_forbidden/));
test('rejects removal of private SQL access', () => reject((x) => { x.resources.find((r) => r.type.toLowerCase().endsWith('networksecuritygroups')).properties.securityRules = rules(x).filter((r) => r.name !== 'AllowPrivateSqlOutbound'); }, /nsg_rules_invalid/));
test('rejects removal of HTTPS access', () => reject((x) => { x.resources.find((r) => r.type.toLowerCase().endsWith('networksecuritygroups')).properties.securityRules = rules(x).filter((r) => r.name !== 'AllowHttpsOutbound'); }, /nsg_rules_invalid/));
test('rejects weakened inbound denial', () => reject((x) => { rules(x).find((r) => r.name === 'DenyAllInbound').properties.access = 'Allow'; }, /nsg_rule_invalid/));
test('rejects weakened remaining-outbound denial', () => reject((x) => { rules(x).find((r) => r.name === 'DenyAllOutbound').properties.destinationAddressPrefix = 'VirtualNetwork'; }, /nsg_rule_invalid/));
test('rejects a public IP resource', () => reject((x) => { x.resources.push({ type: 'Microsoft.Network/publicIPAddresses', name: 'forbidden' }); }, /resource_set_invalid|prohibited_resource/));
test('rejects output drift', () => reject((x) => { delete x.outputs.githubNetworkConfigurationId; }, /output_contract_invalid/));
test('rejects unrelated resources', () => reject((x) => { x.resources.push({ type: 'Microsoft.Storage/storageAccounts', name: 'extra' }); }, /resource_set_invalid/));

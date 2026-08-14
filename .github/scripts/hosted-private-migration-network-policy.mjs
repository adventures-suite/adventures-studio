import fs from 'node:fs';

const expected = Object.freeze({
  businessId: '316268438',
  location: 'westus2',
  networkSettingsName: 'private-sql-migration-vnet',
  nsgName: 'nsg-github-private-sql-migration',
  sqlAddress: '10.40.1.4/32',
  subnetCidr: '10.40.3.0/27',
  subnetName: 'snet-github-private-sql-migration',
  vnetName: 'vnet-adventures-suite-dev',
});

export function validateTemplate(template) {
  if (!template || typeof template !== 'object' || Array.isArray(template)) throw new Error('template_invalid');
  const resources = template.resources;
  if (!Array.isArray(resources) || resources.length !== 3) throw new Error('resource_set_invalid');
  const byType = new Map(resources.map((resource) => [String(resource.type).toLowerCase(), resource]));
  if (byType.size !== 3) throw new Error('resource_type_duplicate');
  const nsg = byType.get('microsoft.network/networksecuritygroups');
  const subnet = byType.get('microsoft.network/virtualnetworks/subnets');
  const settings = byType.get('github.network/networksettings');
  if (!nsg || !subnet || !settings) throw new Error('resource_type_invalid');
  if (settings.apiVersion !== '2024-04-02') throw new Error('network_settings_api_invalid');

  const source = JSON.stringify(template);
  for (const forbidden of ['publicIPAddresses', 'natGateways', 'azureFirewalls', 'virtualMachines']) {
    if (source.toLowerCase().includes(forbidden.toLowerCase())) throw new Error('prohibited_resource');
  }
  const expectedOutputs = {
    virtualNetworkResourceId: "[resourceId('Microsoft.Network/virtualNetworks', parameters('virtualNetworkName'))]",
    subnetResourceId: "[resourceId('Microsoft.Network/virtualNetworks/subnets', parameters('virtualNetworkName'), parameters('subnetName'))]",
    networkSecurityGroupResourceId: "[resourceId('Microsoft.Network/networkSecurityGroups', parameters('networkSecurityGroupName'))]",
    networkSettingsResourceId: "[resourceId('GitHub.Network/networkSettings', parameters('networkSettingsName'))]",
    githubNetworkConfigurationId: "[reference(resourceId('GitHub.Network/networkSettings', parameters('networkSettingsName')), '2024-04-02', 'full').tags.GitHubId]",
    githubNetworkConfigurationName: "[parameters('networkSettingsName')]",
  };
  if (Object.keys(template.outputs ?? {}).sort().join('\n') !== Object.keys(expectedOutputs).sort().join('\n')) throw new Error('output_contract_invalid');
  for (const [output, value] of Object.entries(expectedOutputs)) {
    if (template.outputs[output].type !== 'string' || template.outputs[output].value !== value) throw new Error('output_contract_invalid');
  }

  const params = template.parameters ?? {};
  const defaults = Object.fromEntries(Object.entries(params).map(([key, value]) => [key, value.defaultValue]));
  if (defaults.location !== expected.location || defaults.virtualNetworkName !== expected.vnetName || defaults.subnetName !== expected.subnetName || defaults.networkSecurityGroupName !== expected.nsgName || defaults.networkSettingsName !== expected.networkSettingsName || defaults.githubBusinessId !== expected.businessId) throw new Error('parameter_binding_invalid');
  if (template.variables?.subnetAddressPrefix !== expected.subnetCidr || template.variables?.sqlPrivateEndpointAddress !== expected.sqlAddress) throw new Error('network_address_invalid');

  const subnetText = JSON.stringify(subnet);
  if (subnet.properties?.addressPrefix !== "[variables('subnetAddressPrefix')]") throw new Error('subnet_cidr_invalid');
  if (!subnetText.includes('GitHub.Network/networkSettings')) throw new Error('subnet_delegation_invalid');
  if (subnet.name !== "[format('{0}/{1}', parameters('virtualNetworkName'), parameters('subnetName'))]") throw new Error('vnet_binding_invalid');
  if (subnet.properties?.privateEndpointNetworkPolicies !== 'Enabled' || subnet.properties?.privateLinkServiceNetworkPolicies !== 'Enabled') throw new Error('subnet_policy_invalid');
  if (settings.name !== "[parameters('networkSettingsName')]" || settings.properties?.businessId !== "[parameters('githubBusinessId')]" || settings.properties?.subnetId !== "[resourceId('Microsoft.Network/virtualNetworks/subnets', parameters('virtualNetworkName'), parameters('subnetName'))]") throw new Error('network_settings_binding_invalid');

  const rules = nsg.properties?.securityRules;
  if (!Array.isArray(rules) || rules.length !== 6) throw new Error('nsg_rules_invalid');
  const names = rules.map((rule) => rule.name);
  if (new Set(names).size !== names.length) throw new Error('nsg_rule_duplicate');
  const exactRules = new Map(rules.map((rule) => [rule.name, rule.properties]));
  const required = {
    DenyAllInbound: ['*', '*', '*', 'Deny', 'Inbound', 100],
    AllowAzureDnsOutboundUdp: ['Udp', '53', 'AzurePlatformDNS', 'Allow', 'Outbound', 200],
    AllowAzureDnsOutboundTcp: ['Tcp', '53', 'AzurePlatformDNS', 'Allow', 'Outbound', 210],
    AllowPrivateSqlOutbound: ['Tcp', '1433', "[variables('sqlPrivateEndpointAddress')]", 'Allow', 'Outbound', 220],
    AllowHttpsOutbound: ['Tcp', '443', 'Internet', 'Allow', 'Outbound', 230],
    DenyAllOutbound: ['*', '*', '*', 'Deny', 'Outbound', 4000],
  };
  for (const [name, values] of Object.entries(required)) {
    const rule = exactRules.get(name);
    if (!rule || [rule.protocol, rule.destinationPortRange, rule.destinationAddressPrefix, rule.access, rule.direction, rule.priority].some((value, index) => value !== values[index])) throw new Error('nsg_rule_invalid');
  }
  return true;
}

export function validateParameters(document) {
  if (!document || typeof document !== 'object' || Array.isArray(document)) throw new Error('parameters_invalid');
  const parameters = document.parameters;
  const exact = {
    location: expected.location,
    virtualNetworkName: expected.vnetName,
    subnetName: expected.subnetName,
    networkSecurityGroupName: expected.nsgName,
    networkSettingsName: expected.networkSettingsName,
    githubBusinessId: expected.businessId,
  };
  if (!parameters || Object.keys(parameters).sort().join('\n') !== Object.keys(exact).sort().join('\n')) throw new Error('parameter_set_invalid');
  for (const [name, value] of Object.entries(exact)) {
    const record = parameters[name];
    if (!record || Object.keys(record).length !== 1 || record.value !== value) throw new Error('parameter_value_invalid');
  }
  return true;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  if (process.argv.length !== 4) throw new Error('usage');
  validateTemplate(JSON.parse(fs.readFileSync(process.argv[2], 'utf8')));
  validateParameters(JSON.parse(fs.readFileSync(process.argv[3], 'utf8')));
  process.stdout.write('hosted_private_migration_network_policy_valid\n');
}

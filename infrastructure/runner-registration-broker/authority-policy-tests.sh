#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"; dir="$root/infrastructure/runner-registration-broker"; workflow="$root/.github/workflows/broker-foundation-authority.yml"
require(){ grep -Fq -- "$1" "$2"; }; reject(){ ! grep -Fq -- "$1" "$2"; }

jq -e '.identities.provisioner=="id-adventures-suite-runner-broker-foundation-deployer-dev" and .identities.cleanup=="id-adventures-suite-runner-broker-foundation-cleanup-dev" and .identities.residueReader=="id-adventures-suite-runner-broker-foundation-residue-reader-dev" and .roles.provisioner=="36895920-b36b-4b0c-8a6a-6762164de71e" and .roles.cleanup=="927117fa-ab5d-42a2-b39e-762663171fa4" and .roles.residueReader=="eff3d13d-aeac-4b96-94f8-9c03a1ceee69" and .liveBindings=="required-checksum-bound-input-with-no-default" and .cleanupAssignmentModel=="exact-resource-id-only" and .automaticRetry==false and .automaticRollback==false' "$dir/authority-contract.json" >/dev/null
jq -e '.roleUuid=="36895920-b36b-4b0c-8a6a-6762164de71e" and ([.actions[]|select(endswith("/delete"))]|length)==0 and (.dataActions|length)==0' "$dir/provisioner-role-actions.json" >/dev/null
jq -e '.roleUuid=="927117fa-ab5d-42a2-b39e-762663171fa4" and ([.actions[]|select(endswith("/write"))]|length)==0 and ([.actions[]|select(endswith("/delete"))]|length)>0 and (.dataActions|length)==0' "$dir/cleanup-role-actions.json" >/dev/null
jq -e '.roleUuid=="eff3d13d-aeac-4b96-94f8-9c03a1ceee69" and ([.actions[]|select(endswith("/write") or endswith("/delete"))]|length)==0 and (.dataActions|length)==0' "$dir/residue-reader-role-actions.json" >/dev/null
jq -e '.additionalProperties==false and .properties.vaultDisposition.const=="SoftDeletedRetained" and .properties.roleAssignmentsRemoved.const==true' "$dir/residue-evidence.schema.json" >/dev/null
for catalog in "$dir"/*-role-actions.json; do reject 'Microsoft.Authorization/' "$catalog"; reject 'Microsoft.ManagedIdentity/' "$catalog"; reject 'listKeys' "$catalog"; reject 'purge' "$catalog"; reject 'secrets/' "$catalog"; done
require 'exact-resource-id-only' "$dir/authority-contract.json"; require 'Exact dependency order' "$dir/foundation-cleanup.sh"; reject '|| true' "$dir/foundation-cleanup.sh"; reject '--force' "$dir/foundation-cleanup.sh"
require 'roleAssignmentsRemoved' "$dir/verify-foundation-residue.sh"; require 'SoftDeletedRetained' "$dir/verify-foundation-residue.sh"
require 'environment: database-development' "$workflow"; require 'workflow_dispatch:' "$workflow"; require 'Deliberately stop before Azure login or authority mutation' "$workflow"; require 'if: always()' "$workflow"
reject 'azure/login@' "$workflow"; reject 'az deployment' "$workflow"; reject 'az role' "$workflow"; reject 'continue-on-error' "$workflow"
test "$(grep -Ec '^[[:space:]]*- uses:' "$workflow")" = 2; test "$(grep -Fc 'actions/checkout@11d5960a326750d5838078e36cf38b85af677262' "$workflow")" = 2
for template in authority-identities.bicep authority-role-definitions.bicep provisioner-assignment.bicep cleanup-assignments.bicep residue-reader-assignment.bicep; do az bicep build --file "$dir/$template" --stdout --no-restore >/dev/null; done
node --test "$dir/authority.test.mjs"
echo 'broker foundation authority policy tests passed'

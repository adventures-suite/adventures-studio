#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
workflow="$root/.github/workflows/private-migration-runner.yml"
require(){ rg -q --fixed-strings -- "$1" "$2"; }
reject(){ ! rg -q --fixed-strings -- "$1" "$2"; }
require 'Status: Superseded; do not deploy' "$root/docs/architecture/ephemeral-private-migration-runner.md"
require 'Status: Superseded; do not deploy' "$root/docs/architecture/ephemeral-runner-registration-broker.md"
reject 'ephemeral-runner-registration-broker.yml' "$workflow"
reject 'az deployment' "$workflow"
reject 'az role assignment' "$workflow"
require 'workflow_dispatch:' "$workflow"
reject 'push:' "$workflow"
reject 'pull_request:' "$workflow"
require 'environment: database-development' "$workflow"
require 'RUNNER_GROUP: ${{ vars.PRIVATE_MIGRATION_RUNNER_GROUP }}' "$workflow"
require "test \"\$RUNNER_GROUP\" = 'private-sql-migration-vnet'" "$workflow"
require 'group: ${{ vars.PRIVATE_MIGRATION_RUNNER_GROUP }}' "$workflow"
reject 'group: private-sql-migration-vnet' "$workflow"
require 'labels: adventures-suite-private-sql' "$workflow"
require "test \"\$RUNNER_READY\" = 'private-sql-vnet-runner-v1'" "$workflow"
require 'cancel-in-progress: false' "$workflow"
require 'allow-no-subscriptions: true' "$workflow"
require 'ADVENTURESSUITE_MIGRATION_CREDENTIAL_MODE: github-oidc-azure-cli' "$workflow"
require "if: inputs.operation == 'proof-only'" "$workflow"
require "if: inputs.operation == 'run-migration'" "$workflow"
require 'sqlCommandAttempted":false' "$workflow"
require 'gh attestation verify' "$root/.github/scripts/verify-reviewed-migration-package.sh"
require 'migration-package-evidence.mjs --verify-locks' "$root/.github/scripts/verify-reviewed-migration-package.sh"
require 'REQUIRED_LOCK_PATHS' "$root/.github/scripts/migration-package-evidence.mjs"
reject 'sqlcmd' "$root/.github/scripts/prove-private-sql-network.sh"
reject 'AdventuresSuite.DatabaseMigrator' "$root/.github/scripts/prove-private-sql-network.sh"
test "$(rg -c '^\s*- uses:' "$workflow")" = 2
test "$(rg -c '@[0-9a-f]{40}' "$workflow")" = 2
echo 'hosted private migration runner policy tests passed'

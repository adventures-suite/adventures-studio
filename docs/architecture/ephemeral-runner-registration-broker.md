# Ephemeral Runner Registration Broker

**Status:** repository implementation complete; no live resources, authority, key, installation, or registration exists

## Purpose and authority boundary

The broker is the sole future component allowed to exchange a narrowly bound,
short-lived workload assertion for one GitHub repository JIT runner
configuration. It never grants the migration or SQL-bootstrap UAMI GitHub
administration authority. Repository ID `1317655952`, owner ID `316268438`,
protected `main`, workflow, Environment, operation ID, runner name, labels,
group, work directory, purpose, and 45-minute deadline are closed bindings.

The operation state machine remains `Approved` -> `Issuing` -> `Issued` ->
`Cleaning` -> `Closed`, with terminal `Failed`. Redemption is atomic and
one-use. An ambiguous GitHub response or response loss never mints again.
Independent cleanup performs at most one exact runner deletion, performs no
deletion on ambiguity, and requires zero-residue readback.

## Dedicated development foundation

The repository-owned foundation is
`infrastructure/runner-registration-broker/main.bicep`. It creates only:

- Function `func-adventures-suite-runner-broker-dev` on FC1 plan
  `plan-adventures-suite-runner-broker-dev` in `westus2`;
- Azure Functions v4 on Linux and Node.js 24, 512 MB, maximum one instance,
  zero always-ready instances, and HTTP concurrency one;
- dedicated `stadvsrunnerbrokerdev` Standard LRS StorageV2 with shared keys and
  public access disabled, private `function-releases` container, and
  `RunnerOperations` table;
- dedicated `snet-runner-broker-integration` at `10.40.4.0/27`, delegated only
  to `Microsoft.App/environments`;
- Blob, Queue, and Table private endpoints with exact private DNS bindings;
- dedicated `kv-adventures-runner-dev` in RBAC mode, with public access
  disabled, a private endpoint, purge protection, soft delete, and 90-day
  retention; and
- dedicated Application Insights and Log Analytics resources with 30-day
  retention, a 1-GB/day cap, and bounded/redacted telemetry policy.

The Function system identity's resource, principal, and tenant IDs and every
foundation resource ID are post-deployment outputs. Existing VNet,
private-endpoint subnet, and vault DNS-zone IDs are mandatory approval inputs
without defaults. The foundation creates no role definition, role assignment,
secret, GitHub credential, code deployment, SQL resource, VM, public IP, NSG,
firewall rule, or runner registration. It cannot operate before later authority
and application deployment boundaries pass.

The Function HTTPS endpoint is public so an exact protected workflow can reach
it, but it is not anonymously authoritative. Exact workload-token validation,
endpoint allowlisting, bounds, and one-use state remain mandatory. Storage and
Key Vault data planes are private.

## App-key custody

The future GitHub-generated PKCS#8 RSA PEM is stored only as Key Vault secret
`github-app-4590229-private-key`, content type `application/x-pem-file`.
Broker configuration requires one exact immutable secret-version URI;
versionless references fail.

`import-app-key.mjs` is the one-purpose SDK importer. It:

- accepts key bytes only on inherited descriptor 3 and verifies an anonymous
  pipe or socket;
- rejects key transport through arguments, environment variables, ordinary
  stdin, clipboard, custom data, outputs, artifacts, or repository files;
- selects one exact managed-identity client ID, not a default credential chain;
- caps input at 16 KiB and accepts only PKCS#8 RSA keys of at least 2048 bits;
- uses the official Azure Identity and Key Vault Secrets SDKs;
- imports only the exact secret name/content type and requires an immutable
  version URI from Key Vault;
- emits only operation ID, vault ID, secret name, immutable version URI, GitHub
  key ID, public-key fingerprint, and bounded UTC timestamps; and
- overwrites input buffers on success, failure, cancellation, and timeout.

The SDK requires a managed string during `setSecret`; it is scoped to that call
and is never retained, logged, returned, or placed in evidence. Source and
aggregate byte buffers are explicitly overwritten. A five-minute deadline and
signal cancellation fail closed with zero retry.

The real-key packet must create a reviewed RAM-backed browser-download volume,
disable shell history and clipboard use, stream the single file through an
anonymous pipe to descriptor 3, delete the RAM source, unmount it, clear bounded
browser download history, and prove no PEM in approved paths, process arguments,
environment, logs, artifacts, or shell history. Tests generate fictional keys
in memory and never contact GitHub or Azure.

## Evidence, telemetry, and ambiguous outcomes

Telemetry is redacted operational data, never durable audit authority. App
keys, JWTs, installation tokens, JIT configuration, Azure tokens,
authorization headers, raw claims/responses, PEM text, connection strings,
package URLs, bootstrap content, and arbitrary labels are prohibited from
logs, traces, metrics, errors, state, evidence, environment, command lines,
deployment inputs, and artifacts.

Key-import evidence follows `key-custody-evidence.schema.json`, rejects extra
fields, and is limited to 2 KiB. Service failures expose only stable codes. If
Key Vault might have accepted a value but immutable-version readback fails, the
operation is ambiguous: stop without retry, deletion, or repair and require a
separately approved metadata readback.

## Deployment and approval ordering

These boundaries cannot be combined:

1. merge the inert repository correction and bind exact SHA/checksums;
2. provision only the Azure foundation;
3. independently read back every resource and the Function identity;
4. create and assign exact data-plane roles under a separate approval;
5. deploy and verify broker code without arming an operation;
6. approve the importer actor/private path and RAM-backed custody operation;
7. generate one GitHub key, import once, and prove local residue absent;
8. separately install the App into repository ID `1317655952` only;
9. bind the App, installation, and immutable key version;
10. separately arm, register, use, clean, and prove zero residue.

No automatic retry or destructive rollback exists.

## Rotation, emergency response, and cost

Rotation creates a new GitHub key and immutable Key Vault version, validates it
without runner work, updates the exact broker binding, proves idleness, and
then revokes the old GitHub key. Two versions are never simultaneously active.
Old vault versions follow retention and are not purged as rollback.

Emergency response separately disables broker ingress, proves no active
operation, suspends/uninstalls the App, revokes the GitHub key, disables the
exact vault version, independently removes an unambiguous runner, and proves
zero registration residue.

With zero always-ready instances, compute should remain within the Flex grant.
The corrected estimate is USD 30-33/month, dominated by the Blob, Queue, Table,
and Key Vault private endpoints and two new private DNS zones. Retail prices
must be rechecked in the deployment what-if. A new endpoint is a material
repository/cost change, never an execution-time invention.

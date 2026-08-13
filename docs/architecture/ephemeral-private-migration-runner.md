# Ephemeral Private Migration Runner

**Status:** Inert reviewed design; provisioning remains blocked

This increment defines one disposable GitHub self-hosted Azure VM. It does not
provision, grant authority, register a runner, retrieve an artifact, connect to
SQL, or execute DbUp.

## Exact choices

- Create operation-scoped subnet `10.40.3.0/27`; never reuse `snet-devtools` or the old SQL-administration VM/resources.
- Pin Canonical Ubuntu 24.04 to `24.04.202608070`, Trusted Launch, Secure Boot, and vTPM. Use `Standard_B2als_v2` (two vCPU/four GiB), the lowest practical size for the self-contained migrator and runner.
- Use one 32-GiB `StandardSSD_LRS` OS disk. NIC and disk delete with VM; no data disk, public IP, password, inbound port, extension, or persistent compute exists. Azure's Linux provisioning contract receives an operation-only public key, but the private half is never delivered to the workflow or VM and the NSG permits no SSH; the VM deletion removes the public half.
- Attach only UAMI `/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourcegroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-job-dev`.
- Permit private SQL `10.40.1.4:1433`, IMDS `169.254.169.254:80`, and HTTPS only to `github.com`, `api.github.com`, `objects.githubusercontent.com`, `results-receiver.actions.githubusercontent.com`, `actions.githubusercontent.com`, `fulcio.sigstore.dev`, `rekor.sigstore.dev`, and `timestamp.sigstore.dev`. NSGs cannot enforce FQDN, so guest nftables bounds TCP 443 again. VNet DNS needs a final implementation test before authority is added.
- Absolute lifetime is 45 minutes. Guest timeout is secondary; independent GitHub-hosted `if: always()` cleanup is authoritative without VM contact.

The runner uses `--ephemeral --disableupdate`, one operation label, and a short-lived token never placed in VM `customData`, written, or logged. It authenticates its exact UAMI to a future broker, exchanges the one-use approval nonce in memory, and unsets the returned token immediately after configuration. The approved broker hostname must exactly match the HTTPS URL and is added to the guest allowlist; it is intentionally unresolved in this correction. Independent cleanup calls the broker to revoke/delete any registration without VM contact. Ordinary `GITHUB_TOKEN` cannot mint registration tokens, so the workflow unconditionally fails until that OIDC/managed-identity broker and cleanup identity pass a later review. No PAT, client secret, GitHub App key, runner token, or signed package URL is added.

Artifact retrieval must bind repository/organization IDs, run/artifact IDs, protected SHA, package/catalog checksums, evidence, and SLSA provenance. Storage uses 0700/`umask 077`; cleanup removes registration, VM, NIC, disk, subnet, NSG, files, later temporary assignments, and tagged residue.

No automatic retry is allowed. Ambiguity, timeout, cancellation, runner loss, artifact/SQL failure, missing evidence, or residue stops the boundary. The action catalog is review input, not a role; broad Contributor, assignment writes, public IP, VNet/private-link/DNS/SQL mutation are excluded.

Corrections B/C remain separate: this does not advance migration 0009, alter SQL grants or migration sources, or change the package.

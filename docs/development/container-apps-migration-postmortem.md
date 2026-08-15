# Superseded Container Apps Migration Path: Postmortem

The Container Apps/ACR migration execution design was abandoned before any SQL
migration ran. Foundation run `31566238223`, bound to protected-main
`457feb20084ad49353b2146bdd97ad5d38e1c79c`, failed while creating the Basic ACR
because that SKU/configuration did not support disabling public network access
(`DisablingPublicNetworkAccessNotSupported`). The subnet, Log Analytics
workspace, and managed environment created before that failure were later
removed through the separately approved Owner rollback; the registry was never
created.

Lessons:

- validate SKU/network capability and the full pull/publication topology before
  approving a foundation deployment;
- retained structured failures must preserve allowlisted Azure error codes
  without raw CLI output;
- a secure registry path added cost and control-plane complexity unrelated to
  the mature DbUp migration semantics; and
- partial infrastructure success requires an explicit retain-or-rollback
  decision, not an automatic retry.

Git history retains the former implementation. No executable archive is kept.
Existing Azure identities, custom-role definitions, and provider registrations
are not deleted by this repository decision and convey no authority while
unassigned.

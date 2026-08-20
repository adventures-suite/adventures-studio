# Planner Alpha release readiness

**Status:** Local candidate evidence complete; deployment and tagging pending

## Candidate assessed

The local readiness walkthrough on 2026-08-19 assessed protected-main commit
`b73d37abe1daba0af07b63c57cfeebe1d437b52a`. The walkthrough used only a fresh
disposable SQL Server container, the compiled synthetic local Alpha identity,
fictional content, and the normal application authorization and mutation paths.

## Evidence

- The repository selected .NET SDK `10.0.303`.
- The clean database committed migrations `0001` through `0009`, stopped at
  the intentional Companion policy-role prerequisite, and then completed
  `0010` through `0013` after the exact empty local role was created.
- The final journal contained 13 entries and ended at
  `0013_grant_planning_runtime_permissions.sql`.
- Repeating the bounded identity bootstrap preserved exactly one synthetic
  user, one external identity, one active Planner membership, zero additional
  permission grants, and zero plans before the browser walkthrough.
- Normal cookie sign-in reached the Creator-scoped Planner without bypassing
  authorization.
- The browser created one fictional private plan, filtered the library to the
  Lisbon Destination FootStep, previewed it, reviewed inclusive dates, and
  added it through the ordinary POST/antiforgery/PRG path.
- The result advanced the plan version and persisted one destination, one
  immutable FootStep application record, and matching audit evidence.
- The application record retained source
  `footstep_destination_lisbon_gateway@1.0`, target type
  `DestinationVisit`, fingerprint version 1, and the resulting plan version.
- The plan and destination remained present after the web host was stopped and
  restarted against the same database.
- Two browser sessions loaded the same expected version. The accepted edit
  advanced the plan; the second edit returned the explicit conflict state and
  did not overwrite the accepted title.
- Dark mode committed component state correctly.
- A genuine Chrome viewport reported 320 CSS pixels with a 320-pixel document,
  no horizontal overflow, a dialog-labeled mobile FootSteps drawer, focus moved
  to Close, Escape dismissal, and focus returned to the FootSteps trigger.
- The post-restart Chrome walkthrough produced no console warnings or errors.

## Remaining release boundary

This evidence proves the local candidate workflow. It does not authorize an
Azure migration, deployment, production identity change, shared-database
mutation, or tag. After this documentation is reviewed and merged, repeat the
required build and hosted checks for the final protected-main commit, deploy
that exact commit through the approved environment workflow, perform bounded
post-deployment health and workflow verification, and request explicit human
approval before creating `v0.1.0-alpha.1`.

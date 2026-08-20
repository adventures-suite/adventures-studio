# Release process

Releases are evidence-backed labels on an exact protected-main commit. A tag
does not replace pull-request review, required checks, deployment controls, or
database-migration approval.

## Planner Alpha release candidate

Prepare the Planner Alpha candidate on a focused branch from current
`origin/main`. Do not add a feature while proving readiness. Fix only a blocker
that prevents the approved workflow from being reproduced, then rerun the
affected evidence.

The candidate must prove:

1. the exact repository SDK and locked restore succeed;
2. a fresh disposable local SQL database reaches the latest reviewed migration
   through the documented administrator-prerequisite sequence;
3. the application DML identity cannot perform DDL;
4. the bounded synthetic identity bootstrap is an exact no-op when repeated;
5. normal cookie authentication reaches the authorized Creator workspace;
6. a private plan can be created through the browser;
7. a Destination FootStep can be filtered, previewed, date-reviewed, and added
   through the ordinary antiforgery-protected PRG workflow;
8. the plan, destination, audit intent, and immutable FootStep application
   evidence persist atomically and survive an application restart;
9. a stale concurrent edit is rejected without overwriting current state;
10. desktop light/dark presentation and a genuine 320–375 CSS-pixel mobile
    layout work without horizontal overflow, inaccessible drawer focus, or
    browser console errors; and
11. release build, automated tests, formatting, credential scanning, and the
    required hosted checks pass for the final candidate commit.

Evidence must use fictional data and may contain bounded counts, migration
names, source-version identifiers, result versions, and pass/fail outcomes. It
must not contain credentials, connection strings, cookies, antiforgery tokens,
private customer content, or browser storage.

## Alpha tag

Use an annotated semantic prerelease tag such as `v0.1.0-alpha.1`. Create and
push it only after:

- the readiness pull request is merged through the normal protected path;
- the exact resulting protected-main commit is built and deployed through the
  approved environment workflow;
- post-deployment health and the bounded customer walkthrough pass against
  that exact commit; and
- a human explicitly approves tagging that deployed commit.

Never move or reuse an Alpha tag. A correction produces the next prerelease
number. Azure migration execution, application deployment, and tag creation
remain separate decisions with separate evidence.

# Slice 5E Browser and Pipeline Security Matrix

**Status:** Implemented; automated web gate passing

**Last Updated:** August 8, 2026

## Scope Boundary

This matrix closes Slice 5E browser and pipeline controls. It does not provision
Azure identity configuration (Slice 5F), add Creator membership (Slice 6), or
make public Creator hosts private authentication surfaces.

## Gate Checklist

| Gate | Required behavior | Evidence | Status |
| --- | --- | --- | --- |
| Host validation | Resolve only an approved Creator host or the exact workspace host; unknown hosts return 421; forwarded host values do not expand trust. | `CreatorResolutionMiddlewareTests`, `PublicRouteIntegrationTests` | Pass |
| CSRF / antiforgery | Authentication runs before antiforgery when private identity is enabled. Razor Components use antiforgery middleware; POST, PUT, PATCH, and DELETE requests carrying the application cookie validate by default; exact configured OIDC callback paths use protocol state, nonce, and correlation instead; explicit opt-outs remain reviewable; Blazor transport POSTs use the separate exact-Origin gate. Bearer mobile/API authentication remains deferred and is deliberately outside browser antiforgery policy. | `BrowserSecurityPipelineTests`, `ExternalIdBrowserEndpointTests` | Pass |
| Redirect safety | Authentication return targets are bounded local paths; scheme-relative, encoded, traversal, callback, and authentication endpoint targets are rejected; sign-out is POST-only. | `ExternalIdBrowserEndpointTests`, `AuthenticationContractTests` | Pass |
| Cookie scope | Application cookie is `__Host-` prefixed, Secure, HttpOnly, path `/`, host-only, SameSite=Lax, non-sliding, and server-session bounded. The production antiforgery cookie is `__Host-` prefixed, Secure, HttpOnly, path `/`, host-only, and SameSite=Strict; isolated Development HTTP uses a separate non-`__Host-` development cookie with SameAsRequest so local forms remain testable. OIDC correlation and nonce cookies explicitly retain SameSite=None, Secure=Always, and HttpOnly behavior and are not weakened by a global override. | `ExternalIdAdapterTests`, `ApplicationCookieOptionsValidator`, `Program.cs` | Pass |
| SignalR origin | Workspace negotiate, WebSocket, SSE, long-polling, and reconnect paths under `/_blazor` require the exact configured workspace Origin. Missing, malformed, suffix-confused, encoded-host, wrong-port, public-Creator, and cookie-on-public-host attempts fail before a circuit is established. Host casing follows URI semantics; forwarded headers do not expand the match. | `BrowserSecurityPipelineTests` | Pass |
| Circuit revalidation | Interactive Server circuits use `CircuitRevalidationInterval` and recheck the authoritative application session and security version against the canonical workspace origin. Invalid state becomes unauthenticated; protected services remain the final authorization boundary. | `BrowserSecurityPipelineTests`, `AuthenticationRuntimeTests` | Pass |
| Browser headers | Success, host-denial, error, framework, and fallback responses receive one consolidated CSP, frame denial, MIME-sniffing protection, strict referrer policy, and a restrictive Permissions Policy. Production HTTPS responses receive one-year HSTS with subdomains and preload. HSTS is disabled in Development; trusted-proxy provisioning and validation remain a Slice 5F environment gate. | `BrowserSecurityPipelineTests`, `Program.cs` | Pass |
| CSP compatibility | Sources are restricted to same-origin assets, with data images only. `object-src` is disabled and base, form, and frame destinations are restricted. The Razor `ImportMap` receives a cryptographically random per-response nonce; broad inline script execution is not enabled. Inline style remains temporarily allowed for existing component-local style output; no third-party script or style host is allowed. | `BrowserSecurityPipelineTests`, `App.razor` | Pass with documented style compatibility exception |
| Information leakage | Public hosts cannot activate authentication outcome pages; persistence and remote-provider failures return generic outcomes; invalid authoritative sessions do not disclose private state. | `ExternalIdAdapterTests`, `ExternalIdBrowserEndpointTests`, `AuthenticationRuntimeTests` | Pass |
| Public-route security | Public content remains available only on approved Creator hosts; draft/unpublished/cross-Creator content remains unavailable; workspace cookies cannot activate private circuits on public hosts. | `PublicRouteIntegrationTests`, `BrowserSecurityPipelineTests` | Pass |

## Verification Command

```text
dotnet test tests/TheSimontonAdventures.Web.Tests/TheSimontonAdventures.Web.Tests.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
```

Expected result: all tests pass.

The SQL integration project remains a separate gate and requires
`ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING`. Slice 5E does not change database
schema or persistence behavior.

The local Kestrel smoke launch initially stalled because the restricted test
namespace denied local socket binding. Running the identical compiled artifact
outside that namespace completed both startup validators, listened normally,
and served the interactive `/counter` route with the expected security headers.
This is classified as an execution-environment restriction rather than an
application startup defect.

## Slice Boundary Decision

Do not begin Slice 5F until this matrix remains green in CI. Slice 5F must then
run the configured External ID browser smoke tests against the real canonical
workspace origin without weakening any control above.

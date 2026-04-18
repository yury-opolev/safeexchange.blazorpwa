# A06:2025 — Insecure Design

**Findings:** 5 · **Highest priority:** P1

---

## [P2] [MEDIUM] Client-side permission check in `CanEditObject()` fails open (default-allow)

- **Category:** A06:2025 — Insecure Design (also A01)
- **CWE:** CWE-602 (Client-Side Enforcement of Server-Side Security), CWE-841 (Improper Enforcement of Behavioral Workflow)
- **File:** `SafeExchange.Client.Web.Components/Pages/ViewData.razor:255-278`
- **Severity:** Medium · **Exploitability:** Easy · **Exposure:** Authenticated · **Confidence:** High · **Priority: P2**

See `A01-broken-access-control.md` for the full write-up — same root cause, filed in both categories because A01 treats it as "missing authorization" and A06 treats it as a design-level "default-allow fallback" anti-pattern.

**Systemic fix:** Have the server return `canEdit`/`canDelete`/`canGrant`/`canRevoke` flags on every `ObjectMetadataOutput` / `CompoundModel` response. The client should mirror the server's decision, not compute it locally.

---

## [P1] [HIGH] Access-request flow has no client-side ceiling on requested permission scope; DTO mass-assignment

- **Category:** A06:2025 — Insecure Design
- **CWE:** CWE-807 (Reliance on Untrusted Inputs in a Security Decision), CWE-472 (External Control of Assumed-Immutable Web Parameter), CWE-501 (Trust Boundary Violation)
- **Files:**
  - `SafeExchange.Client.Web.Components/Pages/CreateAccessRequest.razor:61-77, 79-94`
  - `SafeExchange.Client.Common/Model/Dto/Input/SubjectPermissionsInput.cs`
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:278`
- **Severity:** High · **Exploitability:** Easy · **Exposure:** Authenticated · **Confidence:** High · **Priority: P1**

**Evidence:**

```csharp
if (NavManager.TryGetQueryString("permission", out string permissionValue))
{
    if (PermissionsConverter.TryParsePermissionString(permissionValue, ...))
        this.AccessRequest.PermissionsString = permissionValue;
    else
        this.AccessRequest.PermissionsString = "Read";
}
```

**Description:**

1. **No requested-scope ceiling.** The page reads target secret name and requested permission from URL query parameters and passes them straight into the request body. `PermissionsConverter.TryParsePermissionString` is purely syntactic. The UI `InputSelect` offers all permission levels (Read through Read+Write+GrantAccess+RevokeAccess). Any user can request the full set on any secret name; there is no cool-down, no rate-limit, and no state machine that ties the requested scope to any upper bound.
2. **DTO mass-assignment.** `SubjectPermissionsInput` is used by **both** `CreateAccessRequestAsync` and `GrantAccessAsync` — the same shape for "request access for me" and "grant access to X". The DTO carries `SubjectId`, `SubjectName`, `SubjectType` — fields that should be server-derived from the bearer token, but are bindable from the client.

**Attack Scenario:**

1. Attacker obtains a secret name (leaked deep-link, chat, enumeration of list-metadata endpoint).
2. Attacker crafts `/addrequest?subject=victim-secret-id&permission=Read,Write,GrantAccess,RevokeAccess` — no warning, no UI friction, no acknowledgment.
3. Attacker submits. Request lands in owner's incoming list. Owner, seeing a plausibly-named requester, may click "Grant" on the incoming list (`AccessRequests.razor:76`).
4. The incoming-request list doesn't prominently display the full requested permission bundle — owner approves without noticing the elevated scope.
5. Attacker can script the endpoint repeatedly (no captcha, no cooldown) — flood the owner with grant prompts until one click-through.

**Recommendation:**

- Request DTO should not accept `SubjectId`/`SubjectName` — server must re-derive requester identity from bearer token. Client DTO shape should be `{ secretName, requestedPermissions }` only.
- Incoming-request list must visibly highlight elevated scopes (Grant/Revoke) with red badges so owners can't fat-finger an approval.
- Introduce a design-level cap: at most one pending request per (secret, user), at most N requests per user per hour.
- Server must reject grant-requests exceeding the scope the secret owner has designated as "maximum grantable via request".

**Assumption:** If the server unconditionally caps requested permissions to Read, severity drops to Medium. If the server re-derives `SubjectId` from the token, the mass-assignment sub-flaw goes away but the "no ceiling" design flaw remains.

---

## [P2] [MEDIUM] No client indication of rate limiting / anti-abuse for search, access-request, and notification endpoints

- **Category:** A06:2025 — Insecure Design (missing anti-abuse control)
- **CWE:** CWE-799 (Improper Control of Interaction Frequency), CWE-1125 (Excessive Attack Surface)
- **Files:**
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:512 SearchUsersAsync`, `518 SearchGroupsAsync`, `278 CreateAccessRequestAsync`, `443 RegisterWebPushSubscriptionAsync`, `46 GetCompoundModelAsync`
  - `SafeExchange.Client.Web.Components/Shared/ItemSearchDialog.razor:323` (timer-based debounce — UX not anti-abuse)
- **Severity:** Medium · **Exploitability:** Easy · **Exposure:** Authenticated · **Confidence:** High · **Priority: P2**

**Description:** None of the client paths have:

- 429 handling or `Retry-After` interpretation.
- Idempotency keys on `CreateAccessRequestAsync` / `RegisterWebPushSubscriptionAsync`.
- Per-session max count for outbound access requests.
- Any UI surfacing of server-side throttling.

`ProcessResponseAsync` (`ApiClient.cs:526`) treats any non-`ok` status as a generic error — no 429 branch. `ItemSearchDialog` has a typing-debounce timer for UX only — it can be bypassed by calling `ApiClient.SearchUsersAsync` directly.

**Attack Scenario:**

1. Attacker scripts repeated calls to `/v2/user-search` with their token. Backend forwards to Graph → Graph throttles *the tenant* once quota exceeds. Legitimate users' "Find user" dialog stops working.
2. Simultaneously, attacker loops `CreateAccessRequestAsync` against every known secret name, producing a spam wave of notifications (web push + email) targeting secret owners. Without idempotency keys or per-(secret, user) dedup, the notifications queue floods.

**Recommendation:**

- Handle 429 / `Retry-After` in `ApiClient.ProcessResponseAsync` — parse, back off, surface a "slow down" banner.
- Add idempotency keys to `CreateAccessRequestAsync` and `RegisterWebPushSubscriptionAsync`.
- Server-side: per-user-per-endpoint token bucket; per-(secret, requester) dedup for pending access requests.

**Assumption:** If Azure APIM / Front Door is deployed with per-endpoint rate limiting, severity drops to Low (client-side robustness only).

---

## [P1] [HIGH] Chunked-upload resumable design has no client-side integrity/ordering/completion safeguards

- **Category:** A06:2025 — Insecure Design
- **CWE:** CWE-362 (Concurrent Execution Using Shared Resource), CWE-841, CWE-693 (Protection Mechanism Failure)
- **Files:**
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:406-431 PutSecretDataStreamAsync`, `208-266 UploadAttachmentsAsync`
  - `SafeExchange.Client.Common/Model/ChunkMetadata.cs` (`Hash` field — defined but unused client-side)
  - `SafeExchange.Client.Common/ApiClient/SecretContentStream.cs:106-122`
- **Severity:** High · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** High · **Priority: P1**

**Description:** The chunked upload protocol has:

- **No explicit chunk index.** Chunks distinguish "interim" from "final" only by the client-asserted `X-SafeExchange-OpType: interim` header.
- **No explicit finalize call.** No commit endpoint.
- **No client-side hash verification on read.** `SecretContentStream.Read` (lines 106-122) concatenates chunks directly into the caller buffer without consulting `ChunkMetadata.Hash`. The `Hash` field exists but is never read.
- **No resume token.** On mid-stream failure (`attachment.Status = UploadStatus.Error; break;` at line 246), the client stops and has no resumable retry; re-upload from byte 0 with no epoch.

**Design consequences:**

- **Truncation by design.** N-1 interim chunks + a "final-but-marked-interim" chunk yields a truncated object with no commit marker.
- **Reordering / concurrent-writer race.** Two tabs uploading the same content have no client-side collision detection; the server ticket is opaque to the client.
- **Undetected tampering on download.** A compromised CDN cache serving wrong chunks is not detected.

**Attack Scenario (integrity attack on download path):**

1. Attacker gets the ability to modify chunk content at rest (compromised cache layer, server bug).
2. Client downloads via `SecretContentStream` — reader blindly concatenates bytes, ignoring `Hash`.
3. User reads a tampered secret with no way to know.
4. A design that verified `ChunkMetadata.Hash` would detect tampering.

**Attack Scenario (upload truncation / race):**

1. Attacker opens two browser tabs for the same secret edit page.
2. Both tabs call `DropContentDataAsync` + `PutSecretDataStreamAsync` — neither carries an epoch/version token.
3. Tab A uploads chunks 1-3 and final; Tab B starts uploading with a different access ticket, overwriting mid-stream.
4. One upload is silently truncated/mixed. UI reports success for both.

**Recommendation:**

- Add `X-SafeExchange-ChunkIndex` header and an explicit final `POST .../commit` call.
- Verify `ChunkMetadata.Hash` in `SecretContentStream.Read` (via `IncrementalHash` / `SHA256.HashData`) and throw on mismatch.
- Add a per-upload client-side epoch (GUID) as `X-SafeExchange-UploadEpoch`.
- Server: treat each upload as atomic content replacement, reject mid-stream writes across epochs.

**Assumption:** If the server enforces SHA verification per chunk on ingest + single-writer semantics per contentId (e.g., ETag `If-Match`), the upload-corruption scenario drops to Low — but the download-path non-verification remains.

---

## [P1] [HIGH] PWA offline service worker + silent-token reuse have no stale-session design

- **Category:** A06:2025 — Insecure Design
- **CWE:** CWE-384 (Session Fixation-adjacent), CWE-613 (Insufficient Session Expiration), CWE-656
- **Files:**
  - `SafeExchange.PWA/wwwroot/service-worker.published.js:88-101` (`onFetch`)
  - `SafeExchange.PWA/wwwroot/manifest.json` (missing `scope`, `id`)
  - `SafeExchange.Client.Web.Components/Classes/ApiAuthorizationMessageHandler.cs`
  - `SafeExchange.Client.Web.Components/Classes/Helpers/TokenHandler.cs`
- **Severity:** High · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** High · **Priority: P1**

**Description:**

- **Service worker serves cached `index.html` for every navigation** (line 93 — `shouldServeIndexHtml = event.request.mode === 'navigate'`). `ApiAuthorizationMessageHandler` silently attaches tokens with no revocation check. Result: the design gives:

  - **Multi-tab stale session.** If a user logs out in Tab A (or admin revokes session in Entra), Tab B still has the MSAL token and will continue to use it. No `visibilitychange` / `BroadcastChannel` coordination between tabs.
  - **Offline-install replay.** A user who installs the PWA while logged in can later open the installed app offline and use cached content even after server-side revoke.
  - **No `scope` / `id` in manifest.** PWA installation covers the entire origin; any path under the origin becomes the PWA context after install.
  - **`TokenHandler.GetName` has no staleness check** — it reads claims with no awareness of token expiry. Fallback fails open precisely when the token is malformed.

**Attack Scenario:**

1. User installs the PWA, logs in, views several secrets.
2. Account is disabled in Entra; admin revokes all refresh tokens.
3. MSAL still has the access token in storage; service worker serves `index.html` from cache. Every read/write succeeds until the access-token TTL expires.
4. User opens a second tab of the PWA; both tabs read and write with no "session was revoked" signal.
5. Separately: an attacker with temporary device access opens the installed PWA offline and can read cached UI chrome, trigger navigation, and attempt to use the stored token against the backend when they next come online.

**Recommendation:**

- **Session heartbeat:** call `/v2/me/ping` on PWA startup and on `visibilitychange`. If it fails with 401/403, force logout, clear `sessionStorage`, unregister the service worker, `caches.delete` the offline cache.
- **Cross-tab coordination:** `BroadcastChannel('auth')` — Tab A's logout posts a message that Tab B handles by clearing state.
- **PWA manifest:** add `scope` and `id` to `manifest.json` — explicit `"scope": "/", "id": "/"`, or restrict to an authenticated sub-path.
- **Service worker:** invalidate cache entries when a logout happens; refuse to serve stale `index.html` after a logout message.

**Assumption:** If the backend invalidates tokens on logout server-side (JWT denylist / very short TTL), severity drops to Medium — but the multi-tab UX design gap remains.

---

## Systemic Observation

A06-1, A06-2, and A06-5 share **fail-open defaults** and **client-owned identity/authorization decisions** in a design that was clearly written assuming "the server is the source of truth". That stance is correct in principle, but the client keeps offering security-relevant decisions (edit gate, request permission scope, session validity) without a disciplined "server-says-no means client-says-no" pattern.

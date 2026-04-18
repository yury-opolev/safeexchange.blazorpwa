# A01:2025 — Broken Access Control

**Findings:** 3 · **Highest priority:** P2

---

## [P2] [MEDIUM] Client-side-only authorization with fail-open default in `CanEditObject()`

- **Category:** A01:2025 — Broken Access Control (also A06 — Insecure Design)
- **CWE:** CWE-602 (Client-Side Enforcement of Server-Side Security), CWE-287 (Improper Authentication)
- **File:** `SafeExchange.Client.Web.Components/Pages/ViewData.razor:255-278`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** High · **Priority: P2**

**Description:** `CanEditObject()` determines whether to enable the Edit/Delete button by comparing the current user's UPN against `compoundModel.Permissions`. Two issues:

1. **Fail-open on missing UPN** (lines 257-261):

   ```csharp
   var currentUserUpn = TokenHandler.GetName(authenticationState.User);
   if (string.IsNullOrEmpty(currentUserUpn) || !currentUserUpn.Contains("@"))
   {
       return true;
   }
   ```

   Any authenticated session whose ID token lacks `preferred_username` / `upn` / `ClaimTypes.Upn` (federated guest, application identity, custom B2C policy, social login) receives a UI that permits edit and delete regardless of the actual ACL.

2. **Client-side-only decision** (lines 263-277): the permission decision is made in the browser from JSON. An attacker in DevTools can flip the local `compoundModel.Permissions` list and enable the button, or directly navigate to `/editdata/{objectName}` (the route is `[Authorize]` only — no per-object ACL check).

**Attack Scenario:**
1. A federated user obtains an ID token without a UPN-shaped claim. `TokenHandler.GetName()` returns null.
2. User navigates to `/viewdata/{secret}`. Edit button is enabled.
3. User clicks Edit (or direct-navigates `/editdata/{secret}`), modifies, submits.
4. If the backend under-enforces write authorization for any principal shape, write succeeds. If not, UX is broken.

**Recommendation:** Flip the fallback to fail-closed (`return false;` on empty UPN). Use stable `oid`/`sub` claims instead of UPN. Best: have the server return `canEdit`/`canDelete`/`canGrant` flags on `CompoundModel` and mirror them in the client.

**Assumptions:** If the backend always enforces write permissions independently (server A01), client impact is UX only. If the fail-open branch is unreachable because all deployed IdPs always emit UPN, severity drops to Low.

---

## [P3] [LOW] MSAL `ValidateAuthority: false` in client auth config

- **Category:** A01:2025 — Broken Access Control / A07 — Authentication Failures / A04 — Cryptographic Failures (cross-category finding)
- **CWE:** CWE-287, CWE-295
- **File:** `SafeExchange.PWA/wwwroot/appsettings.json:16`
- **Severity:** Low · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** Confirmed · **Priority: P3**

```json
"AzureAdB2C": {
  "Authority": "https://login.microsoftonline.com/1472703e-99d8-45ee-aeac-7ec3ae9ab104",
  "ClientId": "fdfa17b0-db9b-4927-8b83-07055ae70c39",
  "ValidateAuthority": false
}
```

**Description:** `ValidateAuthority: false` tells MSAL.js to skip the check that the authority URL is a known Microsoft cloud instance. The current authority is `login.microsoftonline.com/<tenant-id>` — the standard Entra ID endpoint, which is in MSAL's built-in allowlist — so the flag is **unnecessary**. It weakens a defense-in-depth control against authority substitution: if `appsettings.json` is ever mutated (CDN cache poisoning, mis-templated build pipeline, static-host compromise), MSAL will happily redirect to an attacker-controlled OIDC endpoint.

The config section is named `AzureAdB2C` but the authority is actually a plain AAD tenant — suggesting the flag was copy-pasted from a B2C sample without being needed.

**Attack Scenario:** Requires a prior integrity compromise of `wwwroot/appsettings.json` (supply-chain, CDN cache poisoning, SSA re-deploy) — then attacker rewrites `Authority` to a rogue OIDC host and captures credentials on redirect. Defense-in-depth, not directly exploitable.

**Recommendation:** Remove the `ValidateAuthority` key (defaults to `true`) or set it explicitly to `true`. Rename the config section from `AzureAdB2C` to `EntraId` to reflect reality and prevent re-introduction. Consolidate with the A02/A04/A07 duplicates of this finding.

---

## [P4] [INFO] Un-encoded user-influenced path segments in `ApiClient` URL construction

- **Category:** A01:2025 — Broken Access Control (adjacent to A05 — Injection)
- **CWE:** CWE-20, CWE-22, CWE-116
- **File:** `SafeExchange.Client.Common/ApiClient/ApiClient.cs:135-437` (every method)
- **Severity:** Info · **Exploitability:** Theoretical (self-only) · **Exposure:** N/A (client) · **Confidence:** Confirmed · **Priority: P4**

**Description:** Every `ApiClient` method interpolates identifiers (`secretId`, `contentId`, `chunkId`, `pinnedGroupId`) into path templates without `Uri.EscapeDataString()`. Example at line 137:

```csharp
new Uri(client.BaseAddress, $"{ApiVersion}/secret/{secretId}/content/{contentId}/all")
```

Characters like `/`, `?`, `#`, `%`, or `..` break path-segment boundaries and can be smuggled into unintended routes.

**Context:** This is a **client** holding the user's own access token — a user who can already issue arbitrary HTTP to the backend via DevTools. The primary impact is correctness (bugs when secret names contain reserved characters) and self-inflicted routing confusion, not a remote-attacker vector. A05's analysis covers the same root cause from a different angle (URL injection, P2) — see `A05-injection.md`.

**Recommendation:** Wrap every path parameter in `Uri.EscapeDataString(...)` (single-line fix per call site) and constrain `{objectName}` in Razor routes. Validate secret names at create-time against `^[a-zA-Z0-9._-]{1,128}$`.

---

## Verification Checklist (reviewer's own work)

- [x] Read `Program.cs` + `ServicesHelper.cs` — confirmed global `[Authorize]` fallback via MSAL + route `[Authorize]` attribute.
- [x] Read `ApiAuthorizationMessageHandler.cs` — confirmed bearer token is narrowly scoped to backend URL only.
- [x] Confirmed no open redirects: `NavigationManager.NavigateTo` targets are always relative and derived from hard-coded strings or route parameters.
- [x] No SSRF: `HttpClient.BaseAddress` is fixed; no user-supplied URLs flow into HTTP calls.
- [x] Path traversal in downloads not exploitable — `attachment.FileName` flows to browser `download` attribute / `showSaveFilePicker.suggestedName`, both sanitized by browser.
- [x] No CORS config in client (client has no server).

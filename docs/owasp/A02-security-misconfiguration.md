# A02:2025 — Security Misconfiguration

**Findings:** 3 · **Highest priority:** P2

---

## [P2] [MEDIUM] No Content Security Policy and no hardening meta tags in `index.html`

- **Category:** A02:2025 — Security Misconfiguration
- **CWE:** CWE-693 (Protection Mechanism Failure), CWE-16 (Configuration)
- **File:** `SafeExchange.PWA/wwwroot/index.html` (no `<meta http-equiv="Content-Security-Policy">`, no Referrer-Policy, no hosting config checked in)
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Internet · **Confidence:** High · **Priority: P2**

**Description:** `index.html` contains only three `http-equiv` meta tags — all for caching (`Cache-Control`, `Pragma`, `Expires`). There is no `Content-Security-Policy`, no `Referrer-Policy`, no `X-Frame-Options`, no `X-Content-Type-Options`, no `Strict-Transport-Security`, no `Permissions-Policy`. Repo-wide grep of all six header names returns zero matches.

Because the Blazor WASM publish is a pure static asset bundle, no middleware can set these headers — they have to be applied at the hosting layer (Azure Static Web Apps `staticwebapp.config.json`, CDN, reverse proxy, IIS `web.config`), none of which are checked in. The auto-generated `publish/web.config` also has no `<customHeaders>` block.

**Why this matters (attack amplifier):** The app uses a rich text editor (`RichTextEditor.cs` + `richTextEditor.js` assign `element.innerHTML = value`) that handles user-supplied HTML. The primary stored-XSS finding in A05 (`@((MarkupString)...)` in `ViewData.razor`) depends on the absence of CSP to reach maximum impact. A strict `Content-Security-Policy: default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; connect-src 'self' https://login.microsoftonline.com https://*.azurewebsites.net; frame-ancestors 'none'; base-uri 'none'; object-src 'none'` would block inline-event-handler execution and exfiltration to arbitrary origins.

**Attack Scenario:**
1. Attacker (a legitimate SafeExchange user with share permission) crafts a secret whose rich-text body smuggles `<img src=x onerror="...">` past Quill's paste sanitizer via direct API POST.
2. Victim opens the secret. Because there is no CSP, the injected handler runs with the application origin's privileges.
3. Script reads MSAL tokens from `sessionStorage` / `localStorage` and posts them to `attacker.example/collect`. No CSP `connect-src` restriction; no `script-src 'self'`.
4. Attacker uses the stolen access token against `safeexchange-staging.azurewebsites.net/api/` to read every secret the victim had access to.
5. A strict CSP meta tag in `index.html` would break steps 3-4 even without fixing the primary XSS.

**Verification:** Grepped the entire repo for `Content-Security-Policy|Strict-Transport|X-Frame-Options|X-Content-Type-Options|Referrer-Policy|Permissions-Policy` → 0 matches. No `staticwebapp.config.json` or equivalent hosting config present.

**Recommendation:**
- Add a `<meta http-equiv="Content-Security-Policy" content="default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' https://login.microsoftonline.com https://*.azurewebsites.net; frame-ancestors 'none'; base-uri 'none'; object-src 'none'">` tag to `wwwroot/index.html`. Note `'wasm-unsafe-eval'` is required by Blazor WASM.
- Check in `wwwroot/staticwebapp.config.json` (or equivalent for your host) setting `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: geolocation=(), microphone=(), camera=()`, `X-Frame-Options: DENY`.

**Assumptions:** If the production deployment target (Azure Static Web Apps, Cloudflare, reverse proxy) already enforces these headers outside the repo, the finding is informational. Cannot be verified from repo contents alone — hence Medium severity. As long as the headers aren't checked in, every new environment is a re-introduction risk.

---

## [P3/P4] [LOW] MSAL `ValidateAuthority` explicitly disabled for a standard Entra ID authority

- **Category:** A02:2025 — Security Misconfiguration (cross-linked to A01/A04/A07)
- **CWE:** CWE-1174 (Misconfiguration: Improper Model Validation), CWE-16
- **File:** `SafeExchange.PWA/wwwroot/appsettings.json:13-17` (bound at `SafeExchange.Client.Web.Components/ServicesHelper.cs:69`)
- **Severity:** Low · **Exploitability:** Theoretical · **Exposure:** Internet · **Confidence:** Confirmed · **Priority: P4**

See `A01-broken-access-control.md` for the detailed write-up. The finding is duplicated across four categories and should be addressed once.

**Recommendation:** Remove `"ValidateAuthority": false` and rename the `AzureAdB2C` section to `EntraId` to prevent future maintainers from reintroducing the flag.

---

## [P4] [INFO] PWA manifest has no explicit `scope`

- **Category:** A02:2025 — Security Misconfiguration
- **CWE:** CWE-16
- **File:** `SafeExchange.PWA/wwwroot/manifest.json`
- **Severity:** Info · **Exploitability:** Theoretical · **Exposure:** Internet · **Confidence:** High · **Priority: P4**

`scope` is omitted. Default `scope` is derived from `start_url` (here `./` → origin root). Same-origin default, so mostly informational — but declaring `"scope": "/"` explicitly documents the contract. No `permissions` / `prefer_related_applications` set.

**Recommendation:** Add `"scope": "/"` and `"id": "/"` to `manifest.json`.

---

## Observations That Are NOT Findings

- No hardcoded credentials or secrets in repo (`appsettings.json` only contains public identifiers — ClientId, tenant, VAPID public key).
- `VapidOptions.PrivateKey` property exists in a client class library (latent risk — see A04).
- `launchSettings.json` has only dev-only data (`ASPNETCORE_ENVIRONMENT=Development`, localhost URLs) — not used in production.
- No debug/Swagger/test endpoints (browser-only client, no server).
- No PDBs published in release artifact.
- No XML parsers (no XXE surface).
- No `eval` / `new Function` in app code (only minified vendor libs).
- No external CDN references → SRI not applicable for framework assets (but see A03 jQuery finding).

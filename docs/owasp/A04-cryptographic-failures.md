# A04:2025 — Cryptographic Failures

**Findings:** 5 · **Highest priority:** P2

---

## [P2] [MEDIUM] Bearer access tokens held in `sessionStorage` (MSAL default), reachable from any XSS

- **Category:** A04:2025 — Cryptographic Failures (sensitive data at rest in browser storage)
- **CWE:** CWE-522 (Insufficiently Protected Credentials), CWE-311 (Missing Encryption of Sensitive Data at Rest), CWE-312
- **File:** `SafeExchange.Client.Web.Components/ServicesHelper.cs:67-72`; related `Classes/ApiAuthorizationMessageHandler.cs`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Internet · **Confidence:** High · **Priority: P2**

**Evidence:**

```csharp
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
    builder.Configuration.Bind("AccessTokenScopes", options.ProviderOptions.DefaultAccessTokenScopes);
    builder.Configuration.Bind("AdditionalScopesToConsent", options.ProviderOptions.AdditionalScopesToConsent);
});
```

`AddMsalAuthentication` in Blazor WASM defaults to MSAL.js `cacheLocation = "sessionStorage"` and no override is present (`grep cacheLocation` → zero hits). Acquired access tokens (scope `api://safeexchange-staging.onmicrosoft.com/user_impersonation`) and the MSAL account record are readable by any JS executing in the page origin. `ApiAuthorizationMessageHandler` attaches those tokens as `Authorization: Bearer` on calls to the secret-exchange backend.

**Attack Scenario (chains with A05-1 stored XSS):**
1. Attacker finds any XSS sink (e.g., the A05 `@((MarkupString)...)` in `ViewData.razor`).
2. Attacker script reads `window.sessionStorage`, locates MSAL cache keys prefixed with the client-id `fdfa17b0-db9b-4927-8b83-07055ae70c39`, extracts the active access token.
3. Script POSTs the token to attacker's server.
4. Attacker calls `https://safeexchange-staging.azurewebsites.net/api/v2/secret/*` with the stolen bearer, lists and downloads every secret the victim can read.
5. Secrets are decrypted server-side and streamed as plaintext in the HTTP response (see `ApiClient.GetSecretDataStreamAsync`) — attacker gets full plaintext secret contents.

**Recommendation (ordered by effectiveness):**
1. **BFF pattern (strongest):** move the OAuth flow server-side. The server holds the access token; the browser holds only a first-party `Secure; HttpOnly; SameSite=Strict` session cookie. OWASP 2025 recommendation for SPAs handling sensitive data.
2. **CSP with nonce-based `script-src` + Trusted Types** (defense in depth — also fixes A02-02).
3. **Subresource Integrity** on third-party `<script>` tags in `index.html`.
4. **Short token lifetime** in the Entra ID app registration (15 minutes over 1 hour).

**Assumption:** Scoring assumes no BFF layer exists. Confirmed by reading `ServicesHelper.cs`, `ApiAuthorizationMessageHandler.cs`, and `ApiClient.cs` — the bearer is attached client-side.

---

## [P3] [MEDIUM] VAPID `PrivateKey` property defined in Blazor WASM client model (latent trap)

- **Category:** A04:2025 — Cryptographic Failures
- **CWE:** CWE-321 (Hardcoded Cryptographic Key — latent), CWE-320 (Key Management)
- **File:** `SafeExchange.Client.Web.Components/Classes/Model/VapidOptions.cs:15`
- **Severity:** Medium · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** High · **Priority: P3**

**Evidence:**

```csharp
public class VapidOptions
{
    public string Subject { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }   // line 15 — in a Blazor WASM client project
}
```

**Description:** The type lives inside `SafeExchange.Client.Web.Components`, a Razor class library consumed by a Blazor WebAssembly host whose entire binary and `appsettings.json` ship to the browser. No consumer currently populates `VapidOptions.PrivateKey` (`grep PrivateKey` returns only this declaration), but the shape matches the common `web-push` library DTO — a future developer adding `"PrivateKey": "..."` to `wwwroot/appsettings.json` would publish the VAPID signing key to the public internet. Possession of that key lets anyone impersonate the app server to every push service and deliver forged notifications to every subscribed user.

**Recommendation:** Delete `PrivateKey` from `VapidOptions`. VAPID signing must happen server-side only. If a shared DTO is needed, split: `VapidClientOptions { Subject, PublicKey }` in the client lib, `VapidServerOptions : VapidClientOptions { PrivateKey }` in a server-only library.

**Assumption:** If `VapidOptions` is also used by a server-side project I did not read, move it to a server-only library instead of deleting the property. The concern is that the type lives in a client-shipped assembly.

---

## [P3] [MEDIUM] No transport-scheme enforcement on `BackendApi:BaseAddress`

- **Category:** A04:2025 — Cryptographic Failures
- **CWE:** CWE-319 (Cleartext Transmission of Sensitive Information)
- **File:** `SafeExchange.Client.Web.Components/ServicesHelper.cs:38-49`; config at `SafeExchange.PWA/wwwroot/appsettings.json:7-9`
- **Severity:** Medium · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** High · **Priority: P3**

**Evidence:**

```csharp
client.BaseAddress = new Uri(apiConfiguration["BaseAddress"]!);
```

No assertion that `BaseAddress.Scheme == "https"`. The currently-shipped `appsettings.json` uses HTTPS, but the value is operator-controlled. A future operator or CI pipeline that sets `"BaseAddress": "http://..."` (for a local dev backend) will cause the Blazor WASM app to (a) attach the MSAL bearer token over cleartext HTTP and (b) stream secret plaintext over cleartext HTTP. MSAL's `AuthorizationMessageHandler.ConfigureHandler` does not refuse `http://` URLs.

**Attack Scenario:** Operator misconfiguration → one static `appsettings.json` edit away from "every request leaks token and secret plaintext" to any on-path attacker (corporate proxy, rogue Wi-Fi, ISP, mis-configured TLS-inspection appliance). Modern browsers block mixed content from HTTPS pages, so the window requires the PWA itself to be served over HTTP or for users to bypass the block.

**Recommendation:**

```csharp
var baseAddress = new Uri(apiConfiguration["BaseAddress"]!);
if (!baseAddress.IsAbsoluteUri || baseAddress.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException(
        $"BackendApi:BaseAddress must use https:// (got '{baseAddress.Scheme}').");
}
client.BaseAddress = baseAddress;
```

Apply the same check in `ApiAuthorizationMessageHandler` before calling `ConfigureHandler(authorizedUrls: [...])`. Set `Strict-Transport-Security` at the hosting layer.

---

## [P4] [INFO] No client-side end-to-end encryption (architectural note)

- **Category:** A04:2025 — Cryptographic Failures (design disclosure)
- **CWE:** CWE-311
- **Files:** `SafeExchange.Client.Common/ApiClient/SecretContentStream.cs`, `PartialStreamContent.cs`, `ApiClient.cs:406-437`
- **Severity:** Info · **Exploitability:** N/A · **Exposure:** Auth · **Confidence:** Confirmed · **Priority: P4**

**Description:** Secret bytes flow from Quill / file attachments straight to the HTTP body with no client-side encryption, no KDF, no AEAD, no integrity MAC. Confidentiality relies entirely on TLS and backend honesty. A product called "SafeExchange" may lead users to assume zero-knowledge semantics (cf. Bitwarden, 1Password, Proton Pass); this is a legitimate design choice, but it must be stated because:

1. A compromised backend operator can read every secret.
2. A backend logging bug leaks every secret (cross-link to A09).
3. Any backend A01 bug exfiltrates plaintext.

**Recommendation (forward-looking):** If zero-knowledge is desired, introduce client-side AEAD (libsodium / WebCrypto AES-GCM-256, random IV per chunk), derive the data key via Argon2id from a user passphrase, store only ciphertext + wrapped keys on the backend. Significant design change — track as a roadmap item, not a bug.

**Assumption:** If the backend does server-side at-rest encryption and the design trust model is "backend is trusted", this is documented design, not a vuln.

---

## [P4] [LOW] `ValidateAuthority: false` (duplicate — see A01/A02/A07)

- **Category:** A04:2025 (cross-linked)
- **Priority: P4** (collapsed against A01)

See `A01-broken-access-control.md` for the full write-up. Consolidate remediation with the other three duplicates.

---

## Clean Areas (verified)

- `SecretContentStream.cs`, `PartialStreamContent.cs`: pure byte plumbing, no crypto primitives.
- `TokenHandler.cs`: only reads claims from `ClaimsPrincipal` — no storage logic.
- `ApiAuthorizationMessageHandler.cs`: uses the framework-provided handler with a narrow scope list.
- `NotificationsSubscriber.cs` / `PushNotifications.cs`: only handle the VAPID **public** key; delegate to browser `PushManager`.
- No use of MD5, SHA1, DES, 3DES, RC4, ECB, `RNGCryptoServiceProvider`, or `new Random()` in application code.
- No `ServerCertificateCustomValidationCallback`, no `rejectUnauthorized: false`, no TLS downgrades.
- No hardcoded keys/IVs/salts/tokens matching typical patterns.
- No local password storage — authentication fully delegated to Entra ID.

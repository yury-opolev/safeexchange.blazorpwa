# A08:2025 — Software or Data Integrity Failures

**Findings:** 2 · **Highest priority:** P3

---

## [P3] [MEDIUM] Push-notification URL from encrypted payload used to open/navigate windows without allowlist

- **Category:** A08:2025 — Software or Data Integrity Failures
- **CWE:** CWE-829 (Inclusion of Functionality from Untrusted Control Sphere), CWE-345 (Insufficient Verification of Data Authenticity)
- **File:** `SafeExchange.PWA/wwwroot/service-worker.published.js:39-59` (identical code in `service-worker.js:33-56`)
- **Severity:** Medium · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** High · **Priority: P3**

**Description:** The `onNotificationClick` handler reads `event.notification.data.url` (which originates from `payload.url` in the encrypted Web Push message) and uses it to drive `matchingClient.navigate(urlToOpen)` and `clients.openWindow(urlToOpen)`. The URL is resolved via `new URL(event.notification.data.url, self.location.origin)`, which correctly anchors relative URLs — but **absolute cross-origin URLs are preserved as-is**. No allowlist, no pattern check, no verification of the target origin. `clients.openWindow` will open any URL in a new tab. While `matchingClient.navigate` on a cross-origin URL is blocked by the browser, `openWindow` is not.

**Trust assumption:** The payload is encrypted to the browser's push subscription keypair (`p256dh`/`auth`) and signed by the backend with VAPID — so only the legitimate backend (or an insider holding the VAPID private key) can inject the URL. The finding is that the client blindly trusts a signed-but-unbounded URL.

**Attack Scenario (post backend compromise / insider):**

1. Attacker obtains the SafeExchange backend's VAPID private key (separate compromise, insider, accidental exposure) or exploits a server-side bug that lets them control the `url` field in outgoing push messages.
2. Attacker sends `{ "message": "New secret shared", "url": "https://attacker.example/fake-login?state=…" }` to a victim with an active subscription.
3. Victim sees what looks like a normal SafeExchange notification, clicks "Open".
4. Browser calls `clients.openWindow("https://attacker.example/fake-login?state=…")` because no matching client URL exists.
5. Phishing page mimics SafeExchange login and harvests Entra ID credentials.

**Recommendation:**

```javascript
const urlToOpen = new URL(event.notification.data.url, self.location.origin);
if (urlToOpen.origin !== self.location.origin) {
    urlToOpen.href = self.location.origin + '/';  // safe default
}
```

Or better: send a path-only identifier from the backend (`"path": "/viewdata/foo"`) and prefix it with `self.location.origin` in the SW, ignoring any absolute URL entirely.

**Assumption:** If the backend's `NotificationPayload.url` can be influenced by untrusted input (e.g., sharer-controlled text, secret name reflected verbatim), severity rises to **High** / P2 — the precondition ("control the URL field") is satisfied without compromising the backend at all. Verify server-side generation of this field.

---

## [P4] [LOW] `ProcessResponseAsync` parses API response as JSON without content-type or status-code gate

- **Category:** A08:2025 — Software or Data Integrity Failures
- **CWE:** CWE-345 (Insufficient Verification of Data Authenticity)
- **File:** `SafeExchange.Client.Common/ApiClient/ApiClient.cs:526-559`
- **Severity:** Low · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** Low · **Priority: P4**

**Description:** `ProcessResponseAsync<T>` reads the HTTP response body and passes it straight to `JsonSerializer.Deserialize<BaseResponseObject<T>>(content, this.jsonOptions)` with no check of `response.IsSuccessStatusCode`, `Content-Type`, or response origin. The backend is same-origin in normal deployment, and `System.Text.Json` with default options is safe (no polymorphic type discriminator, no `TypeNameHandling`). So the concrete risk is limited to the app trusting any JSON-shaped bytes that reach it.

**Recommendation (defense-in-depth):** Check `response.IsSuccessStatusCode` before deserializing; verify `response.Content.Headers.ContentType?.MediaType == "application/json"`; use `HttpResponseMessage.Content.ReadFromJsonAsync<T>()` which enforces content type.

**Assumption:** If `JsonSerializerOptions` is ever changed to enable `JsonPolymorphic` / custom converters, or if model types gain constructors with side effects, this would move to a real High-confidence finding.

---

## Clean Areas (verified)

- **No unsafe deserializers.** Grep for `BinaryFormatter`, `NetDataContractSerializer`, `SoapFormatter`, `LosFormatter`, `TypeNameHandling`, `Newtonsoft`, `XmlSerializer`, `DataContractSerializer`, `JsonDerivedType`, `JsonPolymorphic`, `Assembly.Load`, `Activator.CreateInstance`, `Type.GetType` — all zero hits.
- **No external CDN scripts.** `index.html` loads only same-origin `_content/…` and `_framework/…` resources. SRI would be moot for first-party same-origin JS.
- **Blazor offline cache enforces per-asset SRI.** `service-worker.published.js:74` uses `new Request(asset.url, { integrity: asset.hash })`, so each cached asset is verified against the framework-generated hash manifest.
- **No GitHub Actions workflows.** `.github/workflows` exists but is empty — no mutable action refs, no `pull_request_target`, no unsigned release pipeline. If workflows are added later, they need a fresh review.
- **Manifest scope is safe.** `manifest.json` does not set `scope`; default is `./` (equal to `start_url`). No widening.
- **Update detector is passive.** `UpdateAvailableDetector.razor` + `sw-registrator.js` only notify that a new SW is installed and rely on `window.location.reload()` driven by user click. The new SW is first-party, same-origin, and SRI-checked per asset.
- **Push payload is parsed as JSON, not `eval`-ed.** No `innerHTML` / DOM injection from push payload.

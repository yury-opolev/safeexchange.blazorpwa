# A09:2025 — Security Logging and Alerting Failures

**Findings:** 4 · **Highest priority:** P2

---

## [P2] [MEDIUM] Silent `catch` paths hide client-side security-relevant events

- **Category:** A09:2025 — Security Logging and Alerting Failures
- **CWE:** CWE-778 (Insufficient Logging)
- **Locations:**
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:260-264` (`UploadAttachmentsAsync` swallows exception into `attachment.Error`, never logged)
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:540-556` (`ProcessResponseAsync` catches `Exception` and converts to a `BaseResponseObject`, no logger)
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:581-588` (`ProcessStreamResponseAsync` catches `Exception`, no logger)
  - `SafeExchange.Client.Web.Components/Classes/StateContainer.cs:138-141` (`TryFetchAccessRequestsAsync` silently calls `exception.Redirect()` on AuthN failure)
  - `SafeExchange.Client.Web.Components/Shared/ItemSearchDialog.razor:386-389, 565-568`
  - `SafeExchange.Client.Web.Components/Pages/CreateAccessRequest.razor:110-113`
  - `SafeExchange.Client.Web.Components/Pages/AccessRequests.razor:231-233, 274-276`
  - `SafeExchange.Client.Web.Components/Pages/CreateData.razor:569-572`
  - `SafeExchange.Client.Web.Components/Pages/EditData.razor:721-724, 865-868, 909-912`
  - `SafeExchange.Client.Web.Components/Pages/ListData.razor:119-122`
  - `SafeExchange.Client.Web.Components/Pages/ViewData.razor:331-334`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Internet · **Confidence:** Confirmed · **Priority: P2**

**Description:** Security-relevant events are completely silent client-side. Every `AccessTokenNotAvailableException` catch immediately calls `exception.Redirect()` with no telemetry and no user-visible banner. A stale session, a revoked token, a failed silent renewal, and an intentional token grab all look identical. `ProcessResponseAsync` / `ProcessStreamResponseAsync` fold network/transport exceptions into a string on `BaseResponseObject` with no `ILogger` call. There is no `ErrorBoundary` in `App.razor` / `MainLayout.razor`, no Application Insights / OpenTelemetry / Sentry wiring in `Program.cs` / `ServicesHelper.cs`. `appsettings.json` has only a minimal `Logging:LogLevel:Default = Information` block.

**Evidence:**

```csharp
// StateContainer.cs lines 138-141
catch (AccessTokenNotAvailableException exception)
{
    exception.Redirect();
}
```

```csharp
// ApiClient.cs lines 540-556
catch (Exception ex)
{
    if (response != null && response.StatusCode != HttpStatusCode.OK)
    {
        return new BaseResponseObject<T>()
        {
            Status = response.StatusCode.ToString(),
            Error = string.IsNullOrEmpty(content) ? $"{ex.GetType()}: {ex.Message}" : content
        };
    }

    return new BaseResponseObject<T>()
    {
        Status = "exception",
        Error = $"{ex.GetType()}: {ex.Message}"
    };
}
```

**Attack Scenario:**

1. Attacker in control of a victim's browser session (via an XSS, hostile browser extension, or compromised auth-popup flow) forces repeated background calls to probe for secrets the victim has read access to.
2. Each failure (token-refresh failure, 403, 500) travels through `ProcessResponseAsync`'s catch, is converted to a `BaseResponseObject { Status = "exception" }`, and no log line reaches the developer console or any backend telemetry surface.
3. Victim sees, at most, a silently redirected page or no change at all.
4. Because client-side logs never leave the browser, attacker activity is invisible to incident response.

**Recommendation:** Add structured `ILogger` logging to every `catch` site, emit `NotificationData` to `StateContainer` to surface user-visible errors on auth failures, and wire up a client-side telemetry sink (Application Insights or equivalent) that redacts tokens/secret content before shipping.

---

## [P2] [MEDIUM] Under-surfacing of security events — `AccessTokenNotAvailable` silently redirects with no notification

- **Category:** A09:2025 — Security Logging and Alerting Failures
- **CWE:** CWE-778
- **Locations:** (10+ sites, all `catch (AccessTokenNotAvailableException exception) { exception.Redirect(); }`)
  - `StateContainer.cs:138-141`
  - `Pages/ViewData.razor:331-334`
  - `Pages/EditData.razor:721-724, 865-868, 909-912`
  - `Pages/CreateData.razor:569-572`
  - `Pages/AccessRequests.razor:231-233, 274-276`
  - `Pages/ListData.razor:119-122`
  - `Pages/CreateAccessRequest.razor:110-113`
  - `Shared/ItemSearchDialog.razor:386-389, 565-568`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Internet · **Confidence:** High · **Priority: P2**

**Description:** Every `AccessTokenNotAvailableException` is handled with the identical idiom `exception.Redirect();` and nothing else — no `NotificationData`, no cause explanation, no "your session expired" signal. A stale-session / revoked-consent / blocked-popup / MFA-expired scenario is indistinguishable from "random UI glitch". The OWASP A09 sub-agent explicitly flags this as a "security-relevant event that should have a 'let the user know'-level signal".

**Attack Scenario:**

1. Attacker forces intermittent failed token renewal (e.g., via a sibling tab manipulating MSAL state, a revoked consent prompt, or targeted network interference).
2. User is silently redirected to login every few clicks, with no explanatory UI — trained to "just click through" login prompts.
3. A credential-phishing pop-up or look-alike redirect target (combined with an open-redirect chain) fits the same "silent re-login" pattern and is more likely to succeed.
4. After a successful credential harvest, there is no client-side audit of how often the redirect was invoked — no way to correlate the phishing moment with a session anomaly.

**Recommendation:** Before calling `exception.Redirect()`, set `StateContainer.SetNextNotification(...)` with a warning that the user's session expired and they're being returned to login. Count redirects in a session-scoped metric and surface them if they exceed a threshold (anomaly detection).

---

## [P3] [LOW] `Console.WriteLine` used as ad-hoc logger in place of `ILogger`

- **Category:** A09:2025 — Security Logging and Alerting Failures
- **CWE:** CWE-778
- **Locations:**
  - `ApiClient.cs:412` — `Console.WriteLine($"Uploading interim content '{contentId}', size: {uploadSize}.");`
  - `ApiClient.cs:420` — `Console.WriteLine($"Uploading content '{contentId}'...");`
  - `Pages/ViewData.razor:377` — `Console.WriteLine($"Started {nameof(DownloadToFileStreamAsync)}.");`
  - `Pages/ViewData.razor:453` — `Console.WriteLine($"Started {nameof(DownloadAsBlobUrlAsync)}.");`
  - `Pages/CreateData.razor:467` — `Console.WriteLine($"{exception.GetType()}: {exception.Message}");` (inside catch)
  - `Pages/EditData.razor:584` — same pattern
- **Severity:** Low · **Exploitability:** Hard · **Exposure:** Local (browser console) · **Confidence:** Confirmed · **Priority: P3**

**Description:**

1. Log level config (`appsettings.json` → `Logging:LogLevel:Default: Information`) does not apply — lines emit regardless, including in production.
2. Structured logging is bypassed — `contentId` is concatenated into a string rather than passed as a structured parameter.
3. The exception-handling `Console.WriteLine` sites log only `Message`, losing `StackTrace`, `InnerException`, and correlation context — classic insufficient-logging for debugging/detection.

**Recommendation:** Replace every `Console.WriteLine` with `ILogger.LogDebug("...{Method}", nameof(...))` — structured, level-gated, and consistent with the rest of the codebase (`StateContainer.cs`, `AccessRequests.razor` already use `ILogger`, just with string interpolation that should also be refactored to structured form).

**Assumption:** `contentId` is a server-assigned blob identifier, not secret material. If a future refactor starts logging `SecretName` or filename at these sites, severity rises to Medium.

---

## [P3] [LOW] No global `ErrorBoundary` and no central exception sink

- **Category:** A09:2025 — Security Logging and Alerting Failures
- **CWE:** CWE-778
- **Files:**
  - `SafeExchange.PWA/App.razor` (no `ErrorBoundary`)
  - `SafeExchange.PWA/Shared/MainLayout.razor` (no `ErrorBoundary`)
  - `SafeExchange.PWA/wwwroot/appsettings.json` (no telemetry config)
- **Severity:** Low · **Exploitability:** Moderate · **Exposure:** Internet · **Confidence:** Confirmed · **Priority: P3**

**Description:** Blazor WASM uncaught exceptions default to rendering "An unhandled error has occurred. Reload." in the `#blazor-error-ui` element and dumping the full exception to `Console.Error`. With no `ErrorBoundary` wrapper and no telemetry pipeline, exceptions that leak from catch-rich pages:

1. Print stack traces to the browser console (info disclosure, minor).
2. Provide nothing useful to the user (banner is a dead-end).
3. Provide nothing to operators (no telemetry path).

**Recommendation:** Add `<ErrorBoundary>` in `MainLayout.razor` wrapping `@Body` with a custom `ErrorContent` that logs via `ILogger`, displays a friendly message, and optionally ships redacted diagnostics to a telemetry endpoint. If telemetry is intentionally off-the-device for privacy, document that decision and at minimum wire up the boundary for user-facing recovery.

**Assumption:** If the operator intentionally keeps telemetry off the client for privacy (reasonable for a secrets app — you don't want to ship secret names to a SaaS telemetry vendor), the finding becomes "implement a local-only `ErrorBoundary`". Severity then drops to Info/P4.

---

## Not Found (verification results)

- **Tokens / passwords / secret content / auth headers in log statements:** None. `TokenHandler.cs` does no logging. `ApiClient.PutSecretDataStreamAsync` sets `X-SafeExchange-Ticket` without logging it. No grep hit for `WriteLine.*token|password|secret|Authorization|Bearer|apiKey`.
- **Full request/response body logging:** None.
- **Service-worker cache leakage:** `service-worker.published.js:68, 79` logs only `Service worker: Install` / `Activate`. `pushNotifications.js` logs only a literal string. No cached response content, endpoint URLs, or push payloads printed.
- **Application Insights / OTel / Sentry** — not configured (cross-referenced as A09-04).

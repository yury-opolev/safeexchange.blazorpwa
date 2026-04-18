# Client-side telemetry — SafeExchange PWA

This folder documents the browser-side Application Insights integration
shipped in the PWA. Files:

- `README.md` — architecture and component map (this file)
- `security-considerations.md` — threat model, mitigations considered,
  and what we chose not to do (with rationale)
- `setup.md` — operator runbook for configuring and rotating keys

## Goals

1. Capture errors, traces, and page views from the Blazor WASM client
   so they land in the same Application Insights instance as the
   backend Azure Function.
2. Correlate client and server activity on a single operation ID
   (W3C Trace Context) so a single user journey can be followed from
   the browser through the API.
3. Never emit telemetry for anonymous / unauthenticated users.
4. Never expose PII (UPN, email, display name) to App Insights —
   only the opaque AAD `oid` claim.
5. Allow telemetry to be disabled per-environment without redeploying
   code, via a single flag in `deployment/.env`.

## Component map

```
┌─────────────────────────────┐
│  appsettings.json           │   Source ships Telemetry.Enabled=false
│   "Telemetry": {            │   Real values injected at deploy by
│     "Enabled": bool,        │   deploy-pwa.ps1 from .env.
│     "ConnectionString": ""  │
│   }                         │
└──────────┬──────────────────┘
           │ (config binding)
           ▼
┌─────────────────────────────────────────────────────────────┐
│  TelemetryOptions         │  Scoped. Holds Enabled + string. │
└──────────┬──────────────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────┐        ┌─────────────────────────┐
│  TelemetryService           │◀──────▶│  telemetry.js           │
│   - InitializeAsync()       │        │   window.saexTelemetry  │
│   - TrackEvent/Exception    │        │   .initialize           │
│   - Listens to              │  JS    │   .setAuthenticated     │
│     AuthenticationState     │interop │   .trackEvent/Exception │
│     changes                 │        │   .trackTrace/PageView  │
│   - Holds sessionOperation  │        │   .flush                │
│     Id (correlation)        │        └──────────┬──────────────┘
└────────▲────────────────────┘                   │
         │                                         ▼
         │                            ┌─────────────────────────┐
         │                            │  ai.3.3.9.min.js        │
         │                            │  (bundled AI JS SDK)    │
         │                            └────────────┬────────────┘
         │                                         │ TLS, correlation
         │                                         ▼
         │                            ┌──────────────────────────────────┐
         │                            │  <region>.applicationinsights     │
         │                            │        .azure.com                 │
         │                            └──────────────────────────────────┘
         │
┌────────┴────────────────────┐        ┌─────────────────────────┐
│  TelemetryLoggerProvider    │──────▶│  Blazor ILogger*        │
│   + TelemetryLogger         │ bridge │   Warning+ → telemetry  │
└─────────────────────────────┘        └─────────────────────────┘

┌─────────────────────────────┐
│  App.razor                  │  Wraps Router in ErrorBoundary;
│   + ErrorBoundary           │  calls TrackException on uncaught
└─────────────────────────────┘  exceptions.
```

## Data flow

1. **Boot.** `App.razor.OnInitializedAsync` calls
   `TelemetryService.InitializeAsync`. If `TelemetryOptions.Enabled`
   is `false` or `ConnectionString` is empty, we return immediately —
   no SDK touched, no network traffic.

2. **SDK init.** Otherwise JS interop calls `saexTelemetry.initialize`
   which constructs an `ApplicationInsights` instance with
   `disableTelemetry: true`. The SDK is loaded but muted.

3. **Auth state.** `TelemetryService` subscribes to
   `AuthenticationStateProvider.AuthenticationStateChanged`. On every
   transition it calls `saexTelemetry.setAuthenticated(isAuth, oid)`.
   That flips `appInsights.config.disableTelemetry` accordingly. On
   authentication, we pass the AAD `oid` claim as the authenticated
   user context — **not** UPN or email.

4. **Emission.** `TelemetryLogger` wraps the standard Blazor
   `ILogger`. Log entries at `Warning` and above are forwarded as
   `trackTrace` (no exception) or `trackException` (with exception).
   Components can also call `TelemetryService.TrackEventAsync` /
   `TrackExceptionAsync` directly.

5. **Correlation.** Every event gets a custom property
   `saex.sessionId = <guid>` generated per `TelemetryService`
   instance (effectively per app load). W3C Trace Context propagates
   automatically to the backend via `traceparent` headers on HTTP
   requests through the SDK's fetch/XHR hooks.

6. **Error boundary.** Any unhandled component exception flows into
   `App.razor`'s `<ErrorBoundary>`, which calls
   `TelemetryService.TrackExceptionAsync` and shows a user-safe
   fallback. The boundary can be reset with a button.

## Files on disk

| File | Purpose |
|---|---|
| `SafeExchange.Client.Web.Components/wwwroot/js/ai.3.3.9.min.js` | Bundled App Insights JS SDK (no CDN — see security notes). |
| `SafeExchange.Client.Web.Components/wwwroot/js/telemetry.js` | Thin window.saexTelemetry wrapper with auth gating. |
| `SafeExchange.Client.Web.Components/Classes/Telemetry/TelemetryOptions.cs` | Config POCO (Enabled + ConnectionString). |
| `SafeExchange.Client.Web.Components/Classes/Telemetry/TelemetryService.cs` | Managed wrapper over JS SDK; owns auth subscription. |
| `SafeExchange.Client.Web.Components/Classes/Telemetry/TelemetryLoggerProvider.cs` | Pipes ILogger → telemetry service. |
| `SafeExchange.Client.Web.Components/Classes/Telemetry/TelemetryLogger.cs` | ILogger implementation for the above. |
| `SafeExchange.Client.Web.Components/ServicesHelper.cs` | DI registration (`AddScoped<TelemetryService>` + the logger provider). |
| `SafeExchange.PWA/App.razor` | Wraps routing in ErrorBoundary; calls InitializeAsync. |
| `SafeExchange.PWA/wwwroot/index.html` | Script tags (`sw-registrator.js`, `ai.*.min.js`, `telemetry.js`) + CSP allowlisting AI ingestion domains. |
| `SafeExchange.PWA/wwwroot/appsettings.json` | Source ships `Telemetry.Enabled=false` + empty connection string. Real values injected at deploy time. |
| `deployment/.env` | Holds `APPSETTINGS_TELEMETRY_ENABLED_*` + connection strings (gitignored). |
| `deployment/deploy-pwa.ps1` | Reads env, rewrites the Telemetry section in the published appsettings.json before upload. |

Also see `security-considerations.md` for the threat model and the
matrix of mitigations considered/chosen.

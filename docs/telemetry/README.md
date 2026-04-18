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
5. Never expose the Application Insights connection string to
   anonymous / pre-sign-in clients. The credential lives in the
   backend's Key Vault and is only handed to authenticated browsers.

## Component map

```
┌───────────────────────────────────┐
│  Backend Key Vault                │
│   WebClientTelemetry--            │   Populated per env via
│      ConnectionString             │   `az keyvault secret set`
└──────────┬────────────────────────┘
           │ (KV config provider, backend startup)
           ▼
┌───────────────────────────────────┐
│  SafeExchangeTelemetryConfig      │   GET /v2/telemetry/config
│   (backend Azure Function)        │   requires valid tenant JWT
└──────────┬────────────────────────┘
           │ HTTPS (authenticated)
           ▼
┌─────────────────────────────┐        ┌─────────────────────────┐
│  TelemetryService (C#)      │◀──────▶│  telemetry.js           │
│   - InitializeAsync()       │        │   window.saexTelemetry  │
│   - TrackEvent/Exception    │  JS    │   .initialize           │
│   - Listens to              │interop │   .setAuthenticated     │
│     AuthenticationState     │        │   .trackEvent/Exception │
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

1. **Page load.** `wwwroot/appsettings.json` contains **no** telemetry
   configuration. `TelemetryService.InitializeAsync` subscribes to
   `AuthenticationStateProvider.AuthenticationStateChanged` and
   immediately queries the current state. For anonymous sessions,
   nothing else happens — no SDK call, no network traffic.

2. **Sign-in.** When auth transitions to authenticated,
   `TelemetryService` issues `GET /v2/telemetry/config` via the
   authenticated `BackendApi` `HttpClient`. The request carries the
   user's bearer token; the backend endpoint rejects anonymous
   requests with 401.

3. **Backend response.** The backend function reads the connection
   string from `IConfiguration["WebClientTelemetry:ConnectionString"]`
   (sourced from Key Vault via the standard config provider) and
   returns `{ enabled, connectionString }`. Returns empty
   `connectionString` + `enabled: false` if the secret is absent,
   which leaves the client with telemetry disabled for that env.

4. **SDK init.** On a truthy response the client calls
   `saexTelemetry.initialize(connectionString)`, which constructs the
   AI JS SDK instance. The config fetch is attempted exactly once per
   page load to avoid retry-storming the backend if it is down.

5. **Auth context.** `saexTelemetry.setAuthenticated(true, oid)` is
   called with the AAD `oid` claim (tenant-specific, opaque, not
   human-readable). Never UPN, email, or display name.

6. **Emission.** `TelemetryLogger` wraps the standard Blazor
   `ILogger`. Log entries at `Warning` and above flow as
   `trackTrace` (no exception) or `trackException` (with exception).
   Components can also call `TelemetryService.TrackEventAsync`
   directly — and do, for `SessionStarted`, `CreateSecret`,
   `ReadSecret`, `UpdateSecret`, `DeleteSecret`.

7. **Correlation.** Every event gets a custom property
   `saex.sessionId = <guid>` generated per `TelemetryService`
   instance (effectively per app load). W3C Trace Context propagates
   automatically to the backend via `traceparent` headers on HTTP
   requests through the SDK's fetch/XHR hooks.

8. **Error boundary.** Any unhandled component exception flows into
   `App.razor`'s `<ErrorBoundary>`, which calls
   `TelemetryService.TrackExceptionAsync` and shows a user-safe
   fallback. The boundary can be reset with a button.

## Files on disk

### Client (`safeexchange.blazorpwa`)

| File | Purpose |
|---|---|
| `SafeExchange.Client.Web.Components/wwwroot/js/ai.3.3.9.min.js` | Bundled App Insights JS SDK (no CDN — see security notes). |
| `SafeExchange.Client.Web.Components/wwwroot/js/telemetry.js` | Thin `window.saexTelemetry` wrapper with auth gating. |
| `SafeExchange.Client.Web.Components/Classes/Telemetry/TelemetryService.cs` | Fetches config from backend, drives SDK via JS interop, owns auth subscription. |
| `SafeExchange.Client.Web.Components/Classes/Telemetry/TelemetryLoggerProvider.cs` | Pipes `ILogger` → telemetry service. |
| `SafeExchange.Client.Web.Components/Classes/Telemetry/TelemetryLogger.cs` | `ILogger` implementation for the above. |
| `SafeExchange.Client.Web.Components/ServicesHelper.cs` | DI registration (`AddScoped<TelemetryService>` + the logger provider). |
| `SafeExchange.PWA/App.razor` | Wraps routing in `ErrorBoundary`; calls `InitializeAsync`. |
| `SafeExchange.PWA/wwwroot/index.html` | Script tags (sw-registrator, ai.min, telemetry.js) + CSP allowlisting AI ingestion/config domains. |

### Backend (`safeexchange`)

| File | Purpose |
|---|---|
| `SafeExchange.Core/Configuration/WebClientTelemetryConfiguration.cs` | Options class bound to `WebClientTelemetry` config section. |
| `SafeExchange.Core/Model/Dto/Output/WebClientTelemetryConfigOutput.cs` | DTO returned from the endpoint. |
| `SafeExchange.Core/Functions/SafeExchangeTelemetryConfig.cs` | Handler — reads config, returns DTO. |
| `SafeExchange.Functions/Functions/SafeTelemetryConfig.cs` | HTTP trigger at `v2/telemetry/config`. |
| `SafeExchange.Core/SafeExchangeStartup.cs` | Registers `WebClientTelemetryConfiguration`. |

### Operator artefacts

| Location | Contents |
|---|---|
| `safeexchange-staging-kv` / `safeexchange-backend` Key Vaults | Secret `WebClientTelemetry--ConnectionString` populated out of band via `az keyvault secret set` — one per env. |

See `security-considerations.md` for the threat model and
`setup.md` for operator steps.

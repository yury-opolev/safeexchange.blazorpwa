# Telemetry security considerations

## Threats we worry about

| # | Threat | Why it matters |
|---|---|---|
| T1 | An anonymous attacker extracts the App Insights connection string from the public PWA bundle and floods the ingestion endpoint from their own infrastructure. | Cost runaway (ingestion is billed per-GB) and signal masking (real errors drowned in noise). |
| T2 | An authenticated user (legitimately logged in) spams telemetry via the running app. | Same costs as T1 but bounded by one account's tab count. |
| T3 | An attacker extracts the connection string and then uses it from their own domain (no app involvement). | Same as T1 — the connection string is a bearer credential in the AI data plane. |
| T4 | PII leaks into telemetry: emails, UPNs, display names, secret names, secret values, bearer tokens. | SafeExchange is a secret-sharing app. Any leak through telemetry is a sensitive-data exposure. |
| T5 | Cross-environment contamination: staging telemetry lands in prod AI or vice versa. | Makes production signals unreliable and risks exposing staging secrets (if any) via prod dashboards. |
| T6 | Telemetry emitted by dev builds running on localhost is indistinguishable from real user telemetry. | Pollutes production signals; also a privacy issue if dev machine data lands in company AI. |

## Mitigations considered

| # | Mitigation | Effect on threats | Cost | Chosen? |
|---|---|---|---|---|
| M1 | Hide the connection string from the bundle. | T1, T3 | Impossible in a pure SPA — credentials must be client-side. | **No.** |
| M2 | Proxy telemetry via our backend API (authenticated). | T1, T3 (major reduction); T2 (reduced to per-user rate) | High — doubles API request volume, needs new endpoint + server-side AI SDK. | **No** (documented as future option). |
| M3 | Daily ingestion cap on the App Insights resource. | T1, T2, T3 (bounded cost); degrades legitimate telemetry after cap. | Low — one CLI call. | **Yes.** 2 GB/day per env. |
| M4 | Client-side sampling. | T2 (reduces noise); spam still counts against cap. | Low. | Kept at 100% for now; turned down if volume grows. |
| M5 | Authenticated-only telemetry gate. | T2 (partial — still requires a valid login); **no help** against T1/T3 because those bypass our client code. | Low — design decision. | **Yes.** Both our JS and C# sides enforce it. |
| M6 | Build-time / deploy-time Enabled flag. | T6 (dev machines never emit); lets operator kill-switch without redeploying. | Low. | **Yes.** `APPSETTINGS_TELEMETRY_ENABLED_<ENV>` in .env. |
| M7 | Strict CSP `connect-src` allowlisting AI ingestion domains only. | T4 partial — blocks accidental exfiltration to unknown hosts via the AI SDK. | Low — one line in index.html. | **Yes.** See index.html. |
| M8 | Never pass UPN/email/display name to SDK; only opaque AAD oid. | T4 | Low — one line in telemetry.js. | **Yes.** |
| M9 | Separate AI resources per environment, distinct connection strings. | T5 | Already configured. | **Yes.** staging uses `safeexchange-staging-insights`, prod uses `safeexchange-backend-insights`. |
| M10 | Bundle the AI JS SDK locally (no CDN). | T7 extended (no third-party script origin), keeps `script-src 'self'`. | Low — 149 KB bundled. | **Yes.** `wwwroot/js/ai.3.3.9.min.js`. |

## What we explicitly accepted (residual risk)

- **The connection string is extractable.** Anyone who can open devtools on `www.safeexchange.dk` can read `Telemetry:ConnectionString` from the bundled `appsettings.json`. This is inherent to client-side SPAs, and the backend-proxy mitigation (M2) is disproportionate for the current threat model. The daily cap (M3) bounds the blast radius to 2 GB/day (~$5/day at default pricing).
- **Authenticated users can emit arbitrary events.** The auth gate prevents anonymous ingestion, but any tenant user could, in principle, call our JS bridge directly from devtools. We accept this — their events carry their own `oid` so anomalies can be investigated.
- **No rate-limiting per user/IP at the AI ingestion layer.** Azure AI does not expose this natively; adding it requires fronting AI with an API Management instance (out of scope).

## What we deliberately do NOT send

Keep these out of `TrackEvent` / `TrackTrace` / `TrackException` properties:

- Access tokens, ID tokens, refresh tokens, any bearer-looking strings
- Email addresses, UPNs, display names (use the `oid` claim if you need user identity)
- Secret names or contents (SafeExchange's core data)
- Full URLs that embed path identifiers for secrets
  (strip path segments after `/viewdata/` / `/editdata/`)
- Raw request/response bodies

The JS SDK is configured with `enableResponseHeaderTracking: false` to
avoid inadvertently capturing `Set-Cookie` or `Authorization`-like
response headers; request headers are tracked but **not** Authorization
by default (SDK's default allowlist).

## Ingestion auth mode

Both AI resources (`safeexchange-staging-insights`,
`safeexchange-backend-insights`) have `DisableLocalAuth = false` —
the ingestion endpoint accepts connection-string / instrumentation-key
writes without requiring an AAD token.

This is the required mode for browser telemetry: a pure SPA cannot
realistically acquire an AAD token for the AI ingestion scope (would
need user consent on the resource, cross-origin token exchange, etc).

The flag defaults to `true` for workspace-based App Insights resources
created via `az monitor app-insights component create`, which is why
our initial browser telemetry deploy produced a `401 Authentication
required` from the ingestion endpoint. Flipping it:

```powershell
az resource update --resource-type "microsoft.insights/components" \
  -n safeexchange-staging-insights -g safeexchange-staging \
  --set properties.DisableLocalAuth=false
```

The residual security exposure (anyone with the connection string can
write to the AI endpoint) is managed by the daily cap (M3), the
authenticated-only wrapper gate (M5), the separate AI per env (M9),
and the ability to rotate the connection string at will (see
`setup.md`).

The backend Function continues to write via managed identity
regardless of this flag — its code path uses the resource-level OAuth
token, not the connection string.

## Killing telemetry quickly

Three escape hatches from widest to narrowest:

1. **Daily cap reached.** App Insights will drop new events until
   reset. Monitor the 90% threshold email.
2. **Flip the flag.** Edit `deployment/.env`, set
   `APPSETTINGS_TELEMETRY_ENABLED_PRD=false`, redeploy. No code
   change needed. Takes about 5 minutes end-to-end.
3. **Rotate the connection string.** In Azure Portal → App Insights
   resource → API Access → regenerate key. The old string becomes
   invalid immediately. Update `.env` with the new string and
   redeploy.

## What to do if telemetry looks hostile

Indicators worth watching:

- Volume spike (jump from baseline; cap-hit notifications)
- Unusual geography in `client_City` / `client_CountryOrRegion`
- Unusual user agents
- Events with high cardinality in custom properties
  (signals someone probing or spamming bogus keys)
- Events with no `user_AuthenticatedId` — indicates a bug in our
  auth gate OR someone calling the ingestion endpoint directly with
  our connection string

Response playbook:

1. Regenerate the connection string (invalidates the attacker key).
2. Flip `APPSETTINGS_TELEMETRY_ENABLED_<ENV>` to `false`, redeploy.
3. Investigate the spike in Log Analytics (`customEvents`,
   `exceptions`, `traces` tables). Filter on `operation_Name`,
   `appId`, and `client_IP` to narrow the attacker's pattern.
4. File a finding in `docs/owasp/` under A09 (logging + alerting).

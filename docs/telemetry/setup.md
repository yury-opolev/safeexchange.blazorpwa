# Telemetry setup — operator runbook

## New environment

When adding a third environment (e.g. `test-eu`), repeat the checklist:

1. **Provision App Insights**
   ```powershell
   az monitor app-insights component create `
     --app safeexchange-<env>-insights `
     --location northeurope `
     --resource-group safeexchange-<env>
   ```

2. **Enable local auth** (required for browser telemetry — see
   `security-considerations.md` "Ingestion auth mode").
   ```powershell
   az resource update --resource-type "microsoft.insights/components" `
     -n safeexchange-<env>-insights -g safeexchange-<env> `
     --set properties.DisableLocalAuth=false
   ```

3. **Set a tight daily cap** (spam mitigation — see
   `security-considerations.md` M3).
   ```powershell
   az monitor app-insights component billing update `
     --app safeexchange-<env>-insights -g safeexchange-<env> --cap 2
   ```
   2 GB/day is plenty for a single-user application; it maps to
   roughly $4.50/day at default pricing and gives a hard ceiling on
   cost-runaway attacks.

4. **Capture the connection string**
   ```powershell
   $cs = az monitor app-insights component show `
     --app safeexchange-<env>-insights `
     --resource-group safeexchange-<env> `
     --query connectionString -o tsv
   ```

5. **Store it in the per-env Key Vault** — this is where the backend
   reads it from, via the standard Azure Key Vault config provider:
   ```powershell
   az keyvault secret set `
     --vault-name safeexchange-<env>-kv `
     --name "WebClientTelemetry--ConnectionString" `
     --value $cs
   ```
   The double dash maps to `WebClientTelemetry:ConnectionString` in
   `IConfiguration`, which is what `SafeExchangeTelemetryConfig`
   reads.

6. **Restart the function app** so the KV config provider picks up
   the new secret on startup:
   ```powershell
   az functionapp restart `
     --name safeexchange-<env> --resource-group safeexchange-<env>
   ```
   Without restart, the secret is picked up at the provider's next
   reload interval (5 minutes by default).

7. **Deploy the client**: `./deployment/deploy-pwa.ps1 -Environment <env>`
   No telemetry-specific env vars in the client deploy — the PWA
   fetches config from the backend at runtime.

8. **Verify** in the browser:
   - Log in. Open DevTools → Network.
   - Filter by `telemetry/config` — should see a single `GET` to
     `/api/v2/telemetry/config` returning 200.
   - Filter by `applicationinsights.azure.com` — should see at least
     one `POST /v2/track` returning 200 per session (for the
     `SessionStarted` event).
   - Filter by `user_AuthenticatedId == <your oid>` in Log Analytics
     to confirm your events arrive tagged correctly.

## Rotating the connection string

If the current connection string is suspected of being abused (see
`security-considerations.md` "What to do if telemetry looks hostile"):

1. Regenerate the instrumentation key in the Azure Portal (App
   Insights resource → API Access → Regenerate), or via:
   ```powershell
   # Returns the new connection string on stdout
   az monitor app-insights component create `
     --app safeexchange-<env>-insights `
     --resource-group safeexchange-<env> `
     --location <region> `
     --force `
     --query connectionString -o tsv
   ```
   The old connection string becomes invalid immediately at the AI
   side.

2. Update the Key Vault secret:
   ```powershell
   az keyvault secret set `
     --vault-name safeexchange-<env>-kv `
     --name "WebClientTelemetry--ConnectionString" `
     --value "<new-connection-string>"
   ```

3. Restart the function app (or wait ≤ 5 minutes):
   ```powershell
   az functionapp restart `
     --name safeexchange-<env> --resource-group safeexchange-<env>
   ```

4. **No client redeploy needed.** Browsers that reload fetch the new
   string from `/v2/telemetry/config` on their next sign-in.

## Disabling telemetry for an environment

Delete the Key Vault secret and restart the function app. The
backend will return `{ enabled: false, connectionString: "" }` to
every client call, and the PWA's `TelemetryService` will skip SDK
initialisation entirely — no data can leave the browser.

```powershell
az keyvault secret delete `
  --vault-name safeexchange-<env>-kv `
  --name "WebClientTelemetry--ConnectionString"

az functionapp restart `
  --name safeexchange-<env> --resource-group safeexchange-<env>
```

To re-enable, run the `az keyvault secret set` from the rotation
recipe above. No client change.

## Local development

Source `appsettings.json` has **no** `Telemetry` section. Running
`dotnet run` locally points `BackendApi.BaseAddress` at the staging
function app (that's the shipped default). The locally running PWA,
once you sign in, will fetch the staging connection string from
staging's backend and emit into the staging AI instance — which is
the same thing that happens when browsing staging directly.

If you want a separate local-dev AI instance, provision one and
store its connection string in the staging KV under a different
name, then branch the backend handler on a host/header check. That
is a bigger change and not covered here.

## Common operator questions

**Q. I added a new ILogger call in my component — does it go to AI?**
Only if the log level is `Warning` or higher. See
`TelemetryLogger.IsEnabled`. If you need lower levels, either use
`TelemetryService.TrackEventAsync` directly or lower the threshold
in `TelemetryLogger.IsEnabled`.

**Q. How do I add a custom property to every event?**
Edit `TelemetryService.WithSessionCorrelation`. It's the single
merge point for event-level properties.

**Q. How do I find one user's full session?**
In Log Analytics:
```kusto
union customEvents, exceptions, traces, pageViews
| where customDimensions["saex.sessionId"] == "<session guid>"
| order by timestamp asc
```

Or filter on `user_AuthenticatedId` if you know the user's oid.
Users can copy their current `saex.sessionId` out of the user
dropdown menu in the PWA (the Session id line above Sign out).

**Q. How are client and backend correlated?**
W3C Trace Context. The JS SDK adds `traceparent` headers to
outgoing fetches automatically. The backend (server-side AI SDK)
picks these up and stitches operations together. Look at the
"End-to-end transaction" view in App Insights.

**Q. Why bundle the AI SDK locally instead of loading from the CDN?**
So `script-src 'self'` in our CSP stays strict. The CDN
`https://js.monitor.azure.com` is still allowlisted in `connect-src`
for the SDK's remote config fetch, but not in `script-src`. See
`security-considerations.md` M10.

**Q. Why an authenticated endpoint instead of putting the connection
string in `appsettings.json`?**
To keep the AI ingestion credential out of the public PWA bundle.
Anyone on the internet can `curl` `appsettings.json`; only tenant
members can acquire the bearer token needed to read
`/v2/telemetry/config`. See `security-considerations.md` M1.

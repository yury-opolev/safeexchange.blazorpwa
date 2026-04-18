# Telemetry setup — operator runbook

## New environment

When adding a third environment (e.g. `test-eu`), repeat the checklist:

1. **Provision App Insights**
   ```powershell
   az monitor app-insights component create \
     --app safeexchange-<env>-insights \
     --location northeurope \
     --resource-group safeexchange-<env>
   ```

2. **Set a tight daily cap** (spam mitigation — see
   `security-considerations.md` M3).
   ```powershell
   az monitor app-insights component billing update \
     --app safeexchange-<env>-insights -g safeexchange-<env> --cap 2
   ```
   2 GB/day is plenty for a single-user application; it maps to
   roughly $4.50/day at default pricing and gives a hard ceiling on
   cost-runaway attacks.

3. **Capture the connection string**
   ```powershell
   az monitor app-insights component show \
     --app safeexchange-<env>-insights \
     --resource-group safeexchange-<env> \
     --query connectionString -o tsv
   ```

4. **Add to `deployment/.env`** (gitignored — never commit):
   ```
   APPSETTINGS_TELEMETRY_ENABLED_<ENV>=true
   APPSETTINGS_TELEMETRY_CONNECTION_STRING_<ENV>=InstrumentationKey=...;IngestionEndpoint=...;LiveEndpoint=...;ApplicationId=...
   ```

5. **Deploy**: `./deployment/deploy-pwa.ps1 -Environment <env>`

6. **Verify** in the browser:
   - Log in. Open devtools → Network.
   - Confirm a POST to `*.in.applicationinsights.azure.com` fires
     (not before login — the auth gate should block it for
     anonymous sessions).
   - In the AI portal, open Live Metrics and confirm events arrive.
   - Filter events by `user_AuthenticatedId == <your oid>`.

## Rotating the connection string

If the current connection string is suspected of being abused (see
`security-considerations.md` "What to do if telemetry looks hostile"):

1. In Azure Portal → App Insights resource → API Access → **Create
   API key** (or regenerate the existing instrumentation key via
   `az monitor app-insights component create` with `--force`).
   Note: regenerating the Instrumentation Key creates a new GUID;
   the old one stops working immediately.

2. Update `deployment/.env`:
   ```
   APPSETTINGS_TELEMETRY_CONNECTION_STRING_<ENV>=InstrumentationKey=<new-guid>;...
   ```

3. Redeploy:
   ```powershell
   ./deployment/deploy-pwa.ps1 -Environment <env>
   ```

4. The PWA will pick up the new string on next page load. AFD
   purge happens at the end of the deploy; clients with a cached
   `appsettings.json` will switch over when the cache expires.

## Disabling telemetry for an environment

1. Edit `deployment/.env`:
   ```
   APPSETTINGS_TELEMETRY_ENABLED_<ENV>=false
   ```
   (Leave the connection string in place — it's ignored when
   disabled.)

2. Redeploy: `./deployment/deploy-pwa.ps1 -Environment <env>`

3. The PWA now sees `Telemetry.Enabled = false` in
   `appsettings.json` and `TelemetryService.InitializeAsync`
   returns without calling the SDK. Nothing can leave the browser.

## Local development

Source `appsettings.json` ships with:
```json
"Telemetry": {
  "Enabled": false,
  "ConnectionString": ""
}
```

This means `dotnet run` from a dev machine emits no telemetry by
default. If you want to debug telemetry locally, temporarily flip
`Enabled` to `true` and paste the **staging** connection string
(never prod). Restore before committing.

Better: use a separate dev App Insights resource if you do this
regularly.

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

**Q. How are client and backend correlated?**
W3C Trace Context. The JS SDK adds `traceparent` headers to
outgoing fetches automatically. The backend (server-side AI SDK)
picks these up and stitches operations together. Look at the
"End-to-end transaction" view in App Insights.

**Q. Why bundle the AI SDK locally instead of loading from the CDN?**
So `script-src 'self'` in our CSP stays strict. The CDN
`https://js.monitor.azure.com` would require a CSP exception. See
`security-considerations.md` M10.

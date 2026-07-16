# Build-time version stamping

The web client shows its version in the account menu (`SafeExchange.PWA/Shared/LoginDisplay.razor`),
read at runtime from `wwwroot/version.json`:

```json
{ "version": "1.0.16", "buildDate": "2026-07-16" }
```

## Setting the version from a build pipeline

Any external pipeline (Azure DevOps, GitHub Actions, ...) can stamp the version
at build time without editing a single source file, by passing the `PwaVersion`
MSBuild property:

```bash
dotnet publish SafeExchange.PWA -c Release -p:PwaVersion=2.3.4
```

The published `wwwroot/version.json` — and therefore the running UI — then reports
`2.3.4`. Optionally pin the stamped date:

```bash
dotnet publish SafeExchange.PWA -c Release -p:PwaVersion=2.3.4 -p:PwaBuildDate=2026-07-16
```

`PwaBuildDate` defaults to the UTC build date when omitted.

### Azure DevOps example

```yaml
- script: dotnet publish SafeExchange.PWA -c Release -p:PwaVersion=$(Build.BuildNumber)
  displayName: Publish web client
```

### GitHub Actions example

```yaml
- run: dotnet publish SafeExchange.PWA -c Release -p:PwaVersion=${{ github.run_number }}
```

## How it works

`Directory.Build.targets` (repo root) defines the `StampPwaVersion` target. It runs
`BeforeTargets="BeforeBuild"` and rewrites `wwwroot/version.json` **before** the build
computes the static-web-asset fingerprints and the service-worker integrity manifest,
so the cache-busting hashes shipped to the browser match the stamped content.

The target is **inert unless `PwaVersion` is supplied**, so:

- a plain `dotnet build` / `dotnet run` leaves `version.json` untouched;
- `deployment/deploy-pwa.ps1` keeps its existing auto-bump-and-commit behaviour
  (it does not pass `PwaVersion`).

Only the project that owns `wwwroot/version.json` (the PWA host) is affected; the
target is a no-op in every other project in the tree. A repo that consumes this
one can reuse the same `Directory.Build.targets` and `version.json` layout to get
the same build-time knob.

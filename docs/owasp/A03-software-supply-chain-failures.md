# A03:2025 — Software Supply Chain Failures

**Findings:** 3 · **Highest priority:** P2

---

## Package Inventory (Directory.Packages.props)

| Package | Pinned Version | Note |
|---|---|---|
| Microsoft.AspNetCore.Components.Web | 8.0.1 | more recent 8.0.x servicing rollups exist |
| Microsoft.AspNetCore.Components.WebAssembly | 8.0.1 | same |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 8.0.1 | dev-only |
| Microsoft.Authentication.WebAssembly.Msal | 8.0.1 | security-sensitive |
| Microsoft.Bcl.HashCode | 1.1.1 | legacy polyfill, no-op on net8.0 |
| Microsoft.Extensions.Configuration | 8.0.0 | `.0` baseline, not latest 8.0.x |
| Microsoft.Extensions.Configuration.Binder | 8.0.1 | |
| Microsoft.Extensions.Http | 8.0.0 | `.0` baseline |
| Microsoft.Extensions.Http.Polly | 8.0.1 | |
| System.ComponentModel.Annotations | 5.0.0 | facade on modern TFM |
| System.Net.Http.Json | 8.0.0 | `.0` baseline |
| System.Text.Encodings.Web | 8.0.0 | `.0` baseline |
| System.Text.Json | 8.0.5 | current 8.0.x |

Frontend bundled assets (same-origin only, no external CDN): Bootstrap 5.3.3 (current), Bootstrap Icons 1.11.3 (current), **jQuery 3.3.1 slim (outdated — see below)**, Quill 2.0.3 (reasonably current).

---

## [P2] [MEDIUM] jQuery 3.3.1 slim shipped to production (CVE-2020-11022, CVE-2020-11023)

- **Category:** A03:2025 — Software Supply Chain Failures
- **CWE:** CWE-1104 (Use of Unmaintained Third-Party Components), CWE-79 (reachable via library)
- **File:** `SafeExchange.Client.Web.Components/wwwroot/js/jquery-3.3.1.slim.min.js`; included at `SafeExchange.PWA/wwwroot/index.html:41`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Internet · **Confidence:** High · **Priority: P2**

**Evidence:**

```
/*! jQuery v3.3.1 -ajax,-ajax/jsonp,... */
```
```html
<script src="_content/SafeExchange.Client.Web.Components/js/jquery-3.3.1.slim.min.js"></script>
```

**Description:** CVE-2020-11022 and CVE-2020-11023 (fixed in jQuery 3.5.0) affect `jQuery.htmlPrefilter` — which is present in the **slim** build because the slim build removes only `ajax`/`effects`, not `manipulation`. Any caller passing untrusted HTML to `.html()`, `.append()`, or `$(html)` is vulnerable to XSS via mis-parsed self-closing tags.

**Attack Scenario:**
1. Attacker places crafted HTML (e.g. `<option><style></option></select><img src=x onerror=...>`) into any user-reachable string that eventually reaches a jQuery DOM-manipulation API.
2. Because jQuery 3.3.1's `htmlPrefilter` mis-parses the input, the injected payload is appended to the DOM as executable markup.
3. JavaScript executes in the origin of the SafeExchange PWA — token theft via MSAL storage, API request forgery, account impersonation.

**Recommendation:** **Remove jQuery entirely** if no current code path uses `$`. Bootstrap 5.3.3 does not require jQuery; Quill 2 does not require jQuery. Delete the `<script>` tag from `index.html` and remove the file from `wwwroot`. If jQuery is still needed, upgrade to 3.7.1 (current stable).

**Assumption:** If no Blazor code path feeds untrusted HTML to any jQuery API, the residual CVE impact drops to zero — but an unused outdated dependency loaded on every page is gratuitous attack surface, so remove it.

---

## [P2] [HIGH] No CI/CD supply-chain controls — empty `.github/workflows/`, no Dependabot, no SBOM, no SCA

- **Category:** A03:2025 — Software Supply Chain Failures
- **CWE:** CWE-1357 (Reliance on Insufficiently Trusted Software Component), CWE-829
- **Files:**
  - `.github/workflows/` — directory exists but is **empty**
  - No `.github/dependabot.yml`
  - No `renovate.json`
  - No `packages.lock.json` in any project
  - No SBOM artifact, no `cyclonedx`/`syft`/`trivy`/`osv-scanner` integration
- **Severity:** High · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** Confirmed · **Priority: P2**

**Description:** A PWA handling authenticated access to a secrets exchange has **zero automated patch pipeline**. New CVEs in `Microsoft.Authentication.WebAssembly.Msal` or `Microsoft.AspNetCore.Components.WebAssembly` would generate no bump PR. No `packages.lock.json` means transitive dependency versions are non-reproducible; local restore can float across semver ranges; subtle supply-chain drift between a developer machine and a production build is invisible.

**Attack Scenario:**
1. A new CVE is published for `Microsoft.Authentication.WebAssembly.Msal` (MSAL has historically had advisories around redirect handling and token caching).
2. With no Dependabot and no CI, the vulnerable version persists on `main` indefinitely. Contributors have no signal.
3. Because there is no `packages.lock.json`, non-reproducible restores mask drift.
4. At deployment, the vulnerable library ships to every PWA user; attacker exploits the known issue.

**Recommendation (concrete steps):**
1. Create `.github/dependabot.yml` with `package-ecosystem: nuget` and `package-ecosystem: github-actions` (weekly).
2. Add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to `Directory.Build.props`, commit the generated `packages.lock.json`, and run CI with `dotnet restore --locked-mode`.
3. Create `.github/workflows/ci.yml` with `permissions: { contents: read }`, third-party actions SHA-pinned, and a `dotnet list package --vulnerable --include-transitive` step that fails on any finding.
4. Add `dotnet CycloneDX` (or `CycloneDX/gh-dotnet-generate-sbom`) for SBOM generation, artifact upload on release.
5. Optionally add `google/osv-scanner-action` (SHA-pinned) to catch vulnerable transitive dependencies.
6. Enable branch protection on `main` — required reviewers, required status checks, no force-push.

**Assumption:** If the repository is mirrored from another source that runs CI centrally, severity drops to Medium. Not observed — this repo is the primary.

---

## [P3] [LOW] `Microsoft.Extensions.*` packages pinned at `8.0.0` baseline

- **Category:** A03:2025 — Software Supply Chain Failures
- **CWE:** CWE-1104
- **File:** `Directory.Packages.props:11-14`
- **Severity:** Low · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** Medium · **Priority: P3**

Four packages (`Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Http`, `System.Net.Http.Json`, `System.Text.Encodings.Web`) are pinned at `8.0.0` — the GA baseline, not the latest 8.0.x servicing build. Historically, the `.0` baseline of these packages skips multiple Microsoft servicing rollups that fold in advisory fixes for parsing, DoS, and downstream dependencies.

**Recommendation:** Bump to the latest 8.0.x (minimum 8.0.1 of each). Automate via Dependabot (see the finding above).

**Assumption:** Because the client is Blazor WASM and never deserializes untrusted remote configuration through these binders, reachable impact is low today. A future dependency with reachable parsing of attacker-controlled JSON would elevate severity.

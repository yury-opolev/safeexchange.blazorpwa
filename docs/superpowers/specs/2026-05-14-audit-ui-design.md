# Audit Log UI — Design

**Status:** Approved
**Date:** 2026-05-14
**Author:** yury-opolev

## Goal

Surface the new backend audit API (`GET /v2/secret/{secretId}/audit`) in the Blazor PWA so users with `Read` permission on an audit-enabled secret can answer "who did what to this secret, and when?" — without ever exposing secret content.

## Scope

1. **Opt-in at creation.** `CreateData` gains an "Enable audit log" checkbox (default **on**). The flag is sent in `MetadataCreationInput.AuditEnabled`. Inline help text states the choice is immutable.
2. **ViewData affordance.** When the loaded secret's `auditEnabled = true`, an "Audited" badge is shown next to the secret name; a new `[Audit log]` button in the action group navigates to `/auditlog/{name}`. When `false`, neither badge nor button appears.
3. **Dedicated audit page.** New route `/auditlog/{name}` renders a paginated, newest-first log with server-side date filters, client-side type/actor filters, and a "raw chunk reads" toggle. Empty-state when `auditEnabled = false` ("Audit was not enabled when this secret was created.").
4. **No hash recomputation in browser.** Hashes are surfaced (truncated, with copy-to-clipboard) on each row's expanded detail. Chain verification is offline / forensic, per the backend spec.

## Non-goals

- Editing `auditEnabled` post-create. Backend forbids; UI does not surface it.
- Viewing audit of a deleted-and-recycled name. Backend does not expose it.
- Exporting the log (CSV/JSON). Out of v1 — can be a follow-up.
- Auditing of `GET /v2/secret/{id}/audit` itself (reading an audit log does not emit an event).

## Backend addition (small)

The backend `GET /v2/secret/{secretId}/audit` currently pages ascending by `SequenceNumber`. The UI needs newest-first; we add support for descending direction.

### `direction` query param

| Name | Type | Default | Notes |
|---|---|---|---|
| `direction` | `"asc"` \| `"desc"` | `"asc"` | When `"desc"`, page in descending sequence order. |

When `direction = "desc"`:
- Query becomes `OrderByDescending(SequenceNumber)`.
- Continuation predicate is `SequenceNumber < afterVal` (vs. `>` for asc).
- The page (before merging) is **reversed in memory** so it is ascending when handed to `ContentReadMerger.Merge` (the merger's contract — same-actor-same-content runs — assumes ascending input). The merged list is **reversed again** before being returned in the response. This keeps the merger pure / order-agnostic and avoids duplicating its logic.
- Continuation token format unchanged: base64 of the last in-page `SequenceNumber`. Direction is part of the request, not the token; the client always re-sends `direction=desc` along with the continuation.

`api-endpoints.md` is updated to document the param.

### Tests (backend)

New tests in `SafeExchange.Tests/Tests/`:

- `SecretAuditDirectionTests.cs` — verifies asc/desc pagination, continuation correctness in both directions, merge correctness when desc.

Existing audit tests are untouched; default behavior (`direction` omitted) remains ascending.

## Web-client changes

### DTOs (`SafeExchange.Client.Common/Model/Dto/Output/`)

New:
- `SecretAuditEventOutput.cs` — mirrors backend DTO fields: `EventType`, `OccurredAt`, `FirstAt?`, `LastAt?`, `SequenceNumber`, `SequenceFrom?`, `SequenceTo?`, `Actor` (subjectType / id / name), `ContentId?`, `ChunkIds?`, `Payload` (raw `JsonElement`), `Hash`, `PrevHash`.
- `SecretAuditPageOutput.cs` — `AuditEnabled`, `Events`, `NextContinuation`.
- `ActorOutput.cs` — `SubjectType`, `SubjectId`, `SubjectName`.

Modified:
- `ObjectMetadataOutput.cs` — adds `AuditEnabled` bool.

### Input DTO

- `MetadataCreationInput.cs` — adds nullable `bool? AuditEnabled` so existing call sites (without opt-in) keep working.

### Model

- `ObjectMetadata.cs` — adds `AuditEnabled` (bool, default `true` for new creations). Sourced from `ObjectMetadataOutput.AuditEnabled` on the read constructor. `ToCreationDto()` now sets `AuditEnabled` on the DTO.

### `ApiClient` (`SafeExchange.Client.Common/ApiClient/ApiClient.cs`)

Add:
```csharp
public async Task<BaseResponseObject<SecretAuditPageOutput>> GetSecretAuditAsync(
    string secretId,
    string? direction = null,
    DateTime? from = null,
    DateTime? to = null,
    int? pageSize = null,
    string? continuation = null,
    bool raw = false);
```

Construction:
- URL: `{ApiVersion}/secret/{secretId}/audit` with query-string args.
- Dates serialized as ISO-8601 UTC round-trip (`"O"`).
- All params optional; only non-null appear in the query string.
- Returns `BaseResponseObject<SecretAuditPageOutput>`.

### Pages — Create

`SafeExchange.Client.Web.Components/Pages/CreateData.razor`:

- New `<InputCheckbox>` "Enable audit log for this secret", bound to a local `bool auditEnabled`.
  - **Client default:** `true`. The UI sends `AuditEnabled = true` unless the user unticks the box.
  - Backend DTO stays `bool?` so older clients (no field) keep their pre-feature behaviour (`false` on the server). The web client always sends an explicit value.
- Inline help text: "Audit logging records every meaningful action on this secret. This choice cannot be changed later."
- On submit, the bool flows into `compoundModel.Metadata.AuditEnabled` → `ToCreationDto().AuditEnabled`.

### Pages — View

`SafeExchange.Client.Web.Components/Pages/ViewData.razor`:

- When `this.compoundModel.Metadata.AuditEnabled` is true:
  - Render a small `<span class="badge bg-info-subtle text-info-emphasis ms-2">Audited</span>` next to the page header / name area. Tooltip: "Audit logging is enabled for this secret."
  - Add `[Audit log]` to the action group, between Refresh and Edit:
    ```html
    <button class="btn btn-outline-primary" type="button"
            @onclick="@(() => NavManager.NavigateTo($"auditlog/{this.ObjectName}"))">
      <i class="bi bi-clipboard-data"></i>&nbsp;Audit log
    </button>
    ```
- When false: badge and button are not rendered.

### New page — Audit log

`SafeExchange.Client.Web.Components/Pages/AuditLog.razor` + `AuditLog.razor.css`:

- Public parameter: `[Parameter] public string ObjectName { get; set; }`.
- Loads on mount: `apiClient.GetSecretAuditAsync(ObjectName, direction: "desc", raw: false, pageSize: 100)`.
- States:
  - `auditEnabled = false` → empty state with explanatory text and link back to `/viewdata/{name}`.
  - `auditEnabled = true, events.Count == 0` → "No events recorded yet."
  - `auditEnabled = true` → render filter bar + event list.
- Filter bar:
  - From / To (datetime-local) → triggers reload with `from`/`to` query params, clearing pagination state.
  - Event type checkboxes (default: all on) → client-side filter on the loaded pages.
  - Actor search (free-text) → client-side substring match on `actor.subjectName` and `actor.subjectId`.
  - "Raw chunk reads" toggle → triggers reload with `raw=true` and clears pagination.
- Pagination:
  - Initial load fetches page 1 (desc, newest-first, 100 events).
  - "Load older" button at the bottom uses `NextContinuation`.
  - Loaded pages are concatenated in display order (newest at top, older below).
- Row layout: compact one-line row, click-to-expand inline detail (see "Row rendering" below).
- Header: shows secret name + breadcrumb back to `/viewdata/{name}`.

### Row rendering

Each event row is a `<li class="list-group-item">` with:

- Caret icon (`bi-chevron-right` / `bi-chevron-down` rotated on expand).
- Time column (`OccurredAt` for non-merged; `FirstAt` for merged ContentRead).
- Event type pill: colour-coded by category — lifecycle (`Created/Updated/Deleted`) neutral, permission (`PermissionGranted/Revoked`) primary, content (`ContentRead/Written/Committed`) info, access-request warning.
- Actor: `SubjectName` (fallback `SubjectId`).
- Summary: per-event-type one-liner:
  - `SecretCreated` → tag count + `auditEnabled: true`
  - `SecretMetadataUpdated` → list of changed fields
  - `SecretDeleted` → "(deleted)"
  - `PermissionGranted` / `PermissionRevoked` → `target.subjectName + diff of flags`
  - `ContentRead` (merged) → `contentId · {ChunkIds.Count} chunks · firstAt → lastAt`
  - `ContentRead` (raw) → `contentId · chunkId`
  - `ContentWritten` → `contentId · chunkId`
  - `ContentCommitted` → `fileName (contentId)`
  - `AccessRequested` / `AccessRequestApproved` / `AccessRequestDenied` → `requestor.subjectName + permission diff`

Expanded detail (Bootstrap `collapse`):
- Pretty-printed payload JSON (`<pre><code>` block).
- Sequence number(s).
- Hash and PrevHash (truncated to 12 chars + copy button each).

### Routing / shim

`SafeExchange.PWA/Pages/AuditLog.razor`:

```razor
@page "/auditlog/{objectName}"

@using Microsoft.AspNetCore.Authorization

@attribute [Authorize]

<SafeExchange.Client.Web.Components.Pages.AuditLog ObjectName=@this.ObjectName />

@code {
    [Parameter]
    public string ObjectName { get; set; }
}
```

### Service registration

No new singletons / scoped services required. Implementation lives in the existing `ApiClient` and a new component.

### Telemetry

`AuditLog.razor` `OnInitializedAsync` emits `AuditLogViewed`:

```csharp
await this.Telemetry.TrackEventAsync("AuditLogViewed", new Dictionary<string, string>
{
    ["name"] = this.ObjectName,
});
```

Failure path: a separate `AuditLogLoadFailed` event with `status` from the response. No PII beyond the secret name.

## Testing

### Backend (`SafeExchange.Tests/Tests/`)

- **`SecretAuditDirectionTests.cs`** — new file:
  - `Direction_Default_IsAscending`
  - `Direction_Desc_ReturnsNewestFirst`
  - `Direction_Desc_ContinuationWalksToOlderEvents`
  - `Direction_Desc_MergesContentReadCorrectly`

### Web client (`SafeExchange.Client.Common/Tests/` or new `SafeExchange.PWA.Tests/`)

The blazorpwa repo currently has **no test project**. We add a minimal one only if necessary:

- `ApiClient.GetSecretAuditAsync` URL-building unit test (verifies query-string encoding for combinations of `direction`, `from`, `to`, `pageSize`, `continuation`, `raw`).

For Blazor component logic (filter application, merge-toggle reload), this is exercised manually in a smoke test in the staging environment — adding a Blazor component test harness (bUnit) is out of scope for this change.

## Manual / smoke test plan (staging)

1. Create an audit-enabled secret via the new checkbox. Verify the `Audited` badge appears on `ViewData` immediately after navigating back.
2. Read it (download the main content + an attachment). Open the audit log; confirm `ContentRead` rows appear, merged.
3. Toggle "Raw chunk reads"; confirm per-chunk events.
4. Grant access to another principal. Confirm `PermissionGranted` row.
5. Filter by date / event type / actor; confirm rows hide as expected.
6. Click "Load older"; confirm older events stream in.
7. Create a secret with the checkbox **unchecked**. Open `ViewData`; confirm no badge, no button. Try to navigate to `/auditlog/{name}` directly; confirm empty-state.
8. Confirm `auditEnabled` is invariant — there's no UI to toggle it on an existing secret.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Backend `direction=desc` regresses existing asc pagination. | Default behavior preserved (param absent ⇒ asc). New behaviour tested in isolation. |
| Merge logic produces wrong ranges when fed reversed pages. | Handler reverses the page back to ascending before calling `ContentReadMerger.Merge`, then reverses the merged result. Existing merger unit tests remain valid. |
| Large logs flood the page. | Server-side `pageSize` cap (500) + client-side type/actor filters + "Load older" pagination. |
| Hash columns add visual noise. | Hashes live in the row's expanded detail, not the row summary; copy-to-clipboard pattern matches the rest of the app. |
| `auditEnabled = false` users hit the page directly. | Backend already returns `{auditEnabled: false, events: []}` for that case; component renders an explanatory empty state with a link back. |
| Recycled-secret-name confusion (old audit invisible). | Documented backend behaviour. UI shows the live instance's log only; users with forensic needs go to the operator. |

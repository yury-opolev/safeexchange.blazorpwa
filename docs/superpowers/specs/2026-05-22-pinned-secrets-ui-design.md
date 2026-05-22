# Pinned Secrets — UI Design

**Status:** Approved
**Author:** yury-opolev (with Claude)
**Date:** 2026-05-22
**Branch:** `main` (feature branch to be created)
**Backend reference:** `safeexchange` commit `db70bb1` — `feat(pinned-secrets): per-user pinned secrets for main-page favourites`

## Summary

Uptake the backend pinned-secrets feature in the Blazor PWA. A user can pin up to N secrets
(default 5, server-configured) as favourites. Pinned secrets surface as a quick-access list on
the Home page. Pin/unpin is a personal, per-user action that does not touch the secret's data,
permissions, or audit log. The UI mirrors the existing **pinned groups** pattern for both code
structure and the click-to-toggle star visual.

## Motivation

The Home page is currently just a search box; the "My Secrets" page lists everything with no
notion of favourites. Pinning gives the user a fast-access surface for frequently used secrets
without changing the authorisation model.

## Backend contract (already shipped)

All endpoints are `v2`, Bearer-authenticated, user-only (Applications get `403`).

| Method | Path | Behaviour |
|---|---|---|
| GET | `/v2/pinnedsecrets-list` | List caller's pins, newest-pin first. `no_content` + empty when none. |
| GET | `/v2/pinnedsecrets/{secretId}` | `PinnedSecretOutput` when pinned; `no_content` + null `result` when not pinned. |
| PUT | `/v2/pinnedsecrets/{secretId}` | Pin. Requires `Read`. `400` when at/over cap. Idempotent (re-PUT returns existing). **Empty request body.** |
| DELETE | `/v2/pinnedsecrets/{secretId}` | Unpin. Idempotent — `no_content` when nothing to remove, `ok` when removed. |

`PinnedSecretOutput` (camelCase JSON):

```json
{
  "secretName": "prod-db-creds",
  "exists": true,
  "canRead": true,
  "canWrite": false,
  "canGrantAccess": false,
  "canRevokeAccess": false,
  "tags": ["prod", "database"]
}
```

Three render states derived from the DTO:

| State | Condition | UI treatment |
|---|---|---|
| **Live** | `exists && canRead` | Name links to `viewdata/{name}`; tags shown; unpin star. |
| **Access-lost** | `exists && !canRead` | Greyed; "no access" text; "Request access" link; unpin star. (`tags` is empty per backend.) |
| **Deleted** | `!exists` | Greyed; strikethrough name; "deleted" text; only the unpin star is actionable. |

The cap-rejection `PUT` returns `400` with a message like:
`"Pinned secret count is {N}, which is higher or equal than allowed no. of {Max} pinned secrets. Please unpin secrets before adding new ones."`

## Client architecture

Mirrors the pinned-groups implementation already in the repo.

### New types — `SafeExchange.Client.Common`

- `Model/Dto/Output/PinnedSecretOutput.cs` — DTO matching the backend
  (`SecretName`, `Exists`, `CanRead`, `CanWrite`, `CanGrantAccess`, `CanRevokeAccess`, `Tags`).
- `Model/PinnedSecret.cs` — domain model constructed from the DTO, exposing a derived
  `PinnedSecretState State` (`Live` / `AccessLost` / `Deleted`).
- `Model/PinnedSecretState.cs` — the enum (one type per file, per house style).

### ApiClient — `SafeExchange.Client.Common/ApiClient/ApiClient.cs`

New `#region pinned secrets`, following the pinned-groups region:

```csharp
Task<BaseResponseObject<List<PinnedSecretOutput>>> ListPinnedSecretsAsync();
Task<BaseResponseObject<PinnedSecretOutput>>       GetPinnedSecretAsync(string secretId);
Task<BaseResponseObject<PinnedSecretOutput>>       PutPinnedSecretAsync(string secretId);   // empty body
Task<BaseResponseObject<string>>                   DeletePinnedSecretAsync(string secretId);
```

`PutPinnedSecretAsync` sends `PUT` with no body (the secret name is fully in the URL). Use a bare
`HttpRequestMessage(HttpMethod.Put, ...)` since there is no input DTO to serialize.

### Shared state — `StateContainer`

Add `HashSet<string> PinnedSecretNames` (case-sensitive — secret names are case-sensitive in this
system). Populated from `ListPinnedSecretsAsync`. View and My-Secrets pages read this set to render
star state without a per-row `GET`. Mutated through the helper so it stays in sync.

### Toggle helper — `Helpers/PinnedSecretsHelper.cs`

Mirrors `GroupsHelper.SwitchGroupPinAsync`:

```csharp
static Task<BaseResponseObject<...>> SwitchSecretPinAsync(
    ApiClient apiClient, StateContainer stateContainer, string secretName, bool newPinValue);
```

- `newPinValue == true` → `PutPinnedSecretAsync`; on `ok`, add name to `PinnedSecretNames`.
- `newPinValue == false` → `DeletePinnedSecretAsync`; treat `no_content` as success
  (the `GroupWasUnpinnedPreviously` analogue); on success remove name from `PinnedSecretNames`.
- Returns the response so callers can surface a `400` cap error.

### The star icon

Reuse the exact pinned-groups visual (`ItemSearchDialog`):

- not pinned → `<i class="bi bi-star"></i>`
- pinned → `<i class="bi bi-star-fill"></i>`
- in progress → `<span class="spinner-border spinner-border-sm" role="status"></span>`

Rendered as a `btn btn-link` (text-primary). Optimistic toggle with spinner; reverts on error.

## Surfaces

### Home — `SafeExchange.Client.Web.Components/Pages/Index.razor`

- A new **Pinned** `list-group` section rendered **below** the existing search box.
- On `OnInitializedAsync`, fetch `ListPinnedSecretsAsync`; map to `PinnedSecret` models; also seed
  `StateContainer.PinnedSecretNames`.
- One row per pin, rendered by `State`:
  - **Live** — name is a link to `viewdata/{name}`; tags as small muted text; filled star unpins.
  - **Access-lost** — greyed row, "no access" text, small "Request access" link to
    `addrequest?subject={name}&permission=Read`; filled star unpins.
  - **Deleted** — greyed, strikethrough name, "deleted" text; filled star unpins.
- Empty list → section hidden entirely (Home stays clean).
- A subtle `N / Max` counter near the section header. (Max known only after a list fetch; show
  the count of current pins; the cap number comes from config-less client knowledge, so display
  just the current count, e.g. "Pinned (3)", and rely on the cap error for the ceiling. See
  Open questions.)
- Unpin from Home refetches in place (or removes the row optimistically) and updates the counter.

### My Secrets — `SafeExchange.Client.Web.Components/Pages/ListData.razor`

- Fetch `ListPinnedSecretsAsync` alongside the secret list (or read `StateContainer.PinnedSecretNames`
  if already populated) to know which rows are pinned.
- Add a star icon to each row (near the existing button group) that toggles pin in place via the helper.
- A `400` cap error surfaces as the page's existing `NotificationData` warning alert; the star reverts.

### View — `SafeExchange.Client.Web.Components/Pages/ViewData.razor`

- On load, determine pinned state from `StateContainer.PinnedSecretNames` (populated by a
  `ListPinnedSecretsAsync` call if the set is empty/unknown).
- Add a star toggle in the action bar beside Refresh / Edit / Give up.
- Cap `400` surfaces via the existing `NotificationData` pattern.

## Cap handling

The only failure that needs explicit UX is the `PUT` `400` cap rejection. The helper returns the
response; each calling page renders the backend message as a dismissible `alert` notification and
reverts the star to empty. No client-side cap pre-check (the client does not know `Max`; the server
is the source of truth).

## Error & edge handling

- `GET /pinnedsecrets/{id}` `no_content` (not pinned) → empty star, no error.
- `DELETE` `no_content` (already unpinned) → treated as success.
- Network/`exception` status on toggle → revert optimistic change, surface a warning notification.
- `AccessTokenNotAvailableException` on any fetch → `exception.Redirect()` (existing pattern).

## Testing strategy (TDD, red→green)

Unit-test the new client surface (no Blazor component runtime needed), mirroring the existing
give-up `ApiClient` tests:

- **ApiClient** (mocked `HttpMessageHandler`):
  - `ListPinnedSecretsAsync` parses a list and maps `ok`.
  - `GetPinnedSecretAsync` maps a pinned DTO and the `no_content`/null case.
  - `PutPinnedSecretAsync` issues `PUT` with no body; maps `ok`; maps `400` cap error
    (status + error message preserved).
  - `DeletePinnedSecretAsync` maps `ok` and `no_content`.
- **PinnedSecretsHelper**:
  - pin success adds to `PinnedSecretNames`.
  - unpin success (`ok`) removes from `PinnedSecretNames`.
  - unpin `no_content` treated as success (still removed / idempotent).
  - pin `400` cap error: name **not** added; error response returned to caller.
- **PinnedSecret model**: `State` derivation for all three flag combinations.

Each test written failing first (red), then implemented to green. Component (`.razor`) markup is
verified manually by running the app, consistent with how the rest of the UI is validated in this
repo (no bUnit harness currently present).

## File layout

### New files

| Path | Purpose |
|---|---|
| `SafeExchange.Client.Common/Model/Dto/Output/PinnedSecretOutput.cs` | Backend DTO |
| `SafeExchange.Client.Common/Model/PinnedSecret.cs` | Domain model + `State` |
| `SafeExchange.Client.Common/Model/PinnedSecretState.cs` | `Live`/`AccessLost`/`Deleted` enum |
| `SafeExchange.Client.Web.Components/Classes/Helpers/PinnedSecretsHelper.cs` | Toggle helper |
| Test files under the existing client test project | ApiClient + helper + model tests |

### Modified files

| Path | Change |
|---|---|
| `SafeExchange.Client.Common/ApiClient/ApiClient.cs` | + pinned-secrets region (4 methods) |
| `SafeExchange.Client.Web.Components/Classes/StateContainer.cs` | + `PinnedSecretNames` |
| `SafeExchange.Client.Web.Components/Pages/Index.razor` | + Pinned section below search |
| `SafeExchange.Client.Web.Components/Pages/ListData.razor` | + per-row star toggle |
| `SafeExchange.Client.Web.Components/Pages/ViewData.razor` | + action-bar star toggle |

## Out of scope

- Reordering / drag-and-drop (backend orders by `CreatedAt DESC`).
- Auto-purge of stale pins (backend keeps them; UI surfaces deleted/access-lost states).
- Client-side cap pre-check (server is source of truth; surface its `400`).
- Notifications when a pinned secret becomes inaccessible (derived from the list response).
- A bUnit component-test harness (not present in the repo today).

## Open questions

- **`N / Max` counter:** the client does not know the server's `Max`. First version shows the
  current count only (e.g. "Pinned (3)") and relies on the cap `400` for the ceiling. Revisit if a
  config endpoint later exposes `Max`.

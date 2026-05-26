# Running the spike locally

> Spike branches: `spike/s2s-apps` in `safeexchange` (backend) and
> `safeexchange.blazorpwa` (frontend). **Never deployed.** Local-only.

## What's on the branch right now

Phases A, B, and C are landed end-to-end:

### Backend (`safeexchange`)

- New entity `ApplicationOwner` (many-to-many `Application` ↔ User/Group),
  Cosmos container `ApplicationOwners`. Composite key
  `{ApplicationId, SubjectType, SubjectId}`.
- `ApplicationOwnerService` enforces the ownership invariant: **≥ 2 distinct
  principals, ≥ 1 must be a User**. Pure-function ValidateInvariant is unit-
  tested in `ApplicationOwnerInvariantTests` (no Cosmos dependency, green).
- KV-backed settings: `Features:S2SAppsSelfService`,
  `Features:RequireApplicationOwnership`, `Limits:AdminListDefaultPageSize`,
  `Limits:AdminListMaxPageSize`, `Limits:OwnerlessGracePeriodDays`. All default
  to safe values; spike branch flips `Features:S2SAppsSelfService=True` via
  local `appsettings.json`.

Self-service endpoints (regular auth gate):
- `POST   /v2/s2sapps` register (caller becomes owner #1; ≥1 additional owner required)
- `GET    /v2/s2sapps/mine`
- `GET    /v2/s2sapps/{name}` (owner-only)
- `DELETE /v2/s2sapps/{name}` (owner-only; cascades owner rows)
- `GET    /v2/s2sapps/{name}/owners` (owner-only)
- `POST   /v2/s2sapps/{name}/owners` (owner-only; idempotent)
- `DELETE /v2/s2sapps/{name}/owners/{subjectType}/{subjectId}` (owner-only; refuses if invariant would break)

Admin endpoints (admin gate, paginated `?q=&page=&pageSize=` uniformly):
- `GET   /v2/admin/users` + `PATCH /v2/admin/users/{upn}/enabled`
- `GET   /v2/admin/applications` + `PATCH /v2/admin/applications/{name}/enabled`
- `GET   /v2/admin/audit?secretName=` — substring search, **includes historical
  anchors** for purged secrets (`IsHistorical=true` on the response).

### Main PWA (`safeexchange.blazorpwa`, project `SafeExchange.PWA`)

- `/s2sapps` page: list, register form with required second owner. NavMenu
  link "My Apps" (authenticated users only).
- `ApiClient.S2SApps.cs` partial with `RegisterS2SAppAsync` /
  `ListMyS2SAppsAsync`.

### Admin panel (new project `SafeExchange.AdminPanel`)

- **Separate Blazor WASM app**, peer of `SafeExchange.PWA`. Same Entra client
  id; no project reference to `SafeExchange.Client.Web.Components` so the
  visual language can diverge cleanly. Bootstrap via CDN.
- Dark theme by default, cyan accent (`--admin-accent: #36b1bf`), mobile-first
  layout (slide-over nav on phones), permanent `ADMIN` chip in the top bar.
- Pages:
  - `/` dashboard with three cards.
  - `/users` paginated search + enable/disable toggle.
  - `/applications` paginated search by name or client-id GUID + toggle + a
    "needs attention" pill when an app has fewer than 2 owners.
  - `/audit` paginated secret-name substring search with historical pill.
- `ApiClient.Admin.cs` partial in `SafeExchange.Client.Common` powers the panel.

### Still pending in the branch (next turns)

- **Phase A6:** `ApplicationOwnershipMigration` class (idempotent reconciliation
  of existing `Application` rows that have no `ApplicationOwner` records yet),
  exposed via `POST /v2/admin/migrate-applications`. Spike-local: also a
  `SEED_TEST_APPS=true` switch that pre-loads two test apps so the UI is
  testable without registering Entra apps by hand.
- **Phase B finish:** per-app detail / owner-mgmt UX on `/s2sapps`.
- **Phase D dev-auth cherry-pick:** lift the no-Entra-needed local-auth harness
  from `spike/images-as-attachments` so the spike can run without real Entra.
  Until that's done, **the spike needs a valid Entra token to hit any endpoint**.
- **Code reviewer subagent passes** for Phase A, B, and C — to be done once
  the remaining code lands.

## Prerequisites

- .NET 10 SDK
- Cosmos DB Emulator (used by the existing tests + dev run)
- Azurite (Storage Emulator)
- A way to log in. Two options:
  1. Real Entra: same app registration as staging. Easiest if you already use it.
  2. Dev-auth harness from `spike/images-as-attachments` (planned cherry-pick).

## Backend

```pwsh
cd safeexchange
git checkout spike/s2s-apps
cd SafeExchange.Functions

# Add to local.settings.json under "Values" (or user secrets):
#   "Features:S2SAppsSelfService": "True"
# Without it the endpoints return 204 (off-by-default for safety).

func host start
```

Sanity-check with a real token:

```pwsh
curl -H "Authorization: Bearer $TOKEN" http://localhost:7071/api/v2/s2sapps/mine
```

## Main PWA — self-service

```pwsh
cd safeexchange.blazorpwa
git checkout spike/s2s-apps

dotnet run --project SafeExchange.PWA
```

Open `https://localhost:5001/s2sapps` and register an app
(display name + Entra client GUID + second-owner UPN).

## Admin panel

```pwsh
# Different port (so it can run alongside the main PWA).
dotnet run --project SafeExchange.AdminPanel --urls "https://localhost:5101"
```

Open `https://localhost:5101` to land on the admin dashboard, then explore
Users / Applications / Audit. Search-as-you-go + pagination work; toggle
buttons call the backend.

## End-to-end smoke (manual)

1. Start backend + main PWA + admin panel.
2. Main PWA → `/s2sapps`: register an app with a co-owner.
3. Admin panel → `/applications`: see the new app appear; `Owners: 2` (no
   "attn" pill).
4. Admin panel → toggle the app `disabled`.
5. Main PWA → `/s2sapps`: the row's badge flips to "disabled".
6. (After Phase B finish lands) Try removing a co-owner from main PWA — the
   server should respond 409 with the ≥2-with-a-user message.
7. Admin panel → `/users`: search a partial UPN substring, page through, toggle
   `enabled` on a test user.
8. Admin panel → `/audit?secretName=…`: search across anchors, including a
   secret you've deleted — the row should carry the `historical` pill.

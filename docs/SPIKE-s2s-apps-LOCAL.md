# Running the spike locally

> Spike branches: `spike/s2s-apps` in `safeexchange` (backend) and
> `safeexchange.blazorpwa` (frontend). **Never deployed.** Local-only.

## What works in the current branch state

Phase A foundation + Phase A self-service slice + Phase B main-PWA UI slice:

- Backend: `ApplicationOwner` entity + Cosmos container, ownership-invariant
  service, two endpoints — `POST /v2/s2sapps` (register, caller becomes
  owner #1) and `GET /v2/s2sapps/mine` (apps where caller is direct user
  owner).
- Frontend: `/s2sapps` page on the main PWA — lists your apps and registers
  a new one with a required second owner. "My Apps" link added to NavMenu.

What's pending (subsequent commits on the same branch):

- Backend: detail GET, PATCH, DELETE, owners sub-resource; admin paginated
  users/applications/audit endpoints; existing-Applications migration class.
- Frontend: per-app detail / owner management UX.
- New project: `SafeExchange.AdminPanel` — separate WASM static web app with
  its own theme and admin pages (users / applications / audit). Scaffolding
  is in the plan; not yet committed.

See `docs/SPIKE-s2s-apps.md` and `docs/SPIKE-s2s-apps-PLAN.md` for the full
design and task list.

## Prerequisites

- .NET 10 SDK
- Cosmos DB Emulator (the backend tests + dev run already expect this; same
  one used by the rest of the project)
- Azurite (Azure Storage Emulator)
- An auth path. For the spike, the cleanest is to cherry-pick the dev-auth
  bypass from `spike/images-as-attachments` so you don't need real Entra.
  That branch's `LocalDev/` helpers are intentionally never-merged-to-main;
  see its README. (The cherry-pick step is **planned for Phase D** and not yet
  done on this branch — track in `SPIKE-s2s-apps-PLAN.md`.)

## Backend

```pwsh
cd safeexchange
git checkout spike/s2s-apps

# In local.settings.json (or user secrets), enable the spike feature flag:
#   "Features:S2SAppsSelfService": "True"
# Default is false everywhere, including the spike — endpoints return 204
# until you flip it.

cd SafeExchange.Functions
func host start
```

Sanity-check (with the feature flag on):

```pwsh
# Should return an empty list once you have a valid token; 204 if disabled.
curl -H "Authorization: Bearer $TOKEN" http://localhost:7071/api/v2/s2sapps/mine
```

## Main PWA (`/s2sapps`)

```pwsh
cd safeexchange.blazorpwa
git checkout spike/s2s-apps

dotnet run --project SafeExchange.PWA
```

Navigate to `https://localhost:5001/s2sapps`. With the feature flag on and a
valid token, you'll see the empty-state message and the register form.

## Admin panel

Not yet in the branch — scaffolding is planned as the next Phase C in
`SPIKE-s2s-apps-PLAN.md`. It will live as a peer Blazor WASM project at
`SafeExchange.AdminPanel/` in the same repo and be started independently
(`dotnet run --project SafeExchange.AdminPanel`).

## Smoke test (once everything is in)

1. Start backend + main PWA + admin panel.
2. From main PWA → `/s2sapps`, register an app supplying yourself as caller
   plus a co-owner email.
3. Switch to admin panel → `/applications` → see the new app, observe the
   pagination + filter.
4. Toggle the app to `disabled` from admin; the owner's `/s2sapps` row should
   flip to a disabled badge.
5. Try to remove a co-owner via the owner UI — the 409 message should mention
   the ≥2-with-user invariant.

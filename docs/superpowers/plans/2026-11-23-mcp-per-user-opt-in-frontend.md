# Per-user MCP opt-in — Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a signed-in user turn "Allow MCP (AI agents)" on/off in the PWA settings dialog, and let administrators see each user's MCP status and block/unblock MCP in the AdminPanel.

**Architecture:** All logic lives in `SafeExchange.Client.Common` so it is unit-testable in the existing NUnit project with a stubbed `HttpMessageHandler`: new DTOs, three `ApiClient` methods (`/me/settings` GET/PATCH, admin `mcp-blocked` PATCH), a `McpSettingsState` view-state class behind the PWA switch (load, optimistic save, revert on failure, admin-block handling) and a `McpStatus` label helper for the AdminPanel. The Razor changes are thin bindings to those classes: `SettingsDialog` (re-mounted in `MainLayout`, opened from the account dropdown), `Users.razor` (MCP pill per user) and `ManageUser.razor` ("Block MCP" toggle that mirrors the existing Enable/Disable toggle).

**Tech Stack:** .NET 10, Blazor WebAssembly (PWA + AdminPanel), Bootstrap 5 + Bootstrap Icons, NUnit 4 (`SafeExchange.Client.Common.Tests`).

**Spec (source of truth):** `C:\Users\yurio\Documents\github\safeexchange-wt-optin\docs\superpowers\specs\2026-11-23-mcp-per-user-opt-in-design.md` (sections "Frontend", "API", "Testing → Frontend").

---

## Context and decisions (read before starting)

- **Worktree:** `C:\Users\yurio\Documents\github\safeexchange.blazorpwa-wt-optin`, branch `feat/mcp-user-opt-in`. Run every command from that directory. Never touch `C:\Users\yurio\Documents\github\safeexchange.blazorpwa` (main tree).
- **API version:** `v3`. `ApiClient.ApiVersion` is `"v3"` and every comparable call already uses it (`/me/s2sapps` in `ApiClient.S2SApps.cs`, `/admin/users…` in `ApiClient.Admin.cs`). New methods build URLs with `$"{ApiVersion}/…"` — never hard-code a version.
- **Backend contract (from the spec):**
  - `GET /api/v3/me/settings` → `{ "status":"ok", "result":{ "mcpAllowed":bool, "mcpBlockedByAdmin":bool, "mcpEffective":bool } }`
  - `PATCH /api/v3/me/settings` body `{ "mcpAllowed": bool }` → same body. Enabling while blocked → HTTP 403 `{ "status":"forbidden", "subStatus":"mcp_blocked_by_admin", "error":"MCP is blocked for this account by an administrator." }`. S2S caller → 403 `status:"forbidden"`. Bad body → 400 `status:"error"`.
  - `GET /api/v3/admin/users` and `/admin/users/{upn}` additionally return `mcpAllowed`, `mcpBlockedByAdmin`.
  - `PATCH /api/v3/admin/users/{upn}/mcp-blocked` body `{ "blocked": bool }` → `{ "status":"ok", "result": <UserOverview> }` (same shape as `PATCH …/enabled`, which returns `UserOverviewOutput`).
- **Error handling convention:** `ApiClient` never throws on HTTP errors. `DeserializeOrErrorAsync` (in `ApiClient.S2SApps.cs`) parses the JSON body for any status code into `BaseResponseObject<T>` (`Status`, `SubStatus`, `Error`, `Result`); callers branch on `"ok".Equals(resp.Status)` and on `SubStatus`. JSON options are camelCase (`ApiClient` ctor), so a request DTO property `McpAllowed` serialises as `"mcpAllowed"`.
- **Tests:** NUnit 4 in `SafeExchange.Client.Common.Tests`, using `Utilities\CapturingHttpMessageHandler` + `Utilities\StubHttpClientFactory` (base address `https://test.example/api/`). There is **no bUnit** in the repo; the established pattern (see the pinned-secrets plan, `PinnedSecretsHelper`) is to put UI logic in a plain class in `SafeExchange.Client.Common` and test that. We follow it: the switch behaviour (renders state, saves, reverts on failure, blocked → off + disabled + notice) is tested through `McpSettingsState`; the admin column states through `McpStatus`; the block toggle through the `ApiClient` method test. The `.razor` files only bind to these and are verified by build + the manual checklist in Task 7. `SafeExchange.AdminPanel` has no test project and does not reference `SafeExchange.Client.Web.Components`, which is another reason the shared logic goes into `SafeExchange.Client.Common`.
- **`SettingsDialog` is currently orphaned.** PR #40 ("S2S apps UX…") stopped mounting `<SettingsDialog/>` in `MainLayout` and moved the theme picker into the account dropdown (`LoginDisplay.razor`), leaving the dialog file on disk. The spec places the MCP switch in `SettingsDialog`, so Task 5 re-mounts it in `MainLayout`, adds a "Settings" entry to the account dropdown that opens it, and **replaces** its old theme radios with the MCP switch (keeping the radios would give two theme pickers that do not sync with each other). Settings are loaded each time the dialog opens, never at app start (the user may not be signed in yet).
- **Revert-on-failure in Blazor:** the switch is a plain `<input type="checkbox">` with `checked="@state.SwitchChecked"`. If the state never changed, Blazor's diff sees no change and leaves the DOM checkbox in the user-clicked position. `McpSettingsState.SetAllowedAsync` therefore sets the new value optimistically **before** the first `await` (Blazor renders after the synchronous part of an async handler) and restores the old value on failure, so there are always two renders and the DOM is reset. A test pins this (`SetAllowed_IsOptimisticAndBusyWhileSaving`).
- **Build commands.** `SafeExchange.PWA.slnx` contains Client.Common, Client.Common.Tests, Client.Web.Components, Client.Web.Components.Tests and PWA — **not** the AdminPanel, which is built separately:
  - `dotnet build SafeExchange.PWA.slnx --nologo -v q`
  - `dotnet build SafeExchange.AdminPanel\SafeExchange.AdminPanel.csproj --nologo -v q`
  - `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build [--filter "FullyQualifiedName~<Class>"]`
- **Baseline (verified on `464096b`):** solution build 0 errors / 245 warnings (`--no-incremental`); `SafeExchange.Client.Common.Tests` 78 passed; `SafeExchange.Client.Web.Components.Tests` 20 passed. `SettingsDialog.razor` currently contributes 4 CS8618 warnings that disappear with the rewrite (expected after: 241). No touched file may gain a warning.
- **Versioning:** nothing to do. `SafeExchange.PWA/wwwroot/version.json` and `SafeExchange.AdminPanel/wwwroot/version.json` are bumped by `deployment\deploy-pwa.ps1` / `deployment\deploy-adminpanel.ps1` (commits "chore(version): auto-bump …" / "chore(adminpanel-version): auto-bump …") at deploy time. Do not edit `version.json`.
- **Commits:** every commit message ends with a blank line and the trailer `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` (the second `-m` in each commit command below does this).
- **Rollout dependency:** the frontend is merged/deployed only after the backend PR is on the target environment (spec "Delivery"). Until then the new calls would get 404 HTML, which `DeserializeOrErrorAsync` already turns into a readable error shown inline.

---

## File structure

| File | Action | Responsibility |
|---|---|---|
| `SafeExchange.Client.Common\Model\UserSettings.cs` | Create | `UserSettings` (GET/PATCH `/me/settings` result) and `UserSettingsUpdateRequest` (PATCH body). |
| `SafeExchange.Client.Common\ApiClient\ApiClient.Settings.cs` | Create | `GetMySettingsAsync`, `UpdateMySettingsAsync` (new `ApiClient` partial). |
| `SafeExchange.Client.Common\Model\AdminModels.cs` | Modify | `McpAllowed`/`McpBlockedByAdmin` on `UserOverview` and `UserDetail`; new `McpBlockedToggleRequest`. |
| `SafeExchange.Client.Common\ApiClient\ApiClient.Admin.cs` | Modify | `SetUserMcpBlockedAsync`. |
| `SafeExchange.Client.Common\Helpers\McpStatus.cs` | Create | Off / On / Blocked label + AdminPanel pill class (Blocked wins). |
| `SafeExchange.Client.Common\Helpers\McpSettingsState.cs` | Create | View state behind the PWA switch: load, optimistic save, revert, admin block. |
| `SafeExchange.Client.Common.Tests\Utilities\SequenceHttpMessageHandler.cs` | Create | Test fake answering several requests in order and recording them. |
| `SafeExchange.Client.Common.Tests\McpSettingsApiClientTests.cs` | Create | `/me/settings` request/response tests. |
| `SafeExchange.Client.Common.Tests\AdminUsersMcpApiClientTests.cs` | Create | Admin list/detail MCP fields + `mcp-blocked` PATCH tests. |
| `SafeExchange.Client.Common.Tests\McpStatusTests.cs` | Create | Admin column state tests. |
| `SafeExchange.Client.Common.Tests\McpSettingsStateTests.cs` | Create | Switch behaviour tests. |
| `SafeExchange.Client.Web.Components\Shared\SettingsDialog.razor` | Replace | Settings modal with the "Allow MCP (AI agents)" switch; public `ShowAsync()`. |
| `SafeExchange.PWA\Shared\MainLayout.razor` | Modify | Mount `<SettingsDialog>` and open it on request. |
| `SafeExchange.PWA\Shared\LoginDisplay.razor` | Modify | "Settings" entry in the account dropdown; `OnOpenSettings` callback. |
| `SafeExchange.AdminPanel\Pages\Users.razor` | Modify | "MCP: Off/On/Blocked" pill per user. |
| `SafeExchange.AdminPanel\Pages\ManageUser.razor` | Modify | User's own choice (read-only) + MCP status pill + "Block MCP"/"Unblock MCP" toggle. |

---

### Task 1: Client API — `/me/settings` models and methods

**Files:**
- Create: `SafeExchange.Client.Common\Model\UserSettings.cs`
- Create: `SafeExchange.Client.Common\ApiClient\ApiClient.Settings.cs`
- Test: `SafeExchange.Client.Common.Tests\McpSettingsApiClientTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SafeExchange.Client.Common.Tests\McpSettingsApiClientTests.cs`:

```csharp
/// <summary>
/// McpSettingsApiClientTests — verifies the /me/settings ApiClient methods:
/// request shape (verb + URL + body) and response mapping, including the
/// 403 "mcp_blocked_by_admin" refusal. HttpMessageHandler is stubbed.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Tests.Utilities;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    [TestFixture]
    public class McpSettingsApiClientTests
    {
        [Test]
        public async Task GetMySettingsAsync_SendsGetAndParsesSettings()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"mcpAllowed\":true,\"mcpBlockedByAdmin\":false,\"mcpEffective\":true}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetMySettingsAsync();

            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/me/settings"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.McpAllowed, Is.True);
            Assert.That(response.Result!.McpBlockedByAdmin, Is.False);
            Assert.That(response.Result!.McpEffective, Is.True);
        }

        [Test]
        public async Task UpdateMySettingsAsync_SendsPatchWithMcpAllowedBody()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"mcpAllowed\":true,\"mcpBlockedByAdmin\":false,\"mcpEffective\":true}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.UpdateMySettingsAsync(true);

            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/me/settings"));
            Assert.That(handler.CapturedRequestBody, Is.EqualTo("{\"mcpAllowed\":true}"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.McpEffective, Is.True);
        }

        [Test]
        public async Task UpdateMySettingsAsync_BlockedByAdminSurfacesSubStatus()
        {
            var body = "{\"status\":\"forbidden\",\"subStatus\":\"mcp_blocked_by_admin\",\"error\":\"MCP is blocked for this account by an administrator.\"}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.Forbidden, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.UpdateMySettingsAsync(true);

            Assert.That(response.Status, Is.EqualTo("forbidden"));
            Assert.That(response.SubStatus, Is.EqualTo("mcp_blocked_by_admin"));
            Assert.That(response.Error, Does.Contain("blocked"));
            Assert.That(response.Result, Is.Null);
        }
    }
}
```

- [ ] **Step 2: Build to verify the tests fail**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: FAIL — `error CS1061: 'ApiClient' does not contain a definition for 'GetMySettingsAsync'` (and the same for `UpdateMySettingsAsync`).

- [ ] **Step 3: Create the models**

Create `SafeExchange.Client.Common\Model\UserSettings.cs`:

```csharp
/// <summary>
/// UserSettings — the caller's own settings as returned by GET/PATCH /me/settings.
/// Mirrors the backend response; only McpAllowed is writable by the user.
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    public class UserSettings
    {
        public bool McpAllowed { get; set; }

        public bool McpBlockedByAdmin { get; set; }

        public bool McpEffective { get; set; }
    }

    public class UserSettingsUpdateRequest
    {
        public bool McpAllowed { get; set; }
    }
}
```

- [ ] **Step 4: Create the `ApiClient` partial**

Create `SafeExchange.Client.Common\ApiClient\ApiClient.Settings.cs` (`GetAsync`/`PatchAsync` are the private helpers already defined in `ApiClient.Admin.cs`; they are visible to every partial of `ApiClient`):

```csharp
/// <summary>
/// ApiClient.Settings — partial of ApiClient with the caller's own settings
/// endpoints (/me/settings).
/// </summary>

namespace SafeExchange.Client.Common
{
    using SafeExchange.Client.Common.Model;
    using System.Threading.Tasks;

    public partial class ApiClient
    {
        /// <summary>GET /v3/me/settings — the caller's own settings.</summary>
        public Task<BaseResponseObject<UserSettings>> GetMySettingsAsync()
            => GetAsync<UserSettings>($"{ApiVersion}/me/settings");

        /// <summary>
        /// PATCH /v3/me/settings — set the caller's own MCP opt-in. Enabling while an
        /// administrator has blocked MCP returns status "forbidden" with subStatus
        /// "mcp_blocked_by_admin".
        /// </summary>
        public Task<BaseResponseObject<UserSettings>> UpdateMySettingsAsync(bool mcpAllowed)
            => PatchAsync<UserSettings>($"{ApiVersion}/me/settings",
                new UserSettingsUpdateRequest { McpAllowed = mcpAllowed });
    }
}
```

- [ ] **Step 5: Build and run the tests**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: `Build succeeded.` `0 Error(s)`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build --filter "FullyQualifiedName~McpSettingsApiClientTests"`
Expected: `Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build`
Expected: `Passed!  - Failed:     0, Passed:    81, Skipped:     0, Total:    81`

- [ ] **Step 6: Commit**

```powershell
git add SafeExchange.Client.Common\Model\UserSettings.cs SafeExchange.Client.Common\ApiClient\ApiClient.Settings.cs SafeExchange.Client.Common.Tests\McpSettingsApiClientTests.cs
git commit -m "feat(client): /me/settings API client for the MCP opt-in" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Client API — admin user MCP fields and `mcp-blocked` toggle

**Files:**
- Modify: `SafeExchange.Client.Common\Model\AdminModels.cs:21-27` (`UserOverview`), `:87-93` (`UserDetail`), `:113-116` (`EnabledToggleRequest`)
- Modify: `SafeExchange.Client.Common\ApiClient\ApiClient.Admin.cs:21-23`
- Test: `SafeExchange.Client.Common.Tests\AdminUsersMcpApiClientTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SafeExchange.Client.Common.Tests\AdminUsersMcpApiClientTests.cs`:

```csharp
/// <summary>
/// AdminUsersMcpApiClientTests — verifies the admin user endpoints carry the
/// MCP flags (list + detail) and that the mcp-blocked toggle sends the right
/// verb, URL and body. HttpMessageHandler is stubbed.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Tests.Utilities;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    [TestFixture]
    public class AdminUsersMcpApiClientTests
    {
        [Test]
        public async Task ListUsersAsync_ParsesMcpFlags()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"items\":[" +
                       "{\"aadUpn\":\"a@contoso.com\",\"displayName\":\"A\",\"contactEmail\":\"\",\"enabled\":true,\"mcpAllowed\":true,\"mcpBlockedByAdmin\":false}," +
                       "{\"aadUpn\":\"b@contoso.com\",\"displayName\":\"B\",\"contactEmail\":\"\",\"enabled\":true,\"mcpAllowed\":true,\"mcpBlockedByAdmin\":true}]," +
                       "\"page\":0,\"pageSize\":25,\"total\":2,\"hasMore\":false}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.ListUsersAsync(null);

            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.Items[0].McpAllowed, Is.True);
            Assert.That(response.Result!.Items[0].McpBlockedByAdmin, Is.False);
            Assert.That(response.Result!.Items[1].McpBlockedByAdmin, Is.True);
        }

        [Test]
        public async Task GetUserDetailAsync_ParsesMcpFlags()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"aadUpn\":\"a@contoso.com\",\"enabled\":true,\"mcpAllowed\":false,\"mcpBlockedByAdmin\":true,\"recentTelemetryIds\":[]}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetUserDetailAsync("a@contoso.com");

            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.McpAllowed, Is.False);
            Assert.That(response.Result!.McpBlockedByAdmin, Is.True);
        }

        [Test]
        public async Task SetUserMcpBlockedAsync_SendsPatchWithBlockedBodyAndParsesUser()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"aadUpn\":\"alice@contoso.com\",\"displayName\":\"Alice\",\"contactEmail\":\"\",\"enabled\":true,\"mcpAllowed\":true,\"mcpBlockedByAdmin\":true}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.SetUserMcpBlockedAsync("alice@contoso.com", true);

            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/admin/users/alice%40contoso.com/mcp-blocked"));
            Assert.That(handler.CapturedRequestBody, Is.EqualTo("{\"blocked\":true}"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.McpAllowed, Is.True);
            Assert.That(response.Result!.McpBlockedByAdmin, Is.True);
        }
    }
}
```

- [ ] **Step 2: Build to verify the tests fail**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: FAIL — `error CS1061: 'UserOverview' does not contain a definition for 'McpAllowed'`, `'UserDetail' does not contain a definition for 'McpAllowed'`, `'ApiClient' does not contain a definition for 'SetUserMcpBlockedAsync'`.

- [ ] **Step 3: Add the fields and the request DTO**

In `SafeExchange.Client.Common\Model\AdminModels.cs` (class `UserOverview`), replace:

```csharp
        public string ContactEmail { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }

    public class ApplicationAdminOverview
```

with:

```csharp
        public string ContactEmail { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool McpAllowed { get; set; }
        public bool McpBlockedByAdmin { get; set; }
    }

    public class ApplicationAdminOverview
```

In the same file (class `UserDetail`), replace:

```csharp
        public bool Enabled { get; set; }
        public string Id { get; set; } = string.Empty;
```

with:

```csharp
        public bool Enabled { get; set; }
        public bool McpAllowed { get; set; }
        public bool McpBlockedByAdmin { get; set; }
        public string Id { get; set; } = string.Empty;
```

In the same file, replace:

```csharp
    public class EnabledToggleRequest
    {
        public bool Enabled { get; set; }
    }
```

with:

```csharp
    public class EnabledToggleRequest
    {
        public bool Enabled { get; set; }
    }

    public class McpBlockedToggleRequest
    {
        public bool Blocked { get; set; }
    }
```

- [ ] **Step 4: Add the `ApiClient` method**

In `SafeExchange.Client.Common\ApiClient\ApiClient.Admin.cs`, replace:

```csharp
                new EnabledToggleRequest { Enabled = enabled });

        public Task<BaseResponseObject<PaginatedResult<ApplicationAdminOverview>>> ListApplicationsAsync(string? q, int page = 0, int pageSize = 25)
```

with:

```csharp
                new EnabledToggleRequest { Enabled = enabled });

        public Task<BaseResponseObject<UserOverview>> SetUserMcpBlockedAsync(string upn, bool blocked)
            => PatchAsync<UserOverview>($"{ApiVersion}/admin/users/{Uri.EscapeDataString(upn)}/mcp-blocked",
                new McpBlockedToggleRequest { Blocked = blocked });

        public Task<BaseResponseObject<PaginatedResult<ApplicationAdminOverview>>> ListApplicationsAsync(string? q, int page = 0, int pageSize = 25)
```

- [ ] **Step 5: Build and run the tests**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: `Build succeeded.` `0 Error(s)`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build --filter "FullyQualifiedName~AdminUsersMcpApiClientTests"`
Expected: `Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build`
Expected: `Passed!  - Failed:     0, Passed:    84, Skipped:     0, Total:    84`

- [ ] **Step 6: Commit**

```powershell
git add SafeExchange.Client.Common\Model\AdminModels.cs SafeExchange.Client.Common\ApiClient\ApiClient.Admin.cs SafeExchange.Client.Common.Tests\AdminUsersMcpApiClientTests.cs
git commit -m "feat(client): admin user MCP flags and mcp-blocked toggle API" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: `McpStatus` — AdminPanel column states

**Files:**
- Create: `SafeExchange.Client.Common\Helpers\McpStatus.cs`
- Test: `SafeExchange.Client.Common.Tests\McpStatusTests.cs`

- [ ] **Step 1: Write the failing test**

Create `SafeExchange.Client.Common.Tests\McpStatusTests.cs`:

```csharp
/// <summary>
/// McpStatusTests — the AdminPanel MCP column: Off / On / Blocked, with an
/// administrator block winning over the user's own choice.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common.Helpers;

    [TestFixture]
    public class McpStatusTests
    {
        [TestCase(false, false, "Off", "off")]
        [TestCase(true, false, "On", "ok")]
        [TestCase(false, true, "Blocked", "attn")]
        [TestCase(true, true, "Blocked", "attn")]
        public void LabelAndPillClass_ReflectFlags_BlockedWins(bool mcpAllowed, bool mcpBlockedByAdmin, string expectedLabel, string expectedPillClass)
        {
            Assert.That(McpStatus.Label(mcpAllowed, mcpBlockedByAdmin), Is.EqualTo(expectedLabel));
            Assert.That(McpStatus.AdminPillClass(mcpAllowed, mcpBlockedByAdmin), Is.EqualTo(expectedPillClass));
        }
    }
}
```

- [ ] **Step 2: Build to verify the test fails**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: FAIL — `error CS0103: The name 'McpStatus' does not exist in the current context`.

- [ ] **Step 3: Write the implementation**

Create `SafeExchange.Client.Common\Helpers\McpStatus.cs` (`ok`/`off`/`attn` are the existing `.admin-pill` modifiers in `SafeExchange.AdminPanel\wwwroot\css\admin.css`: green / grey / amber):

```csharp
/// <summary>
/// McpStatus — maps a user's MCP flags to the single status shown in the
/// AdminPanel. An administrator block always wins over the user's own choice.
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    public static class McpStatus
    {
        public const string Off = "Off";

        public const string On = "On";

        public const string Blocked = "Blocked";

        public static string Label(bool mcpAllowed, bool mcpBlockedByAdmin)
            => mcpBlockedByAdmin ? Blocked : mcpAllowed ? On : Off;

        /// <summary>AdminPanel <c>admin-pill</c> modifier class for the status.</summary>
        public static string AdminPillClass(bool mcpAllowed, bool mcpBlockedByAdmin)
            => mcpBlockedByAdmin ? "attn" : mcpAllowed ? "ok" : "off";
    }
}
```

- [ ] **Step 4: Build and run the tests**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: `Build succeeded.` `0 Error(s)`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build --filter "FullyQualifiedName~McpStatusTests"`
Expected: `Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build`
Expected: `Passed!  - Failed:     0, Passed:    88, Skipped:     0, Total:    88`

- [ ] **Step 5: Commit**

```powershell
git add SafeExchange.Client.Common\Helpers\McpStatus.cs SafeExchange.Client.Common.Tests\McpStatusTests.cs
git commit -m "feat(client): McpStatus label helper (Off/On/Blocked, blocked wins)" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: `McpSettingsState` — behaviour of the "Allow MCP" switch

**Files:**
- Create: `SafeExchange.Client.Common.Tests\Utilities\SequenceHttpMessageHandler.cs`
- Create: `SafeExchange.Client.Common\Helpers\McpSettingsState.cs`
- Test: `SafeExchange.Client.Common.Tests\McpSettingsStateTests.cs`

- [ ] **Step 1: Add the multi-request test fake**

`CapturingHttpMessageHandler` answers every request with the same response; these tests need a GET followed by a PATCH with different answers. Create `SafeExchange.Client.Common.Tests\Utilities\SequenceHttpMessageHandler.cs`:

```csharp
/// <summary>
/// SequenceHttpMessageHandler — fake HttpMessageHandler that answers requests
/// from a queue of responders, in order, and records every request (and its
/// body) it receives. Used where one test drives several ApiClient calls.
/// </summary>

namespace SafeExchange.Client.Common.Tests.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responders = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public List<string?> RequestBodies { get; } = new();

        public static HttpResponseMessage Json(HttpStatusCode statusCode, string jsonBody)
            => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };

        public SequenceHttpMessageHandler Enqueue(HttpStatusCode statusCode, string jsonBody)
            => this.Enqueue(_ => Task.FromResult(Json(statusCode, jsonBody)));

        public SequenceHttpMessageHandler Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            this.responders.Enqueue(responder);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            this.RequestBodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

            if (this.responders.Count == 0)
            {
                throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
            }

            return await this.responders.Dequeue()(request).ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `SafeExchange.Client.Common.Tests\McpSettingsStateTests.cs`:

```csharp
/// <summary>
/// McpSettingsStateTests — the "Allow MCP (AI agents)" switch state: renders
/// the loaded state, saves through PATCH /me/settings, reverts on failure, and
/// is off + disabled with the administrator notice while MCP is blocked.
/// ApiClient is driven through a stubbed HttpMessageHandler.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Helpers;
    using SafeExchange.Client.Common.Tests.Utilities;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    [TestFixture]
    public class McpSettingsStateTests
    {
        private const string NotAllowed = "{\"status\":\"ok\",\"result\":{\"mcpAllowed\":false,\"mcpBlockedByAdmin\":false,\"mcpEffective\":false}}";

        private const string Allowed = "{\"status\":\"ok\",\"result\":{\"mcpAllowed\":true,\"mcpBlockedByAdmin\":false,\"mcpEffective\":true}}";

        private const string AllowedButBlocked = "{\"status\":\"ok\",\"result\":{\"mcpAllowed\":true,\"mcpBlockedByAdmin\":true,\"mcpEffective\":false}}";

        private static (McpSettingsState State, SequenceHttpMessageHandler Handler) Create()
        {
            var handler = new SequenceHttpMessageHandler();
            var state = new McpSettingsState(new ApiClient(new StubHttpClientFactory(handler)));
            return (state, handler);
        }

        [Test]
        public async Task Load_Allowed_SwitchOnAndEnabled()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.OK, Allowed);

            await state.LoadAsync();

            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/me/settings"));
            Assert.That(state.IsLoaded, Is.True);
            Assert.That(state.SwitchChecked, Is.True);
            Assert.That(state.SwitchDisabled, Is.False);
            Assert.That(state.ShowBlockedNotice, Is.False);
            Assert.That(state.Error, Is.Null);
        }

        [Test]
        public async Task Load_BlockedByAdmin_SwitchOffDisabledWithNotice()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.OK, AllowedButBlocked);

            await state.LoadAsync();

            Assert.That(state.SwitchChecked, Is.False);
            Assert.That(state.SwitchDisabled, Is.True);
            Assert.That(state.ShowBlockedNotice, Is.True);
            Assert.That(McpSettingsState.BlockedNotice, Is.EqualTo("Blocked by an administrator. Contact your SafeExchange administrator."));
        }

        [Test]
        public async Task Load_Failure_ShowsErrorAndKeepsSwitchDisabled()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.Forbidden, "{\"status\":\"forbidden\",\"error\":\"Settings are available to users only.\"}");

            await state.LoadAsync();

            Assert.That(state.IsLoaded, Is.False);
            Assert.That(state.SwitchDisabled, Is.True);
            Assert.That(state.Error, Is.EqualTo("Settings are available to users only."));
        }

        [Test]
        public async Task SetAllowed_Success_SendsPatchAndAppliesResult()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.OK, NotAllowed).Enqueue(HttpStatusCode.OK, Allowed);
            await state.LoadAsync();

            await state.SetAllowedAsync(true);

            Assert.That(handler.Requests[1].Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(handler.Requests[1].RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/me/settings"));
            Assert.That(handler.RequestBodies[1], Is.EqualTo("{\"mcpAllowed\":true}"));
            Assert.That(state.SwitchChecked, Is.True);
            Assert.That(state.IsBusy, Is.False);
            Assert.That(state.Error, Is.Null);
        }

        [Test]
        public async Task SetAllowed_IsOptimisticAndBusyWhileSaving()
        {
            var (state, handler) = Create();
            var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            handler.Enqueue(HttpStatusCode.OK, NotAllowed).Enqueue(_ => gate.Task);
            await state.LoadAsync();

            var saving = state.SetAllowedAsync(true);

            Assert.That(state.SwitchChecked, Is.True);
            Assert.That(state.IsBusy, Is.True);
            Assert.That(state.SwitchDisabled, Is.True);

            gate.SetResult(SequenceHttpMessageHandler.Json(HttpStatusCode.OK, Allowed));
            await saving;

            Assert.That(state.IsBusy, Is.False);
            Assert.That(state.SwitchChecked, Is.True);
        }

        [Test]
        public async Task SetAllowed_ServerError_RevertsAndShowsError()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.OK, NotAllowed)
                   .Enqueue(HttpStatusCode.BadRequest, "{\"status\":\"error\",\"error\":\"Body must be { \\\"mcpAllowed\\\": bool }.\"}");
            await state.LoadAsync();

            await state.SetAllowedAsync(true);

            Assert.That(state.McpAllowed, Is.False);
            Assert.That(state.SwitchChecked, Is.False);
            Assert.That(state.SwitchDisabled, Is.False);
            Assert.That(state.Error, Does.Contain("mcpAllowed"));
        }

        [Test]
        public async Task SetAllowed_BlockedByAdmin_RevertsAndShowsBlockedNotice()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.OK, NotAllowed)
                   .Enqueue(HttpStatusCode.Forbidden, "{\"status\":\"forbidden\",\"subStatus\":\"mcp_blocked_by_admin\",\"error\":\"MCP is blocked for this account by an administrator.\"}");
            await state.LoadAsync();

            await state.SetAllowedAsync(true);

            Assert.That(state.McpAllowed, Is.False);
            Assert.That(state.SwitchChecked, Is.False);
            Assert.That(state.SwitchDisabled, Is.True);
            Assert.That(state.ShowBlockedNotice, Is.True);
            Assert.That(state.Error, Is.Null);
        }

        [Test]
        public async Task SetAllowed_Exception_RevertsAndExposesException()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.OK, Allowed)
                   .Enqueue(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("network down")));
            await state.LoadAsync();

            await state.SetAllowedAsync(false);

            Assert.That(state.McpAllowed, Is.True);
            Assert.That(state.SwitchChecked, Is.True);
            Assert.That(state.LastException, Is.InstanceOf<HttpRequestException>());
            Assert.That(state.Error, Does.Contain("network down"));
        }

        [Test]
        public async Task SetAllowed_WhileBlocked_DoesNotCallServer()
        {
            var (state, handler) = Create();
            handler.Enqueue(HttpStatusCode.OK, AllowedButBlocked);
            await state.LoadAsync();

            await state.SetAllowedAsync(false);

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(state.SwitchChecked, Is.False);
        }
    }
}
```

- [ ] **Step 3: Build to verify the tests fail**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: FAIL — `error CS0246: The type or namespace name 'McpSettingsState' could not be found`.

- [ ] **Step 4: Write the implementation**

Create `SafeExchange.Client.Common\Helpers\McpSettingsState.cs`:

```csharp
/// <summary>
/// McpSettingsState — view state behind the "Allow MCP (AI agents)" switch in
/// the PWA settings dialog. Loads and saves the caller's own MCP opt-in via
/// /me/settings, applies the change optimistically and reverts it when the save
/// fails, and disables the switch while an administrator block is in place.
/// Kept UI-framework free so it can be unit-tested against a stubbed ApiClient.
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Threading.Tasks;

    public sealed class McpSettingsState
    {
        public const string BlockedByAdminSubStatus = "mcp_blocked_by_admin";

        public const string BlockedNotice = "Blocked by an administrator. Contact your SafeExchange administrator.";

        private readonly ApiClient apiClient;

        public McpSettingsState(ApiClient apiClient)
        {
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public bool IsLoaded { get; private set; }

        public bool IsBusy { get; private set; }

        public bool McpAllowed { get; private set; }

        public bool McpBlockedByAdmin { get; private set; }

        public string? Error { get; private set; }

        /// <summary>
        /// The exception from the last load/save, if any. The component inspects it
        /// for auth exceptions (e.g. an expired token) that need a sign-in redirect.
        /// </summary>
        public Exception? LastException { get; private set; }

        public bool SwitchChecked => this.McpAllowed && !this.McpBlockedByAdmin;

        public bool SwitchDisabled => !this.IsLoaded || this.IsBusy || this.McpBlockedByAdmin;

        public bool ShowBlockedNotice => this.McpBlockedByAdmin;

        public async Task LoadAsync()
        {
            this.IsBusy = true;
            this.Error = null;
            this.LastException = null;
            try
            {
                var response = await this.apiClient.GetMySettingsAsync();
                if ("ok".Equals(response.Status) && response.Result is not null)
                {
                    this.Apply(response.Result);
                    this.IsLoaded = true;
                }
                else
                {
                    this.Error = response.Error ?? response.Status;
                }
            }
            catch (Exception ex)
            {
                this.LastException = ex;
                this.Error = $"Could not load settings: {ex.Message}";
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        public async Task SetAllowedAsync(bool allowed)
        {
            if (this.SwitchDisabled)
            {
                return;
            }

            // Optimistic: the switch re-renders with the new value straight away and
            // with the old one again on failure, so the DOM checkbox is always reset.
            var previous = this.McpAllowed;
            this.McpAllowed = allowed;
            this.IsBusy = true;
            this.Error = null;
            this.LastException = null;
            try
            {
                var response = await this.apiClient.UpdateMySettingsAsync(allowed);
                if ("ok".Equals(response.Status) && response.Result is not null)
                {
                    this.Apply(response.Result);
                }
                else if (BlockedByAdminSubStatus.Equals(response.SubStatus))
                {
                    this.McpAllowed = previous;
                    this.McpBlockedByAdmin = true;
                }
                else
                {
                    this.McpAllowed = previous;
                    this.Error = response.Error ?? response.Status;
                }
            }
            catch (Exception ex)
            {
                this.McpAllowed = previous;
                this.LastException = ex;
                this.Error = $"Could not save the setting: {ex.Message}";
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private void Apply(UserSettings settings)
        {
            this.McpAllowed = settings.McpAllowed;
            this.McpBlockedByAdmin = settings.McpBlockedByAdmin;
        }
    }
}
```

- [ ] **Step 5: Build and run the tests**

Run: `dotnet build SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo -v q`
Expected: `Build succeeded.` `0 Error(s)`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build --filter "FullyQualifiedName~McpSettingsStateTests"`
Expected: `Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9`

Run: `dotnet test SafeExchange.Client.Common.Tests\SafeExchange.Client.Common.Tests.csproj --nologo --no-build`
Expected: `Passed!  - Failed:     0, Passed:    97, Skipped:     0, Total:    97`

- [ ] **Step 6: Commit**

```powershell
git add SafeExchange.Client.Common\Helpers\McpSettingsState.cs SafeExchange.Client.Common.Tests\Utilities\SequenceHttpMessageHandler.cs SafeExchange.Client.Common.Tests\McpSettingsStateTests.cs
git commit -m "feat(client): McpSettingsState for the Allow MCP switch (optimistic save, revert, admin block)" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: PWA — "Allow MCP (AI agents)" switch in the settings dialog

The behaviour is covered by `McpSettingsStateTests` (Task 4); this task only binds markup to it, so the check is a clean build with no new warnings (plus the manual checklist in Task 7).

**Files:**
- Replace: `SafeExchange.Client.Web.Components\Shared\SettingsDialog.razor` (whole file)
- Modify: `SafeExchange.PWA\Shared\MainLayout.razor:15` and `:34-41`
- Modify: `SafeExchange.PWA\Shared\LoginDisplay.razor:90-95`, `:142-143`, `:297-308`

- [ ] **Step 1: Replace `SettingsDialog.razor`**

Replace the entire contents of `SafeExchange.Client.Web.Components\Shared\SettingsDialog.razor` with the following. Notes: `ItemPickDialogHelper` (already registered as scoped in `ServicesHelper.ConfigureServices`) shows a Bootstrap modal for an `ElementReference`; the old theme radios, `ColorThemeContext`/`EditContext` fields and the unused `NotificationsSubscriber` injection are removed because theme and accent now live in the account dropdown; `SettingsDialog.razor.css` is left untouched.

```razor
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
@using SafeExchange.Client.Common
@using SafeExchange.Client.Common.Helpers

@inject ApiClient apiClient
@inject ItemPickDialogHelper dialogHelper

@* Personal settings. Opened from the account dropdown (LoginDisplay -> MainLayout
   -> ShowAsync); settings are (re)loaded on every open so an administrator block
   applied since the last open is shown. Theme and accent live in the dropdown. *@
<div class="modal fade" id="settingsDialog" @ref="this.modalElement" tabindex="-1" role="dialog" aria-labelledby="settingsDialogLabel">
    <div class="modal-dialog" role="document">
        <div class="modal-content">
            <div class="modal-header">
                <p class="modal-title" id="settingsDialogLabel"><strong>Settings</strong></p>
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>
            <div class="modal-body px-4">
                <div class="form-check form-switch d-flex align-items-center gap-2">
                    <input class="form-check-input" type="checkbox" role="switch" id="mcpAllowedSwitch"
                           checked="@this.mcp.SwitchChecked"
                           disabled="@this.mcp.SwitchDisabled"
                           aria-describedby="mcpAllowedHelp"
                           @onchange="this.OnMcpAllowedChangedAsync" />
                    <label class="form-check-label" for="mcpAllowedSwitch">Allow MCP (AI agents)</label>
                    @if (this.mcp.IsBusy)
                    {
                        <span class="spinner-border spinner-border-sm text-primary" role="status" aria-label="Working"></span>
                    }
                </div>
                <div id="mcpAllowedHelp" class="form-text">
                    Lets AI tools such as Copilot CLI act on your secrets through SafeExchange's MCP endpoint.
                </div>
                @if (this.mcp.ShowBlockedNotice)
                {
                    <div class="alert alert-secondary mt-2 mb-0" role="status">
                        <i class="bi bi-slash-circle me-1"></i>@McpSettingsState.BlockedNotice
                    </div>
                }
                @if (!string.IsNullOrEmpty(this.mcp.Error))
                {
                    <div class="alert alert-warning mt-2 mb-0" role="alert">@this.mcp.Error</div>
                }
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
            </div>
        </div>
    </div>
</div>

@code {
    private ElementReference modalElement;

    private McpSettingsState mcp = default!;

    protected override void OnInitialized()
    {
        this.mcp = new McpSettingsState(this.apiClient);
    }

    public async Task ShowAsync()
    {
        this.mcp = new McpSettingsState(this.apiClient);
        this.StateHasChanged();
        await this.dialogHelper.ShowModalAsync(this.modalElement);

        var loading = this.mcp.LoadAsync();
        this.StateHasChanged();
        await loading;

        this.RedirectIfSignInRequired();
        this.StateHasChanged();
    }

    private async Task OnMcpAllowedChangedAsync(ChangeEventArgs e)
    {
        await this.mcp.SetAllowedAsync(e.Value is true);
        this.RedirectIfSignInRequired();
    }

    private void RedirectIfSignInRequired()
    {
        if (this.mcp.LastException is AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
    }
}
```

- [ ] **Step 2: Mount the dialog in `MainLayout.razor`**

In `SafeExchange.PWA\Shared\MainLayout.razor`, replace:

```razor
            <LoginDisplay />
```

with:

```razor
            <LoginDisplay OnOpenSettings="this.OpenSettingsAsync" />
```

In the same file, replace:

```razor
    </main>
</div>

@code {
    private bool navOpen;

    private void ToggleNav() => this.navOpen = !this.navOpen;
    private void CloseNav() => this.navOpen = false;
}
```

with (the modal sits at the layout root, outside the sticky header, so it is not trapped in the header's stacking context):

```razor
    </main>
</div>

<SettingsDialog @ref="this.settingsDialog" />

@code {
    private bool navOpen;

    private SettingsDialog? settingsDialog;

    private void ToggleNav() => this.navOpen = !this.navOpen;
    private void CloseNav() => this.navOpen = false;

    private Task OpenSettingsAsync() => this.settingsDialog?.ShowAsync() ?? Task.CompletedTask;
}
```

- [ ] **Step 3: Add the "Settings" entry to the account dropdown**

In `SafeExchange.PWA\Shared\LoginDisplay.razor`, replace:

```razor
                    <i class="bi bi-robot me-2"></i>My S2S apps
                </button>
            </div>
```

with:

```razor
                    <i class="bi bi-robot me-2"></i>My S2S apps
                </button>
                <button type="button" class="btn btn-link btn-sm w-100 text-start text-decoration-none px-0"
                        @onclick="this.OpenSettingsAsync">
                    <i class="bi bi-gear me-2"></i>Settings
                </button>
            </div>
```

In the same file, replace:

```csharp
@code {
    private const string AccentStorageKey = "client-accent";
```

with:

```csharp
@code {
    private const string AccentStorageKey = "client-accent";

    [Parameter]
    public EventCallback OnOpenSettings { get; set; }
```

In the same file, replace:

```csharp
        this.Navigation.NavigateTo("s2sapps");
    }
```

with (closing the dropdown first, like "My S2S apps" does — it uses `data-bs-auto-close="outside"`, so inner clicks don't close it):

```csharp
        this.Navigation.NavigateTo("s2sapps");
    }

    private async Task OpenSettingsAsync()
    {
        try
        {
            await this.JS.InvokeVoidAsync("safeexchange.closeUserDropdown");
        }
        catch
        {
        }
        await this.OnOpenSettings.InvokeAsync();
    }
```

- [ ] **Step 4: Build and check warnings**

Run: `dotnet build SafeExchange.PWA.slnx --nologo -v q --no-incremental`
Expected: `0 Error(s)` and `241 Warning(s)` (baseline 245 minus the 4 old `SettingsDialog.razor` CS8618 warnings).

Run: `dotnet build SafeExchange.PWA.slnx --nologo -v q --no-incremental 2>&1 | Select-String 'SettingsDialog|MainLayout|LoginDisplay'`
Expected: no output.

Run: `dotnet test SafeExchange.PWA.slnx --nologo --no-build`
Expected: `Passed!  - Failed:     0, Passed:    97 …SafeExchange.Client.Common.Tests.dll` and `Passed!  - Failed:     0, Passed:    20 …SafeExchange.Client.Web.Components.Tests.dll`

- [ ] **Step 5: Commit**

```powershell
git add SafeExchange.Client.Web.Components\Shared\SettingsDialog.razor SafeExchange.PWA\Shared\MainLayout.razor SafeExchange.PWA\Shared\LoginDisplay.razor
git commit -m "feat(pwa): Allow MCP (AI agents) switch in the settings dialog" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: AdminPanel — MCP column and "Block MCP" toggle

The column states are covered by `McpStatusTests` (Task 3) and the toggle request/response by `AdminUsersMcpApiClientTests` (Task 2). `ToggleMcpBlockedAsync` mirrors the existing `ToggleAsync` (Enable/Disable) on the same page: confirm dialog, PATCH, copy the returned flags into the view, inline error.

**Files:**
- Modify: `SafeExchange.AdminPanel\Pages\Users.razor:1-3`, `:33`
- Modify: `SafeExchange.AdminPanel\Pages\ManageUser.razor:4-5`, `:73-74`, `:125`, `:199`

- [ ] **Step 1: MCP pill per user in `Users.razor`**

In `SafeExchange.AdminPanel\Pages\Users.razor`, replace:

```razor
@page "/users"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject ApiClient apiClient
```

with:

```razor
@page "/users"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using SafeExchange.Client.Common.Helpers
@inject ApiClient apiClient
```

In the same file, replace:

```razor
            <span class="admin-pill @(u.Enabled ? "ok" : "off")">@(u.Enabled ? "enabled" : "disabled")</span>
```

with:

```razor
            <span class="admin-pill @McpStatus.AdminPillClass(u.McpAllowed, u.McpBlockedByAdmin)" title="MCP (AI agents)">MCP: @McpStatus.Label(u.McpAllowed, u.McpBlockedByAdmin)</span>
            <span class="admin-pill @(u.Enabled ? "ok" : "off")">@(u.Enabled ? "enabled" : "disabled")</span>
```

- [ ] **Step 2: User's choice + "Block MCP" toggle in `ManageUser.razor`**

In `SafeExchange.AdminPanel\Pages\ManageUser.razor`, replace:

```razor
@using SafeExchange.Client.Common
@using SafeExchange.Client.Common.Model
```

with:

```razor
@using SafeExchange.Client.Common
@using SafeExchange.Client.Common.Helpers
@using SafeExchange.Client.Common.Model
```

In the same file, replace:

```razor
            <dd class="col-sm-9">@(this.user.ConsentRequired ? "yes" : "no")</dd>
```

with:

```razor
            <dd class="col-sm-9">@(this.user.ConsentRequired ? "yes" : "no")</dd>

            <dt class="col-sm-3">MCP (user's choice)</dt>
            <dd class="col-sm-9">
                @(this.user.McpAllowed ? "allowed" : "not allowed")
                <span class="text-muted small">— set by the user, read-only here</span>
            </dd>

            <dt class="col-sm-3">MCP access</dt>
            <dd class="col-sm-9 d-flex align-items-center">
                <span class="admin-pill @McpStatus.AdminPillClass(this.user.McpAllowed, this.user.McpBlockedByAdmin)">@McpStatus.Label(this.user.McpAllowed, this.user.McpBlockedByAdmin)</span>
                <button type="button"
                        class="btn btn-sm @(this.user.McpBlockedByAdmin ? "btn-outline-success" : "btn-outline-warning") ms-3"
                        disabled="@this.togglingMcp"
                        @onclick="this.ToggleMcpBlockedAsync">
                    @if (this.togglingMcp)
                    {
                        <span class="spinner-border spinner-border-sm me-1"></span>
                    }
                    @(this.user.McpBlockedByAdmin ? "Unblock MCP" : "Block MCP")
                </button>
            </dd>
```

In the same file, replace:

```csharp
    private bool toggling;
```

with:

```csharp
    private bool toggling;
    private bool togglingMcp;
```

In the same file, replace:

```csharp
    private void GoToList() => this.NavManager.NavigateTo("users");
```

with:

```csharp
    private async Task ToggleMcpBlockedAsync()
    {
        if (this.user is null || this.confirmDialog is null)
        {
            return;
        }

        var willBlock = !this.user.McpBlockedByAdmin;
        var ok = await this.confirmDialog.ShowAsync(
            title: willBlock ? "Block MCP" : "Unblock MCP",
            body: willBlock
                ? $"Block MCP for user '{this.user.AadUpn}'? AI agents are refused for this user even if they allowed MCP."
                : $"Unblock MCP for user '{this.user.AadUpn}'? MCP stays off until the user allows it.",
            confirmLabel: willBlock ? "Block MCP" : "Unblock MCP",
            confirmButtonClass: willBlock ? "btn-warning" : "btn-success");
        if (!ok)
        {
            return;
        }

        this.togglingMcp = true;
        this.error = null;
        try
        {
            var resp = await this.apiClient.SetUserMcpBlockedAsync(this.user.AadUpn, willBlock);
            if ("ok".Equals(resp.Status) && resp.Result is not null)
            {
                this.user.McpAllowed = resp.Result.McpAllowed;
                this.user.McpBlockedByAdmin = resp.Result.McpBlockedByAdmin;
            }
            else
            {
                this.error = resp.Error ?? resp.Status;
            }
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
        finally
        {
            this.togglingMcp = false;
        }
    }

    private void GoToList() => this.NavManager.NavigateTo("users");
```

- [ ] **Step 3: Build the AdminPanel and check warnings**

The AdminPanel is not in `SafeExchange.PWA.slnx`; build it directly.

Run: `dotnet build SafeExchange.AdminPanel\SafeExchange.AdminPanel.csproj --nologo -v q --no-incremental`
Expected: `0 Error(s)` (pre-existing warnings only, e.g. NU1510 and nullable warnings in other files).

Run: `dotnet build SafeExchange.AdminPanel\SafeExchange.AdminPanel.csproj --nologo -v q --no-incremental 2>&1 | Select-String 'Users.razor|ManageUser.razor'`
Expected: no output.

- [ ] **Step 4: Commit**

```powershell
git add SafeExchange.AdminPanel\Pages\Users.razor SafeExchange.AdminPanel\Pages\ManageUser.razor
git commit -m "feat(adminpanel): MCP status column and Block MCP toggle" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 7: Final verification

- [ ] **Step 1: Full build, tests and warning check**

Run: `dotnet build SafeExchange.PWA.slnx --nologo -v q --no-incremental`
Expected: `0 Error(s)`, `241 Warning(s)`.

Run: `dotnet build SafeExchange.PWA.slnx --nologo -v q --no-incremental 2>&1 | Select-String 'SettingsDialog|MainLayout|LoginDisplay|ApiClient\.Settings|ApiClient\.Admin|AdminModels|UserSettings|McpSettingsState|McpStatus|SequenceHttpMessageHandler|McpSettingsApiClientTests|AdminUsersMcpApiClientTests|McpSettingsStateTests|McpStatusTests'`
Expected: no output.

Run: `dotnet build SafeExchange.AdminPanel\SafeExchange.AdminPanel.csproj --nologo -v q --no-incremental 2>&1 | Select-String 'Users.razor|ManageUser.razor| error '`
Expected: no output.

Run: `dotnet test SafeExchange.PWA.slnx --nologo --no-build`
Expected: `Passed!  - Failed:     0, Passed:    97` (Client.Common.Tests) and `Passed!  - Failed:     0, Passed:    20` (Client.Web.Components.Tests).

- [ ] **Step 2: Check the branch history**

Run: `git --no-pager log --oneline -7`
Expected: the six commits from Tasks 1–6 (plus this plan's commit below them), working tree clean (`git status --porcelain` prints nothing).

- [ ] **Step 3: Manual checklist (once the backend PR is deployed to the environment the local `appsettings` point at, e.g. staging)**

Run the PWA (`dotnet run --project SafeExchange.PWA`) and the AdminPanel (`dotnet run --project SafeExchange.AdminPanel`) against that backend and check:

1. PWA account menu shows "Settings"; it closes the dropdown and opens the "Settings" modal with "Allow MCP (AI agents)" and the explanation line. The switch is disabled with a spinner until loaded, then shows the stored value (off for a new user).
2. Turning the switch on/off persists (close and reopen the dialog: the value is kept; `GET /api/v3/me/settings` in DevTools shows the new value).
3. With DevTools → Network → "Offline", toggling shows an inline warning and the switch returns to its previous position.
4. In the AdminPanel, Users list shows "MCP: Off" (grey), "MCP: On" (green) or "MCP: Blocked" (amber) per user; Blocked shows even when the user allowed MCP.
5. ManageUser shows "MCP (user's choice)" read-only and "MCP access" with a "Block MCP" button; confirming calls `PATCH /api/v3/admin/users/{upn}/mcp-blocked` and the pill changes to "Blocked" and the button to "Unblock MCP" without reloading.
6. Reopen the PWA settings dialog for the blocked user: the switch is off and disabled and the notice "Blocked by an administrator. Contact your SafeExchange administrator." is shown. Unblock in the AdminPanel → reopen the dialog → switch enabled again, showing the user's own stored choice.

No commit in this task unless a fix was needed (then commit it with the same trailer).

---

## Self-review against the spec

| Spec requirement (Frontend / API / Testing) | Covered by |
|---|---|
| PWA `SettingsDialog`: "Allow MCP (AI agents)" switch with one-line explanation | Task 5 Step 1 |
| Loads from `GET /me/settings`, saves with `PATCH /me/settings` | Task 1 (API), Task 4 (`LoadAsync`/`SetAllowedAsync`), Task 5 (`ShowAsync`, `@onchange`) |
| Blocked → switch off and disabled, notice "Blocked by an administrator. Contact your SafeExchange administrator." | Task 4 (`SwitchChecked`/`SwitchDisabled`/`ShowBlockedNotice`, `BlockedNotice`; tests `Load_BlockedByAdmin…`, `SetAllowed_BlockedByAdmin…`), Task 5 markup |
| Enabling while blocked → 403 `mcp_blocked_by_admin` handled | Task 1 test `UpdateMySettingsAsync_BlockedByAdminSurfacesSubStatus`, Task 4 `SetAllowed_BlockedByAdmin_RevertsAndShowsBlockedNotice` |
| Errors shown inline; switch reverts on failure | Task 4 (`Error`, revert; tests `SetAllowed_ServerError…`, `SetAllowed_Exception…`, `SetAllowed_IsOptimistic…`), Task 5 alert markup |
| S2S / non-user → 403 on settings shown as error | Task 4 `Load_Failure_ShowsErrorAndKeepsSwitchDisabled` |
| AdminPanel `Users.razor`: MCP column Off / On / Blocked (Blocked wins) | Task 3 (`McpStatus` + tests), Task 6 Step 1 |
| AdminPanel `ManageUser.razor`: user's own choice read-only + "Block MCP" toggle calling `PATCH …/mcp-blocked` | Task 2 (API + test), Task 6 Step 2 |
| Admin list/detail return `mcpAllowed`, `mcpBlockedByAdmin` | Task 2 (models + tests) |
| Client API methods and models in the shared client library, following existing patterns | Tasks 1–2 (`ApiClient` partials, `GetAsync`/`PatchAsync`, camelCase DTOs, `v3`) |
| Admins can only block/unblock; turning MCP on is the user's choice | Task 6 offers only Block/Unblock; the user's choice is read-only |
| Frontend tests: switch renders state, saves, reverts on failure, blocked disables + notice; admin column states; block toggle calls endpoint and updates view | Tasks 2–4 tests; view update in Task 6 copies `resp.Result` flags (shape tested in Task 2) |
| No MCP tool reads/changes these settings; enforcement; backfill; docs | Backend PR — out of scope for this plan |

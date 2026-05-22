# Pinned Secrets UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Surface the backend pinned-secrets feature in the Blazor PWA — a Home-page favourites list plus pin/unpin star toggles on the View and My Secrets pages.

**Architecture:** Mirror the existing pinned-groups code. New DTO + domain model + 4 ApiClient methods + a toggle helper live in `SafeExchange.Client.Common` (so they are unit-testable against the existing `SafeExchange.Client.Common.Tests` NUnit project). The 3 UI surfaces live in `SafeExchange.Client.Web.Components` and read a `HashSet<string>` of pinned names held on `StateContainer`.

**Tech Stack:** .NET 10, Blazor WebAssembly, Bootstrap 5 + Bootstrap Icons, NUnit (client tests with a stubbed `HttpMessageHandler`).

**Backend response shapes (verified against `safeexchange` Core handlers):**
- `GET /v2/pinnedsecrets-list` → `{status:"ok",result:[PinnedSecretOutput]}`; empty → `{status:"no_content",result:[]}`
- `GET /v2/pinnedsecrets/{id}` pinned → `{status:"ok",result:{...}}`; not pinned → `{status:"no_content",result:null}`
- `PUT /v2/pinnedsecrets/{id}` ok → `{status:"ok",result:{...}}`; over cap → HTTP 400 `{status:"error",error:"Pinned secret count is N, which is higher or equal than allowed no. of M pinned secrets. Please unpin secrets before adding new ones."}`; missing secret → HTTP 404 `{status:"not_found",...}`
- `DELETE /v2/pinnedsecrets/{id}` removed → `{status:"ok",result:"ok"}`; nothing to remove → `{status:"no_content",result:"Pin for secret '...' does not exist."}`

---

### Task 1: `PinnedSecretOutput` DTO

**Files:**
- Create: `SafeExchange.Client.Common/Model/Dto/Output/PinnedSecretOutput.cs`

- [ ] **Step 1: Create the DTO** (pure data, no logic → no test of its own; exercised by Task 3)

```csharp
/// <summary>
/// PinnedSecretOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System.Collections.Generic;

    public class PinnedSecretOutput
    {
        public string SecretName { get; set; } = string.Empty;

        public bool Exists { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        public List<string> Tags { get; set; } = new List<string>();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build SafeExchange.Client.Common/SafeExchange.Client.Common.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Client.Common/Model/Dto/Output/PinnedSecretOutput.cs
git commit -m "feat(pinned-secrets): add PinnedSecretOutput client DTO"
```

---

### Task 2: `PinnedSecret` domain model + `PinnedSecretState`

**Files:**
- Create: `SafeExchange.Client.Common/Model/PinnedSecretState.cs`
- Create: `SafeExchange.Client.Common/Model/PinnedSecret.cs`
- Test: `SafeExchange.Client.Common.Tests/PinnedSecretModelTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
/// <summary>
/// PinnedSecretModelTests — verifies the three-state derivation on the
/// PinnedSecret domain model built from a PinnedSecretOutput DTO.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common.Model;
    using System.Collections.Generic;

    [TestFixture]
    public class PinnedSecretModelTests
    {
        [Test]
        public void State_Live_WhenExistsAndCanRead()
        {
            var model = new PinnedSecret(new PinnedSecretOutput
            {
                SecretName = "s1", Exists = true, CanRead = true, Tags = new List<string> { "prod" }
            });

            Assert.That(model.State, Is.EqualTo(PinnedSecretState.Live));
            Assert.That(model.SecretName, Is.EqualTo("s1"));
            Assert.That(model.Tags, Has.Member("prod"));
        }

        [Test]
        public void State_AccessLost_WhenExistsButCannotRead()
        {
            var model = new PinnedSecret(new PinnedSecretOutput
            {
                SecretName = "s2", Exists = true, CanRead = false
            });

            Assert.That(model.State, Is.EqualTo(PinnedSecretState.AccessLost));
        }

        [Test]
        public void State_Deleted_WhenNotExists()
        {
            var model = new PinnedSecret(new PinnedSecretOutput
            {
                SecretName = "s3", Exists = false, CanRead = false
            });

            Assert.That(model.State, Is.EqualTo(PinnedSecretState.Deleted));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SafeExchange.Client.Common.Tests --filter PinnedSecretModelTests`
Expected: FAIL — `PinnedSecret` / `PinnedSecretState` do not exist.

- [ ] **Step 3: Create the enum**

```csharp
/// <summary>
/// PinnedSecretState
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    public enum PinnedSecretState
    {
        Live,
        AccessLost,
        Deleted
    }
}
```

- [ ] **Step 4: Create the model**

```csharp
/// <summary>
/// PinnedSecret
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System.Collections.Generic;

    public class PinnedSecret
    {
        public PinnedSecret()
        { }

        public PinnedSecret(PinnedSecretOutput source)
        {
            this.SecretName = source.SecretName;
            this.Exists = source.Exists;
            this.CanRead = source.CanRead;
            this.CanWrite = source.CanWrite;
            this.CanGrantAccess = source.CanGrantAccess;
            this.CanRevokeAccess = source.CanRevokeAccess;
            this.Tags = source.Tags ?? new List<string>();
        }

        public string SecretName { get; set; } = string.Empty;

        public bool Exists { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

        public PinnedSecretState State
        {
            get
            {
                if (!this.Exists)
                {
                    return PinnedSecretState.Deleted;
                }

                return this.CanRead ? PinnedSecretState.Live : PinnedSecretState.AccessLost;
            }
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test SafeExchange.Client.Common.Tests --filter PinnedSecretModelTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add SafeExchange.Client.Common/Model/PinnedSecret.cs SafeExchange.Client.Common/Model/PinnedSecretState.cs SafeExchange.Client.Common.Tests/PinnedSecretModelTests.cs
git commit -m "feat(pinned-secrets): add PinnedSecret model with three-state derivation"
```

---

### Task 3: ApiClient pinned-secrets methods

**Files:**
- Modify: `SafeExchange.Client.Common/ApiClient/ApiClient.cs` (add a `#region pinned secrets` after the `#region pinned groups` block, before `#region search`)
- Test: `SafeExchange.Client.Common.Tests/PinnedSecretsApiClientTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
/// <summary>
/// PinnedSecretsApiClientTests — verifies the four pinned-secrets ApiClient
/// methods: request shape (verb + URL + empty PUT body) and response mapping
/// (ok / no_content / 400 cap). HttpMessageHandler is stubbed.
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
    public class PinnedSecretsApiClientTests
    {
        [Test]
        public async Task ListPinnedSecretsAsync_SendsGetAndParsesList()
        {
            var body = "{\"status\":\"ok\",\"result\":[{\"secretName\":\"s1\",\"exists\":true,\"canRead\":true,\"tags\":[\"prod\"]},{\"secretName\":\"s2\",\"exists\":false,\"canRead\":false,\"tags\":[]}]}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.ListPinnedSecretsAsync();

            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v2/pinnedsecrets-list"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result, Has.Count.EqualTo(2));
            Assert.That(response.Result![0].SecretName, Is.EqualTo("s1"));
            Assert.That(response.Result![0].Tags, Has.Member("prod"));
        }

        [Test]
        public async Task ListPinnedSecretsAsync_EmptyMapsToNoContent()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"no_content\",\"result\":[]}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.ListPinnedSecretsAsync();

            Assert.That(response.Status, Is.EqualTo("no_content"));
            Assert.That(response.Result, Is.Empty);
        }

        [Test]
        public async Task GetPinnedSecretAsync_PinnedMapsDto()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\",\"result\":{\"secretName\":\"s1\",\"exists\":true,\"canRead\":true,\"tags\":[]}}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetPinnedSecretAsync("s1");

            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v2/pinnedsecrets/s1"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.SecretName, Is.EqualTo("s1"));
        }

        [Test]
        public async Task GetPinnedSecretAsync_NotPinnedMapsNoContentNullResult()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"no_content\",\"result\":null}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetPinnedSecretAsync("s9");

            Assert.That(response.Status, Is.EqualTo("no_content"));
            Assert.That(response.Result, Is.Null);
        }

        [Test]
        public async Task PutPinnedSecretAsync_SendsPutWithEmptyBody()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\",\"result\":{\"secretName\":\"s1\",\"exists\":true,\"canRead\":true,\"tags\":[]}}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.PutPinnedSecretAsync("s1");

            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v2/pinnedsecrets/s1"));
            Assert.That(handler.CapturedRequest!.Content, Is.Null);
            Assert.That(response.Status, Is.EqualTo("ok"));
        }

        [Test]
        public async Task PutPinnedSecretAsync_OverCapMapsErrorWithMessage()
        {
            var body = "{\"status\":\"error\",\"error\":\"Pinned secret count is 5, which is higher or equal than allowed no. of 5 pinned secrets. Please unpin secrets before adding new ones.\"}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.BadRequest, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.PutPinnedSecretAsync("s6");

            Assert.That(response.Status, Is.EqualTo("error"));
            Assert.That(response.Error, Does.Contain("Please unpin secrets"));
        }

        [Test]
        public async Task DeletePinnedSecretAsync_RemovedMapsOk()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\",\"result\":\"ok\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.DeletePinnedSecretAsync("s1");

            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Delete));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v2/pinnedsecrets/s1"));
            Assert.That(response.Status, Is.EqualTo("ok"));
        }

        [Test]
        public async Task DeletePinnedSecretAsync_NothingToRemoveMapsNoContent()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"no_content\",\"result\":\"Pin for secret 's1' does not exist.\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.DeletePinnedSecretAsync("s1");

            Assert.That(response.Status, Is.EqualTo("no_content"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SafeExchange.Client.Common.Tests --filter PinnedSecretsApiClientTests`
Expected: FAIL — methods do not exist (compile error).

- [ ] **Step 3: Add the region to ApiClient**

Insert immediately after the `#endregion pinned groups` line in `ApiClient.cs`:

```csharp
        #region pinned secrets

        public async Task<BaseResponseObject<List<PinnedSecretOutput>>> ListPinnedSecretsAsync()
            => await this.ProcessResponseAsync<List<PinnedSecretOutput>>(async () =>
            {
                return await client.GetAsync($"{ApiVersion}/pinnedsecrets-list");
            });

        public async Task<BaseResponseObject<PinnedSecretOutput>> GetPinnedSecretAsync(string secretId)
            => await this.ProcessResponseAsync<PinnedSecretOutput>(async () =>
            {
                return await client.GetAsync($"{ApiVersion}/pinnedsecrets/{secretId}");
            });

        public async Task<BaseResponseObject<PinnedSecretOutput>> PutPinnedSecretAsync(string secretId)
            => await this.ProcessResponseAsync<PinnedSecretOutput>(async () =>
            {
                // PUT carries no body — the secret name is fully in the URL path.
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, $"{ApiVersion}/pinnedsecrets/{secretId}");
                return await client.SendAsync(httpRequestMessage);
            });

        public async Task<BaseResponseObject<string>> DeletePinnedSecretAsync(string secretId)
            => await this.ProcessResponseAsync<string>(async () =>
            {
                return await client.DeleteAsync($"{ApiVersion}/pinnedsecrets/{secretId}");
            });

        #endregion pinned secrets
```

(`PinnedSecretOutput` resolves via the existing `using SafeExchange.Client.Common.Model;`.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SafeExchange.Client.Common.Tests --filter PinnedSecretsApiClientTests`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add SafeExchange.Client.Common/ApiClient/ApiClient.cs SafeExchange.Client.Common.Tests/PinnedSecretsApiClientTests.cs
git commit -m "feat(pinned-secrets): add ApiClient list/get/put/delete methods"
```

---

### Task 4: `PinnedSecretsHelper` toggle helper

Lives in `SafeExchange.Client.Common` (not Web.Components, unlike `GroupsHelper`) so it is unit-testable in the existing test project. It operates on an injected `ISet<string>` of pinned names rather than `StateContainer`, keeping it free of Blazor dependencies. Pages pass `stateContainer.PinnedSecretNames`.

**Files:**
- Create: `SafeExchange.Client.Common/Helpers/PinnedSecretToggleResult.cs`
- Create: `SafeExchange.Client.Common/Helpers/PinnedSecretsHelper.cs`
- Test: `SafeExchange.Client.Common.Tests/PinnedSecretsHelperTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
/// <summary>
/// PinnedSecretsHelperTests — verifies the pin/unpin toggle helper keeps the
/// in-memory pinned-name set in sync and surfaces cap errors. ApiClient is
/// driven through a stubbed HttpMessageHandler.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Helpers;
    using SafeExchange.Client.Common.Tests.Utilities;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    [TestFixture]
    public class PinnedSecretsHelperTests
    {
        [Test]
        public async Task Pin_Success_AddsNameToSet()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\",\"result\":{\"secretName\":\"s1\",\"exists\":true,\"canRead\":true,\"tags\":[]}}");
            var client = new ApiClient(new StubHttpClientFactory(handler));
            var set = new HashSet<string>();

            var result = await PinnedSecretsHelper.SwitchSecretPinAsync(client, set, "s1", true);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(set, Has.Member("s1"));
        }

        [Test]
        public async Task Pin_OverCap_DoesNotAddAndReturnsError()
        {
            var body = "{\"status\":\"error\",\"error\":\"Pinned secret count is 5, which is higher or equal than allowed no. of 5 pinned secrets. Please unpin secrets before adding new ones.\"}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.BadRequest, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));
            var set = new HashSet<string>();

            var result = await PinnedSecretsHelper.SwitchSecretPinAsync(client, set, "s6", true);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("Please unpin secrets"));
            Assert.That(set, Is.Empty);
        }

        [Test]
        public async Task Unpin_Ok_RemovesNameFromSet()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\",\"result\":\"ok\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));
            var set = new HashSet<string> { "s1" };

            var result = await PinnedSecretsHelper.SwitchSecretPinAsync(client, set, "s1", false);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(set, Does.Not.Contain("s1"));
        }

        [Test]
        public async Task Unpin_NoContent_TreatedAsSuccessAndRemoves()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"no_content\",\"result\":\"Pin for secret 's1' does not exist.\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));
            var set = new HashSet<string> { "s1" };

            var result = await PinnedSecretsHelper.SwitchSecretPinAsync(client, set, "s1", false);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(set, Does.Not.Contain("s1"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SafeExchange.Client.Common.Tests --filter PinnedSecretsHelperTests`
Expected: FAIL — helper/result type do not exist.

- [ ] **Step 3: Create the result type**

```csharp
/// <summary>
/// PinnedSecretToggleResult
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    public sealed class PinnedSecretToggleResult
    {
        public bool Succeeded { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? Error { get; init; }
    }
}
```

- [ ] **Step 4: Create the helper**

```csharp
/// <summary>
/// PinnedSecretsHelper
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    using SafeExchange.Client.Common;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public static class PinnedSecretsHelper
    {
        public static async Task<PinnedSecretToggleResult> SwitchSecretPinAsync(
            ApiClient apiClient, ISet<string> pinnedSecretNames, string secretName, bool newPinValue)
        {
            if (newPinValue)
            {
                string status;
                string? error;
                try
                {
                    var response = await apiClient.PutPinnedSecretAsync(secretName);
                    status = response.Status;
                    error = response.Error;
                }
                catch (Exception ex)
                {
                    status = "exception";
                    error = $"{ex.GetType()}: {ex.Message}";
                }

                var succeeded = status == "ok";
                if (succeeded)
                {
                    pinnedSecretNames.Add(secretName);
                }

                return new PinnedSecretToggleResult { Succeeded = succeeded, Status = status, Error = error };
            }
            else
            {
                string status;
                string? error;
                try
                {
                    var response = await apiClient.DeletePinnedSecretAsync(secretName);
                    status = response.Status;
                    error = response.Error;
                }
                catch (Exception ex)
                {
                    status = "exception";
                    error = $"{ex.GetType()}: {ex.Message}";
                }

                // Unpin is idempotent server-side: "no_content" means the pin was
                // already gone, which is still success from the user's point of view.
                var succeeded = status == "ok" || status == "no_content";
                if (succeeded)
                {
                    pinnedSecretNames.Remove(secretName);
                }

                return new PinnedSecretToggleResult { Succeeded = succeeded, Status = status, Error = error };
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SafeExchange.Client.Common.Tests --filter PinnedSecretsHelperTests`
Expected: PASS (4 tests).

- [ ] **Step 6: Run the full Common test suite**

Run: `dotnet test SafeExchange.Client.Common.Tests`
Expected: PASS (all existing + 15 new).

- [ ] **Step 7: Commit**

```bash
git add SafeExchange.Client.Common/Helpers/PinnedSecretsHelper.cs SafeExchange.Client.Common/Helpers/PinnedSecretToggleResult.cs SafeExchange.Client.Common.Tests/PinnedSecretsHelperTests.cs
git commit -m "feat(pinned-secrets): add pin/unpin toggle helper"
```

---

### Task 5: `StateContainer.PinnedSecretNames`

**Files:**
- Modify: `SafeExchange.Client.Web.Components/Classes/StateContainer.cs` (add property near `PinnedGroups`)

- [ ] **Step 1: Add the property**

After the `public List<PinnedGroup> PinnedGroups { get; set; } = [];` line, add:

```csharp
        // Names of secrets the current user has pinned. Source of truth for star
        // state across Home / My Secrets / View. Mutated through PinnedSecretsHelper.
        public HashSet<string> PinnedSecretNames { get; set; } = new HashSet<string>();
```

- [ ] **Step 2: Build**

Run: `dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Client.Web.Components/Classes/StateContainer.cs
git commit -m "feat(pinned-secrets): track pinned secret names on StateContainer"
```

---

### Task 6: Home page Pinned section

**Files:**
- Modify: `SafeExchange.Client.Web.Components/Pages/Index.razor`

Adds, **below** the existing search `EditForm`, a Pinned list-group that fetches `ListPinnedSecretsAsync` on init, seeds `StateContainer.PinnedSecretNames`, and renders one row per pin by state. Unpin via the filled star.

- [ ] **Step 1: Add usings + injects** at the top of `Index.razor`

Replace the existing `@using` / `@inject` header block with:

```razor
@using SafeExchange.Client.Common
@using SafeExchange.Client.Common.Helpers
@using SafeExchange.Client.Common.Model
@using SafeExchange.Client.Web.Components.Model
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication

@inject NavigationManager NavigationManager
@inject StateContainer StateContainer
@inject ApiClient apiClient
```

- [ ] **Step 2: Add the Pinned section markup** immediately after the closing `</EditForm>` of the search form and before `@code {`

```razor
@if (this.pinnedSecrets is not null && this.pinnedSecrets.Count > 0)
{
    <hr />
    <p class="d-flex align-items-center">
        <strong>Pinned</strong>
        <span class="badge text-bg-secondary ms-2">@this.pinnedSecrets.Count</span>
        @if (this.isFetchingPinned)
        {
            <span class="spinner-border spinner-border-sm ms-2" role="status"></span>
        }
    </p>
    <ul class="list-group list-group-flush">
        @foreach (var pin in this.pinnedSecrets)
        {
            var current = pin;
            <li class="list-group-item d-flex justify-content-between align-items-center flex-wrap @(current.State == PinnedSecretState.Live ? string.Empty : "text-muted")">
                <div>
                    @switch (current.State)
                    {
                        case PinnedSecretState.Live:
                            <a href="viewdata/@current.SecretName" class="text-decoration-none">@current.SecretName</a>
                            @if (current.Tags.Count > 0)
                            {
                                <small class="ms-2">@string.Join(", ", current.Tags)</small>
                            }
                            break;
                        case PinnedSecretState.AccessLost:
                            <span>@current.SecretName</span>
                            <small class="text-warning ms-2"><i class="bi bi-exclamation-triangle"></i> no access</small>
                            <a href="addrequest?subject=@current.SecretName&permission=Read" class="ms-2 small">Request access</a>
                            break;
                        case PinnedSecretState.Deleted:
                            <span class="text-decoration-line-through">@current.SecretName</span>
                            <small class="text-danger ms-2"><i class="bi bi-x-circle"></i> deleted</small>
                            break;
                    }
                </div>
                <button type="button" class="btn btn-link text-primary" title="Unpin"
                        disabled="@this.togglingNames.Contains(current.SecretName)"
                        @onclick="() => this.UnpinAsync(current.SecretName)">
                    @if (this.togglingNames.Contains(current.SecretName))
                    {
                        <span class="spinner-border spinner-border-sm" role="status"></span>
                    }
                    else
                    {
                        <i class="bi bi-star-fill"></i>
                    }
                </button>
            </li>
        }
    </ul>
}
```

- [ ] **Step 3: Extend the `@code` block** — replace the existing `@code { ... }` with:

```razor
@code {

    private NotificationData Notification;

    private SearchInput searchInput;

    private List<PinnedSecret> pinnedSecrets;

    private bool isFetchingPinned;

    private readonly HashSet<string> togglingNames = new();

    protected override void OnInitialized()
    {
        this.StateContainer.IsInProgress = false;
        this.StateContainer.SetCurrentPageHeader($"Home");
        this.Notification = this.StateContainer.TakeNotification();
        this.searchInput = new SearchInput();
    }

    protected override async Task OnInitializedAsync()
    {
        await this.FetchPinnedSecretsAsync();
    }

    private async Task FetchPinnedSecretsAsync()
    {
        this.isFetchingPinned = true;
        try
        {
            var response = await this.apiClient.ListPinnedSecretsAsync();
            if ("ok".Equals(response.Status) || "no_content".Equals(response.Status))
            {
                var items = response.Result ?? new List<PinnedSecretOutput>();
                this.pinnedSecrets = items.Select(x => new PinnedSecret(x)).ToList();
                this.StateContainer.PinnedSecretNames = new HashSet<string>(this.pinnedSecrets.Select(p => p.SecretName));
            }
            else
            {
                this.Notification = new NotificationData
                {
                    Type = NotificationType.Warning,
                    Status = response.Status,
                    Message = response.Error
                };
            }
        }
        catch (AccessTokenNotAvailableException exception)
        {
            exception.Redirect();
        }
        finally
        {
            this.isFetchingPinned = false;
        }
    }

    private async Task UnpinAsync(string secretName)
    {
        this.togglingNames.Add(secretName);
        try
        {
            var result = await PinnedSecretsHelper.SwitchSecretPinAsync(
                this.apiClient, this.StateContainer.PinnedSecretNames, secretName, false);
            if (result.Succeeded)
            {
                this.pinnedSecrets?.RemoveAll(p => p.SecretName.Equals(secretName));
            }
            else
            {
                this.Notification = new NotificationData
                {
                    Type = NotificationType.Warning,
                    Status = result.Status,
                    Message = result.Error
                };
            }
        }
        finally
        {
            this.togglingNames.Remove(secretName);
        }
    }

    public void ViewObject()
    {
        if (string.IsNullOrEmpty(this.searchInput.SearchString))
        {
            return;
        }

        NavigationManager.NavigateTo($"viewdata/{this.searchInput.SearchString}");
    }

    public void DismissNotification()
    {
        this.Notification = null;
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add SafeExchange.Client.Web.Components/Pages/Index.razor
git commit -m "feat(pinned-secrets): show pinned favourites on Home page"
```

---

### Task 7: My Secrets per-row star toggle

**Files:**
- Modify: `SafeExchange.Client.Web.Components/Pages/ListData.razor`

Fetches the pinned set alongside the secret list and adds a star toggle to each row's button group.

- [ ] **Step 1: Add usings** — add to the existing `@using` block at the top:

```razor
@using SafeExchange.Client.Common.Helpers
```

- [ ] **Step 2: Add the star button** inside the existing `<div class="btn-group ...">`, immediately after the More-actions dropdown `<ul>...</ul>` closes and before the `</div>` that ends the btn-group, insert:

```razor
                    <button type="button" class="btn btn-outline-primary" title="@(this.IsPinned(secretPermissions.ObjectName) ? "Unpin" : "Pin")"
                            disabled="@(this.togglingNames.Contains(secretPermissions.ObjectName) || this.StateContainer.IsInProgress)"
                            @onclick="() => this.TogglePinAsync(secretPermissions.ObjectName)">
                        @if (this.togglingNames.Contains(secretPermissions.ObjectName))
                        {
                            <span class="spinner-border spinner-border-sm" role="status"></span>
                        }
                        else if (this.IsPinned(secretPermissions.ObjectName))
                        {
                            <i class="bi bi-star-fill"></i>
                        }
                        else
                        {
                            <i class="bi bi-star"></i>
                        }
                    </button>
```

- [ ] **Step 3: Add fields + methods** to the `@code` block — add these members alongside the existing ones:

```csharp
    private readonly HashSet<string> togglingNames = new();

    private bool IsPinned(string objectName) => this.StateContainer.PinnedSecretNames.Contains(objectName);

    private async Task FetchPinnedSecretsAsync()
    {
        try
        {
            var response = await apiClient.ListPinnedSecretsAsync();
            if ("ok".Equals(response.Status) || "no_content".Equals(response.Status))
            {
                var items = response.Result ?? new List<PinnedSecretOutput>();
                this.StateContainer.PinnedSecretNames = new HashSet<string>(items.Select(p => p.SecretName));
            }
        }
        catch (AccessTokenNotAvailableException exception)
        {
            exception.Redirect();
        }
    }

    private async Task TogglePinAsync(string objectName)
    {
        var newValue = !this.IsPinned(objectName);
        this.togglingNames.Add(objectName);
        try
        {
            var result = await PinnedSecretsHelper.SwitchSecretPinAsync(
                this.apiClient, this.StateContainer.PinnedSecretNames, objectName, newValue);
            if (!result.Succeeded)
            {
                this.Notification = new NotificationData
                {
                    Type = NotificationType.Warning,
                    Status = result.Status,
                    Message = result.Error
                };
            }
        }
        finally
        {
            this.togglingNames.Remove(objectName);
        }
    }
```

- [ ] **Step 4: Call `FetchPinnedSecretsAsync` on init** — in `OnInitializedAsync`, after `await this.FetchSecretNamesAsync();` add:

```csharp
        await this.FetchPinnedSecretsAsync();
```

- [ ] **Step 5: Build**

Run: `dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add SafeExchange.Client.Web.Components/Pages/ListData.razor
git commit -m "feat(pinned-secrets): add pin toggle to My Secrets rows"
```

---

### Task 8: View page action-bar star toggle

**Files:**
- Modify: `SafeExchange.Client.Web.Components/Pages/ViewData.razor`

Adds a star toggle to the action bar (beside Refresh / Edit / Give up) reflecting the secret's pin state, checked via `GetPinnedSecretAsync` on load.

- [ ] **Step 1: Add using** — add to the `@using` block:

```razor
@using SafeExchange.Client.Common.Helpers
```

- [ ] **Step 2: Add the star button** inside the action-bar `<div class="btn-group" role="group" aria-label="Item buttons">`, after the Refresh button block and before the Audit-log `@if`:

```razor
                    <button class="btn btn-outline-primary" type="button" title="@(this.isPinned ? "Unpin" : "Pin")"
                            disabled="@(this.isTogglingPin || this.StateContainer.IsInProgress)" @onclick="TogglePinAsync">
                        @if (this.isTogglingPin)
                        {
                            <span class="spinner-border spinner-border-sm" role="status"></span>
                        }
                        else if (this.isPinned)
                        {
                            <i class="bi bi-star-fill"></i>
                        }
                        else
                        {
                            <i class="bi bi-star"></i>
                        }
                        <span>&nbsp;@(this.isPinned ? "Pinned" : "Pin")</span>
                    </button>
```

- [ ] **Step 3: Add fields + methods** to the `@code` block:

```csharp
    private bool isPinned;

    private bool isTogglingPin;

    private async Task FetchPinStateAsync()
    {
        try
        {
            var response = await this.apiClient.GetPinnedSecretAsync(this.ObjectName);
            this.isPinned = "ok".Equals(response.Status) && response.Result is not null;
            if (this.isPinned)
            {
                this.StateContainer.PinnedSecretNames.Add(this.ObjectName);
            }
        }
        catch (AccessTokenNotAvailableException exception)
        {
            exception.Redirect();
        }
    }

    private async Task TogglePinAsync()
    {
        var newValue = !this.isPinned;
        this.isTogglingPin = true;
        try
        {
            var result = await PinnedSecretsHelper.SwitchSecretPinAsync(
                this.apiClient, this.StateContainer.PinnedSecretNames, this.ObjectName, newValue);
            if (result.Succeeded)
            {
                this.isPinned = newValue;
            }
            else
            {
                this.Notification = new NotificationData
                {
                    Type = NotificationType.Warning,
                    Status = result.Status,
                    Message = result.Error
                };
            }
        }
        finally
        {
            this.isTogglingPin = false;
        }
    }
```

- [ ] **Step 4: Call `FetchPinStateAsync` on init** — in `OnInitializedAsync`, after `await this.FetchData();` add:

```csharp
        await this.FetchPinStateAsync();
```

- [ ] **Step 5: Build**

Run: `dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add SafeExchange.Client.Web.Components/Pages/ViewData.razor
git commit -m "feat(pinned-secrets): add pin toggle to View page action bar"
```

---

### Task 9: Full verification, code review, push

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeded (PWA + Components + Common).

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test`
Expected: PASS, including the 15 new pinned-secrets tests.

- [ ] **Step 3: Code review**

Invoke the `superpowers:requesting-code-review` skill (or the `/code-review` command) against the branch diff. Address findings; re-run `dotnet test` after any fix.

- [ ] **Step 4: Push the branch**

```bash
git push -u origin features/pinned-secrets-ui
```

- [ ] **Step 5: Deploy to staging** (see deployment section discovered at execution time).

---

## Self-review notes

- **Spec coverage:** DTO (T1), model+state (T2), ApiClient ×4 (T3), helper+cap (T4), StateContainer set (T5), Home section (T6), My Secrets star (T7), View star (T8), tests throughout, review+push+deploy (T9). All spec sections mapped.
- **Type consistency:** `PinnedSecretsHelper.SwitchSecretPinAsync(ApiClient, ISet<string>, string, bool)` returns `PinnedSecretToggleResult { Succeeded, Status, Error }` — used identically in T6/T7/T8. `PinnedSecretState { Live, AccessLost, Deleted }` used in T2 and T6.
- **Deviation from pinned-groups:** helper is in `Common` (not `Web.Components`) and takes an `ISet<string>` (not `StateContainer`) so it is unit-testable — documented in Task 4.
- **No client cap pre-check:** server is source of truth; the `400` `error` message is surfaced verbatim.

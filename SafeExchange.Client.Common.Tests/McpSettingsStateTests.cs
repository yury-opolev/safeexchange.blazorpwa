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

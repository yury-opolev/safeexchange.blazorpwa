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

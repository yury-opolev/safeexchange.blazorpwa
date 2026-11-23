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

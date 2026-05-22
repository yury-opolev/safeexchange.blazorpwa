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

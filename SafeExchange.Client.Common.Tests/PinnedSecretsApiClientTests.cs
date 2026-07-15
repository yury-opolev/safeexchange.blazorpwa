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
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/pinnedsecrets-list"));
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
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/pinnedsecrets/s1"));
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
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/pinnedsecrets/s1"));
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
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/pinnedsecrets/s1"));
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

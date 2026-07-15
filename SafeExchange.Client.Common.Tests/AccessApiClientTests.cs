/// <summary>
/// AccessApiClientTests — verifies the three access-related ApiClient methods
/// added for the give-up-secret feature: UpdateAccessAsync (PATCH /v2/access),
/// GetGiveUpPreviewAsync (GET /v2/access-giveup), GiveUpAccessAsync (DELETE
/// /v2/access-giveup). Tests assert the request shape (verb + URL + body) and
/// that the response deserialises into the right DTO. They do NOT exercise the
/// backend — the HttpMessageHandler is stubbed.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Common.Tests.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    [TestFixture]
    public class AccessApiClientTests
    {
        // ----------------------------- UpdateAccessAsync -----------------------------

        [Test]
        public async Task UpdateAccessAsync_SendsPatchToCorrectUrl()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\",\"result\":\"ok\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var input = new AccessUpdateInput
            {
                Add = new List<SubjectPermissionsInput>
                {
                    new SubjectPermissionsInput { SubjectType = SubjectTypeInput.User, SubjectName = "alice@test", CanRead = true }
                },
                Remove = new List<SubjectPermissionsInput>
                {
                    new SubjectPermissionsInput { SubjectType = SubjectTypeInput.User, SubjectName = "bob@test", CanRead = true }
                }
            };

            var response = await client.UpdateAccessAsync("secret-1", input);

            Assert.That(handler.CapturedRequest, Is.Not.Null);
            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(new HttpMethod("PATCH")));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/access/secret-1"));
            Assert.That(response.Status, Is.EqualTo("ok"));
        }

        [Test]
        public async Task UpdateAccessAsync_SerialisesAddAndRemoveInBody()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\",\"result\":\"ok\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var input = new AccessUpdateInput
            {
                Add = new List<SubjectPermissionsInput>
                {
                    new SubjectPermissionsInput { SubjectType = SubjectTypeInput.User, SubjectName = "alice@test", CanRead = true }
                },
                Remove = new List<SubjectPermissionsInput>
                {
                    new SubjectPermissionsInput { SubjectType = SubjectTypeInput.Group, SubjectId = "group-guid", SubjectName = "group", CanRevokeAccess = true }
                }
            };

            await client.UpdateAccessAsync("secret-x", input);

            Assert.That(handler.CapturedRequestBody, Is.Not.Null);

            using var doc = JsonDocument.Parse(handler.CapturedRequestBody!);
            var root = doc.RootElement;

            // JsonContent.Create uses JsonSerializerDefaults.Web (camelCase).
            Assert.That(root.GetProperty("add").GetArrayLength(), Is.EqualTo(1));
            Assert.That(root.GetProperty("remove").GetArrayLength(), Is.EqualTo(1));
            Assert.That(root.GetProperty("add")[0].GetProperty("subjectName").GetString(), Is.EqualTo("alice@test"));
            Assert.That(root.GetProperty("remove")[0].GetProperty("subjectId").GetString(), Is.EqualTo("group-guid"));
        }

        [Test]
        public async Task UpdateAccessAsync_PropagatesForbiddenAsStatus()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.Forbidden, "{\"status\":\"forbidden\",\"error\":\"no perm\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.UpdateAccessAsync("secret-1", new AccessUpdateInput
            {
                Add = new List<SubjectPermissionsInput>(),
                Remove = new List<SubjectPermissionsInput>()
            });

            Assert.That(response.Status, Is.EqualTo("forbidden"));
            Assert.That(response.Error, Is.EqualTo("no perm"));
        }

        // ----------------------------- GetGiveUpPreviewAsync -----------------------------

        [Test]
        public async Task GetGiveUpPreviewAsync_SendsGetToCorrectUrl()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"hasDirectRow\":true,\"wouldOrphan\":true,\"currentExpireAt\":null,\"prospectiveExpireAt\":\"2026-05-28T09:00:00Z\"}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetGiveUpPreviewAsync("secret-q");

            Assert.That(handler.CapturedRequest, Is.Not.Null);
            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/access-giveup/secret-q"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result, Is.Not.Null);
            Assert.That(response.Result!.HasDirectRow, Is.True);
            Assert.That(response.Result!.WouldOrphan, Is.True);
            Assert.That(response.Result!.ProspectiveExpireAt, Is.EqualTo(new DateTime(2026, 5, 28, 9, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public async Task GetGiveUpPreviewAsync_FeatureFlagOff_SurfacesNoContentStatus()
        {
            // Backend returns 204 NoContent when UseAccessGiveUp is off.
            // ApiClient.ProcessResponseAsync maps that to Status = HttpStatusCode.ToString() ("NoContent").
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.NoContent);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetGiveUpPreviewAsync("secret-1");

            Assert.That(response.Status, Is.EqualTo("NoContent"));
            Assert.That(response.Result, Is.Null);
        }

        [Test]
        public async Task GetGiveUpPreviewAsync_PreviewWithoutOrphan_DeserialisesCorrectly()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"hasDirectRow\":true,\"wouldOrphan\":false,\"currentExpireAt\":null,\"prospectiveExpireAt\":null}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetGiveUpPreviewAsync("secret-shared");

            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.HasDirectRow, Is.True);
            Assert.That(response.Result!.WouldOrphan, Is.False);
            Assert.That(response.Result!.ProspectiveExpireAt, Is.Null);
        }

        // ----------------------------- GiveUpAccessAsync -----------------------------

        [Test]
        public async Task GiveUpAccessAsync_SendsDeleteToCorrectUrl()
        {
            var body = "{\"status\":\"ok\",\"result\":{\"hadDirectRow\":true,\"wasOrphaned\":true,\"expireAt\":\"2026-05-28T09:00:00Z\"}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GiveUpAccessAsync("secret-z");

            Assert.That(handler.CapturedRequest, Is.Not.Null);
            Assert.That(handler.CapturedRequest!.Method, Is.EqualTo(HttpMethod.Delete));
            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/access-giveup/secret-z"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result!.HadDirectRow, Is.True);
            Assert.That(response.Result!.WasOrphaned, Is.True);
            Assert.That(response.Result!.ExpireAt, Is.EqualTo(new DateTime(2026, 5, 28, 9, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public async Task GiveUpAccessAsync_ForbiddenWhenCallerLacksRow()
        {
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.Forbidden, "{\"status\":\"forbidden\",\"error\":\"no access\"}");
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GiveUpAccessAsync("secret-1");

            Assert.That(response.Status, Is.EqualTo("forbidden"));
            Assert.That(response.Error, Is.EqualTo("no access"));
        }
    }
}

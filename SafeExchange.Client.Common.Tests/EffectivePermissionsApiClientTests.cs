/// <summary>
/// EffectivePermissionsApiClientTests — verifies the web-client half of
/// "Proposition - SafeExchange 001": the secret list and the access endpoint each expose the
/// caller's effective permissions (union of direct and group-derived grants) in a separate
/// `callerEffectivePermissions` property, while the displayed permissions stay the actual grant.
/// These tests pin the wire contract (camelCase property names) — a casing drift between backend
/// and frontend would deserialise to null and fail here. The HttpMessageHandler is stubbed.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Common.Tests.Utilities;
    using System.Collections.Generic;
    using System.Net;

    [TestFixture]
    public class EffectivePermissionsApiClientTests
    {
        [Test]
        public async Task ListSecretMetadataAsync_ExposesActualAndEffectivePermissions()
        {
            var body = "{\"status\":\"ok\",\"result\":[{" +
                       "\"objectName\":\"sec-1\",\"canRead\":true,\"canWrite\":false," +
                       "\"callerEffectivePermissions\":{\"canRead\":true,\"canWrite\":true,\"canGrantAccess\":false,\"canRevokeAccess\":false}}]}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.ListSecretMetadataAsync();

            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/secret-list"));
            Assert.That(response.Status, Is.EqualTo("ok"));
            var item = response.Result!.Single();

            // Displayed permissions are the actual direct grant.
            Assert.That(item.CanRead, Is.True);
            Assert.That(item.CanWrite, Is.False);
            Assert.That(item.PermissionsString, Is.EqualTo("Read"));

            // The separate effective grant carries the group-derived Write.
            Assert.That(item.CallerEffectivePermissions.CanRead, Is.True);
            Assert.That(item.CallerEffectivePermissions.CanWrite, Is.True);
        }

        [Test]
        public async Task ListAccessAsync_ReturnsAccessListAndCallerEffectivePermissions()
        {
            var body = "{\"status\":\"ok\",\"result\":{" +
                       "\"accessList\":[{\"objectName\":\"sec-1\",\"subjectName\":\"alice@test\",\"canRead\":true,\"canWrite\":true}]," +
                       "\"callerEffectivePermissions\":{\"canRead\":true,\"canWrite\":false,\"canGrantAccess\":false,\"canRevokeAccess\":false}}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.ListAccessAsync("sec-1");

            Assert.That(handler.CapturedRequest!.RequestUri!.AbsoluteUri, Does.EndWith("/api/v3/access/sec-1"));
            Assert.That(response.Status, Is.EqualTo("ok"));

            // The access list keeps each subject's actual permissions.
            Assert.That(response.Result!.AccessList.Single().SubjectName, Is.EqualTo("alice@test"));
            Assert.That(response.Result!.AccessList.Single().CanWrite, Is.True);

            // The caller's effective permissions arrive as a separate property.
            Assert.That(response.Result!.CallerEffectivePermissions.CanRead, Is.True);
            Assert.That(response.Result!.CallerEffectivePermissions.CanWrite, Is.False);
        }

        [Test]
        public void ObjectMetadata_FromOutput_DefaultsEffectivePermissionsToNoCapabilities()
        {
            var output = new ObjectMetadataOutput
            {
                ObjectName = "sec-2",
                Content = new List<ContentMetadataOutput>(),
                ExpirationSettings = new ExpirationSettingsOutput(),
                AuditEnabled = false,
            };

            var metadata = new ObjectMetadata(output);

            Assert.That(metadata.EffectivePermissions, Is.Not.Null);
            Assert.That(metadata.EffectivePermissions.CanRead, Is.False);
            Assert.That(metadata.EffectivePermissions.CanWrite, Is.False);
        }

        [Test]
        public void ObjectMetadata_CopyConstructor_PreservesEffectivePermissions()
        {
            var source = new ObjectMetadata
            {
                ObjectName = "sec-3",
                Content = new List<ContentMetadata>(),
                ExpirationMetadata = new ExpirationMetadata(),
                EffectivePermissions = new EffectivePermissions { CanRead = true, CanWrite = true }
            };

            var copy = new ObjectMetadata(source);

            Assert.That(copy.EffectivePermissions.CanRead, Is.True);
            Assert.That(copy.EffectivePermissions.CanWrite, Is.True);
            Assert.That(copy.EffectivePermissions.CanGrantAccess, Is.False);
        }

        [Test]
        public void ObjectMetadata_CopyConstructor_NullEffectivePermissions_YieldsNoCapabilityCopy()
        {
            var source = new ObjectMetadata
            {
                ObjectName = "sec-4",
                Content = new List<ContentMetadata>(),
                ExpirationMetadata = new ExpirationMetadata(),
                EffectivePermissions = null!, // simulate a caller violating the non-null contract
            };

            var copy = new ObjectMetadata(source);

            Assert.That(copy.EffectivePermissions, Is.Not.Null);
            Assert.That(copy.EffectivePermissions.CanRead, Is.False);
            Assert.That(copy.EffectivePermissions.CanWrite, Is.False);
            Assert.That(copy.EffectivePermissions.CanGrantAccess, Is.False);
            Assert.That(copy.EffectivePermissions.CanRevokeAccess, Is.False);
        }
    }
}

/// <summary>
/// CallerPermissionsApiClientTests — verifies the web-client half of
/// "Proposition - SafeExchange 001": the single-secret metadata response now carries the
/// caller's effective permissions (union of direct and group-derived grants), and the client
/// ObjectMetadata model surfaces them so the UI can drive Edit / grant / revoke from the same
/// effective model the API authorizes against — rather than matching the caller's identifier
/// against access-list entries. The HttpMessageHandler is stubbed; the backend is not exercised.
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

    [TestFixture]
    public class CallerPermissionsApiClientTests
    {
        [Test]
        public async Task GetSecretMetadataAsync_DeserialisesCallerPermissions()
        {
            var body = "{\"status\":\"ok\",\"result\":{" +
                       "\"objectName\":\"sec-1\",\"content\":[],\"expirationSettings\":null,\"auditEnabled\":false," +
                       "\"callerPermissions\":{\"canRead\":true,\"canWrite\":true,\"canGrantAccess\":false,\"canRevokeAccess\":false}}}";
            var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new ApiClient(new StubHttpClientFactory(handler));

            var response = await client.GetSecretMetadataAsync("sec-1");

            Assert.That(response.Status, Is.EqualTo("ok"));
            Assert.That(response.Result, Is.Not.Null);
            Assert.That(response.Result!.CallerPermissions, Is.Not.Null);
            Assert.That(response.Result!.CallerPermissions.CanRead, Is.True);
            Assert.That(response.Result!.CallerPermissions.CanWrite, Is.True);
            Assert.That(response.Result!.CallerPermissions.CanGrantAccess, Is.False);
            Assert.That(response.Result!.CallerPermissions.CanRevokeAccess, Is.False);
        }

        [Test]
        public void ObjectMetadata_FromOutput_CarriesCallerPermissions()
        {
            var output = new ObjectMetadataOutput
            {
                ObjectName = "sec-2",
                Content = new List<ContentMetadataOutput>(),
                ExpirationSettings = new ExpirationSettingsOutput(),
                AuditEnabled = false,
                CallerPermissions = new CallerPermissions
                {
                    CanRead = true,
                    CanWrite = false,
                    CanGrantAccess = true,
                    CanRevokeAccess = true,
                }
            };

            var metadata = new ObjectMetadata(output);

            Assert.That(metadata.CallerPermissions.CanRead, Is.True);
            Assert.That(metadata.CallerPermissions.CanWrite, Is.False);
            Assert.That(metadata.CallerPermissions.CanGrantAccess, Is.True);
            Assert.That(metadata.CallerPermissions.CanRevokeAccess, Is.True);
        }

        [Test]
        public void ObjectMetadata_FromOutputWithoutCallerPermissions_DefaultsToNoCapabilities()
        {
            // Additive field: responses that don't carry it (create / update) must map to an
            // all-false capability set, never to elevated capabilities.
            var output = new ObjectMetadataOutput
            {
                ObjectName = "sec-3",
                Content = new List<ContentMetadataOutput>(),
                ExpirationSettings = new ExpirationSettingsOutput(),
                AuditEnabled = false,
                CallerPermissions = null
            };

            var metadata = new ObjectMetadata(output);

            Assert.That(metadata.CallerPermissions, Is.Not.Null);
            Assert.That(metadata.CallerPermissions.CanRead, Is.False);
            Assert.That(metadata.CallerPermissions.CanWrite, Is.False);
            Assert.That(metadata.CallerPermissions.CanGrantAccess, Is.False);
            Assert.That(metadata.CallerPermissions.CanRevokeAccess, Is.False);
        }

        [Test]
        public void ObjectMetadata_CopyConstructor_PreservesCallerPermissions()
        {
            var source = new ObjectMetadata
            {
                ObjectName = "sec-4",
                Content = new List<ContentMetadata>(),
                ExpirationMetadata = new ExpirationMetadata(),
                CallerPermissions = new CallerPermissions { CanRead = true, CanWrite = true }
            };

            var copy = new ObjectMetadata(source);

            Assert.That(copy.CallerPermissions.CanRead, Is.True);
            Assert.That(copy.CallerPermissions.CanWrite, Is.True);
            Assert.That(copy.CallerPermissions.CanGrantAccess, Is.False);
        }
    }
}

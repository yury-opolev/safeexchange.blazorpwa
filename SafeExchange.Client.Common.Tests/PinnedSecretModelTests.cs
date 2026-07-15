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

        [Test]
        public void PermissionsString_ReflectsFlags()
        {
            var model = new PinnedSecret(new PinnedSecretOutput
            {
                SecretName = "s4", Exists = true, CanRead = true, CanWrite = true
            });

            Assert.That(model.PermissionsString, Is.EqualTo("Read,Write"));
        }

        [Test]
        public void PermissionsString_Empty_WhenNoFlags()
        {
            var model = new PinnedSecret(new PinnedSecretOutput
            {
                SecretName = "s5", Exists = true, CanRead = false
            });

            Assert.That(model.PermissionsString, Is.EqualTo(string.Empty));
        }

        [Test]
        public void State_And_Permissions_FromEffective_WhenGroupOnlyReadable()
        {
            // Actual direct grant is empty, but the caller can read/write via a group, so the pinned
            // secret is Live (not AccessLost) and shows the effective permissions.
            var model = new PinnedSecret(new PinnedSecretListItemOutput
            {
                SecretName = "s6",
                Exists = true,
                CanRead = false,
                CallerEffectivePermissions = new EffectivePermissions { CanRead = true, CanWrite = true },
                Tags = new List<string> { "prod" }
            });

            Assert.That(model.State, Is.EqualTo(PinnedSecretState.Live));
            Assert.That(model.PermissionsString, Is.EqualTo("Read,Write"));
            Assert.That(model.Tags, Has.Member("prod"));
        }
    }
}

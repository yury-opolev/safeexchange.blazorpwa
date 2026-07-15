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

        // For a pinned secret the user cares about what they can effectively do with it, so the
        // Live / "no access" state and permissions text are driven by the caller's effective grant
        // (direct unioned with group-derived) rather than only a direct assignment.
        public PinnedSecret(PinnedSecretListItemOutput source)
        {
            this.SecretName = source.SecretName;
            this.Exists = source.Exists;

            var effective = source.CallerEffectivePermissions ?? new EffectivePermissions();
            this.CanRead = effective.CanRead;
            this.CanWrite = effective.CanWrite;
            this.CanGrantAccess = effective.CanGrantAccess;
            this.CanRevokeAccess = effective.CanRevokeAccess;
            this.Tags = source.Tags ?? new List<string>();
        }

        public string SecretName { get; set; } = string.Empty;

        public bool Exists { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

        public string PermissionsString
            => PermissionsConverter.ToPermissionString(this.CanRead, this.CanWrite, this.CanGrantAccess, this.CanRevokeAccess);

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

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

        public string SecretName { get; set; } = string.Empty;

        public bool Exists { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

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

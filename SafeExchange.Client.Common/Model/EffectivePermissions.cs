/// <summary>
/// EffectivePermissions
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    /// <summary>
    /// The current caller's effective permissions on a secret (direct unioned with group-derived),
    /// as calculated by the API. Drives client capability checks (Edit / grant / revoke) only; the
    /// API remains the authorization boundary.
    /// </summary>
    public class EffectivePermissions
    {
        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }
    }
}

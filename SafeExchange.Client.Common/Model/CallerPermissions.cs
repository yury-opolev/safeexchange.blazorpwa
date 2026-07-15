/// <summary>
/// CallerPermissions
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    /// <summary>
    /// The current caller's effective permissions on a secret — the union of direct and
    /// group-derived grants, as calculated by the API. Used to drive UI capability checks
    /// (Edit / grant / revoke) so the client presents the same effective model the API
    /// authorizes against, instead of re-deriving capabilities from the access-control list.
    /// </summary>
    public class CallerPermissions
    {
        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }
    }
}

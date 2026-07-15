/// <summary>
/// AccessListOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// The GET access/{secretId} response: the secret's full access list (each subject's actual
    /// permissions) plus the current caller's effective permissions, used for capability checks only.
    /// </summary>
    public class AccessListOutput
    {
        public List<SubjectPermissionsOutput> AccessList { get; set; } = new();

        public EffectivePermissions CallerEffectivePermissions { get; set; } = new();
    }
}

/// <summary>
/// SecretListItemOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using SafeExchange.Client.Common;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One entry in the caller's secret list. The Can* flags are the caller's actual direct grant
    /// (shown to the user); CallerEffectivePermissions is the effective grant (direct unioned with
    /// group-derived) and is used to drive capability controls (Edit) only.
    /// </summary>
    public class SecretListItemOutput
    {
        public string ObjectName { get; set; }

        public SubjectTypeOutput SubjectType { get; set; }

        public string SubjectName { get; set; }

        public string SubjectId { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        public List<string> Tags { get; set; } = new();

        public EffectivePermissions CallerEffectivePermissions { get; set; } = new();

        [JsonIgnore]
        public string PermissionsString
            => PermissionsConverter.ToPermissionString(this.CanRead, this.CanWrite, this.CanGrantAccess, this.CanRevokeAccess);
    }
}

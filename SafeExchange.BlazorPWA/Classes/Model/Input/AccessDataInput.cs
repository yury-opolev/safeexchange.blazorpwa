/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Model
{
    using SafeExchange.BlazorPWA.Helpers;

    public class AccessDataInput
    {
        public string Subject { get; set; }

        public string Permission { get; set; }

        public static string CreatePermissionsString(PermissionsData permissions)
        {
            return PermissionsStringBuilder.CreatePermissionsString(permissions.CanRead, permissions.CanWrite, permissions.CanGrantAccess, permissions.CanRevokeAccess);
        }
    }
}

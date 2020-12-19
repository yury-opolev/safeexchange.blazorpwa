/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using SafeExchange.Client.Web.Components.Helpers;
    using System.ComponentModel.DataAnnotations;

    public class AccessDataInput
    {
        [RegularExpression(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$", ErrorMessage = "Email-like identifier required.")]
        public string Subject { get; set; }

        public string Permission { get; set; }

        public static string CreatePermissionsString(PermissionsData permissions)
        {
            return PermissionsStringBuilder.CreatePermissionsString(permissions.CanRead, permissions.CanWrite, permissions.CanGrantAccess, permissions.CanRevokeAccess);
        }
    }
}

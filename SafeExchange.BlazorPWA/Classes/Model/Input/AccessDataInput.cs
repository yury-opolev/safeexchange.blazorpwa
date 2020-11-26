/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Model
{
    using System;
    using System.Text;

    public class AccessDataInput
    {
        public string Subject { get; set; }

        public string Permission { get; set; }

        public static string CreatePermissionsString(PermissionsData permissions)
        {
            var prefix = string.Empty;
            var stringBuilder = new StringBuilder();
            if (permissions.CanRead)
            {
                stringBuilder.Append($"{prefix}Read");
                prefix = ",";
            }
            if (permissions.CanWrite)
            {
                stringBuilder.Append($"{prefix}Write");
                prefix = ",";
            }
            if (permissions.CanGrantAccess)
            {
                stringBuilder.Append($"{prefix}GrantAccess");
                prefix = ",";
            }
            if (permissions.CanRevokeAccess)
            {
                stringBuilder.Append($"{prefix}RevokeAccess");
                prefix = ",";
            }
            return stringBuilder.ToString();
        }
    }
}

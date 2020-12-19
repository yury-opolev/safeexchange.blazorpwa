/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Helpers
{
    using System.Text;

    public static class PermissionsStringBuilder
    {
        public static string CreatePermissionsString(bool canRead, bool canWrite, bool canGrantAccess, bool canRevokeAccess)
        {
            var prefix = string.Empty;
            var stringBuilder = new StringBuilder();
            if (canRead)
            {
                stringBuilder.Append($"{prefix}Read");
                prefix = ",";
            }
            if (canWrite)
            {
                stringBuilder.Append($"{prefix}Write");
                prefix = ",";
            }
            if (canGrantAccess)
            {
                stringBuilder.Append($"{prefix}GrantAccess");
                prefix = ",";
            }
            if (canRevokeAccess)
            {
                stringBuilder.Append($"{prefix}RevokeAccess");
                prefix = ",";
            }
            return stringBuilder.ToString();
        }
    }
}

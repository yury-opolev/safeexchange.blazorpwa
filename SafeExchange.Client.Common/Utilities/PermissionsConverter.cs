/// <summary>
/// PermissionsConverter
/// </summary>

namespace SafeExchange.Client.Common
{
    using System;
    using System.Text;

    public static class PermissionsConverter
    {
        public static bool TryParsePermissionString(string permissions, out bool canRead, out bool canWrite, out bool canGrantAccess, out bool canRevokeAccess)
        {
            canRead = false;
            canWrite = false;
            canGrantAccess = false;
            canRevokeAccess = false;

            var parts = permissions.Split(",", StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Trim().Equals("Read", StringComparison.InvariantCultureIgnoreCase))
                {
                    canRead = true;
                }
                else if (part.Trim().Equals("Write", StringComparison.InvariantCultureIgnoreCase))
                {
                    canWrite = true;
                }
                else if (part.Trim().Equals("GrantAccess", StringComparison.InvariantCultureIgnoreCase))
                {
                    canGrantAccess = true;
                }
                else if (part.Trim().Equals("RevokeAccess", StringComparison.InvariantCultureIgnoreCase))
                {
                    canRevokeAccess = true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static string ToPermissionString(bool canRead, bool canWrite, bool canGrantAccess, bool canRevokeAccess)
        {
            var builder = new StringBuilder();
            var prefix = string.Empty;
            if (canRead)
            {
                builder.Append($"Read");
                prefix = ",";
            }

            if (canWrite)
            {
                builder.Append($"{prefix}Write");
                prefix = ",";
            }

            if (canGrantAccess)
            {
                builder.Append($"{prefix}GrantAccess");
                prefix = ",";
            }

            if (canRevokeAccess)
            {
                builder.Append($"{prefix}RevokeAccess");
            }

            return builder.ToString();
        }
    }
}

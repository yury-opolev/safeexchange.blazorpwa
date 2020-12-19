/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using SafeExchange.Client.Web.Components.Helpers;

    public class SecretDescriptionData
    {
        public string ObjectName { get; set; }

        public string UserName { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        public string CreatePermissionsString()
        {
            return PermissionsStringBuilder.CreatePermissionsString(this.CanRead, this.CanWrite, this.CanGrantAccess, this.CanRevokeAccess);
        }
    }
}

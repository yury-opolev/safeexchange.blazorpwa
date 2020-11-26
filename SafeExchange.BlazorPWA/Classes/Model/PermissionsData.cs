/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Model
{
    using System;

    public class PermissionsData
    {
        public string UserName { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }
    }
}

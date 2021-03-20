/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class AccessRequestData
    {
        public string UserName { get; set; }

        public string SecretName { get; set; }

        public string Permissions { get; set; }

        public DateTime RequestedAt { get; set; }
    }
}

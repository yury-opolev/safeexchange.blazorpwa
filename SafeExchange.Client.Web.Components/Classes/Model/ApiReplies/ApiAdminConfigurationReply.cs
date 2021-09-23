/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class ApiAdminConfigurationReply
    {
        public string Status { get; set; }

        public ServiceConfigurationBundle Result { get; set; }

        public string Error { get; set; }
    }
}

/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class ApiAdminStatusReply
    {
        public string Status { get; set; }

        public AdminStatus Result { get; set; }

        public string Error { get; set; }
    }
}

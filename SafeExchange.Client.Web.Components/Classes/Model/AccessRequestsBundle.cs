/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;
    using System.Collections.Generic;

    public class AccessRequestsBundle
    {
        public string UserId { get; set; }

        public DateTime LastUpdated { get; set; }

        public List<AccessRequestData> Data { get; set; }
    }
}

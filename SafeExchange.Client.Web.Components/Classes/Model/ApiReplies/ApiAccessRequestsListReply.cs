/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System.Collections.Generic;

    public class ApiAccessRequestsListReply
    {
        public string Status { get; set; }

        public List<AccessRequestData> AccessRequests { get; set; }

        public string Error { get; set; }
    }
}

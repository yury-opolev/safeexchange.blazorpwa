/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class AccessRequestProcessingDataInput
    {
        public string SecretId { get; set; }

        public string RequestId { get; set; }

        public bool Grant { get; set; }
    }
}

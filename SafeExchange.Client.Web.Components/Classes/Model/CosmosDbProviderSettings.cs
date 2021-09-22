/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class CosmosDbProviderSettings
    {
        public string SubscriptionId { get; set; }

        public string ResourceGroupName { get; set; }

        public string AccountName { get; set; }

        public string CosmosDbEndpoint { get; set; }

        public string DatabaseName { get; set; }
    }
}

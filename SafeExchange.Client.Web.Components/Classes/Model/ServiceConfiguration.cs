/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class ServiceConfiguration
    {
        public Features Features { get; set; }

        public string WhitelistedGroups { get; set; }

        public CosmosDbProviderSettings CosmosDb { get; set; }

        public string AdminGroups { get; set; }
    }
}

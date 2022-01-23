/// <summary>
/// ObjectMetadataOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Collections.Generic;

    public class ObjectMetadataOutput
    {
        public string ObjectName { get; set; }

        public List<ContentMetadataOutput> Content { get; set; }

        public ExpirationSettingsOutput ExpirationSettings { get; set; }
    }
}

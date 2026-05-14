/// <summary>
/// MetaCreationInput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class MetadataCreationInput
    {
        public ExpirationSettingsInput ExpirationSettings { get; set; }

        public bool? AuditEnabled { get; set; }
    }
}

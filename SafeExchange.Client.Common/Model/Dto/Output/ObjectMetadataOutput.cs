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

        public bool AuditEnabled { get; set; }

        /// <summary>
        /// The current caller's effective permissions on this secret. Populated by the
        /// single-secret read endpoint; null on responses that do not carry it.
        /// </summary>
        public CallerPermissions CallerPermissions { get; set; }
    }
}

/// <summary>
/// SecretAuditPageOutput — paged audit response shape from
/// GET /v2/secret/{secretId}/audit.
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System.Collections.Generic;

    public class SecretAuditPageOutput
    {
        public bool AuditEnabled { get; set; }

        public List<SecretAuditEventOutput> Events { get; set; } = new();

        public string? NextContinuation { get; set; }
    }
}

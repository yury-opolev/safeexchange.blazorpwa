/// <summary>
/// Admin DTOs as the client sees them. Mirror SafeExchange.Core.Model.Dto.Output
/// shapes; kept separate so the client doesn't take a project reference back
/// to the server project.
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Collections.Generic;

    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public bool HasMore { get; set; }
    }

    public class UserOverview
    {
        public string AadUpn { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }

    public class ApplicationAdminOverview
    {
        public string DisplayName { get; set; } = string.Empty;
        public string AadClientId { get; set; } = string.Empty;
        public string AadTenantId { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public int OwnerCount { get; set; }
        public bool OwnersAttentionRequired { get; set; }
    }

    public class SecretAuditAnchorOverview
    {
        public string AuditInstanceId { get; set; } = string.Empty;
        public string SecretObjectName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public bool IsHistorical { get; set; }
    }

    public class EnabledToggleRequest
    {
        public bool Enabled { get; set; }
    }
}

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

    public class SecretAdminOverview
    {
        public string ObjectName { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? IdleDeleteAt { get; set; }
        public int AttachmentCount { get; set; }
        public string[] Tags { get; set; } = [];
        public bool AuditEnabled { get; set; }
    }

    public class SecretAdminDetail : SecretAdminOverview
    {
        public DateTime ModifiedAt { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
        public bool KeepInStorage { get; set; }
        public string AuditInstanceId { get; set; } = string.Empty;
    }

    public class SecretAccessItem
    {
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectType { get; set; } = string.Empty;
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanGrantAccess { get; set; }
        public bool CanRevokeAccess { get; set; }
    }

    public class UserDetail
    {
        public string AadUpn { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string Id { get; set; } = string.Empty;
        public string AadObjectId { get; set; } = string.Empty;
        public string AadTenantId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool ReceiveExternalNotifications { get; set; }
        public bool ConsentRequired { get; set; }
    }

    public class EnabledToggleRequest
    {
        public bool Enabled { get; set; }
    }
}

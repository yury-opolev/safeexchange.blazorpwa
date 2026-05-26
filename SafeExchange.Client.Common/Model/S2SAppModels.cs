/// <summary>
/// S2S app DTOs as the client sees them. Mirror the server contracts in
/// SafeExchange.Core.Model.Dto.{Input,Output} — kept separate so the client
/// doesn't take a dependency on the server project.
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Collections.Generic;

    public enum OwnerSubjectType
    {
        User = 0,
        Group = 1,
    }

    public class S2SAppOwnerInput
    {
        public OwnerSubjectType SubjectType { get; set; }
        public string SubjectId { get; set; } = string.Empty;
    }

    public class S2SAppRegistrationRequest
    {
        public string DisplayName { get; set; } = string.Empty;
        public string AadClientId { get; set; } = string.Empty;
        public string? AadTenantId { get; set; }
        public string? ContactEmail { get; set; }
        public List<S2SAppOwnerInput> AdditionalOwners { get; set; } = new();
    }

    public class S2SAppOwner
    {
        public OwnerSubjectType SubjectType { get; set; }
        public string SubjectId { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
    }

    public class S2SApp
    {
        public string DisplayName { get; set; } = string.Empty;
        public string AadTenantId { get; set; } = string.Empty;
        public string AadClientId { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<S2SAppOwner> Owners { get; set; } = new();
    }

    public class S2SAppOverview
    {
        public string DisplayName { get; set; } = string.Empty;
        public string AadClientId { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public int OwnerCount { get; set; }
        /// <summary>True iff the caller registered this app; false = added later as a co-owner.</summary>
        public bool IsRegistrar { get; set; }
    }
}

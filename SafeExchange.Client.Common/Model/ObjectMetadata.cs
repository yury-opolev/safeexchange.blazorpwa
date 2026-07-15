/// <summary>
/// ObjectMetadata
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    public class ObjectMetadata
    {
        public ObjectMetadata() { }

        public ObjectMetadata(ObjectMetadata source)
        {
            this.ObjectName = source.ObjectName;

            this.Content = new List<ContentMetadata>(source.Content.Count);
            this.Content.AddRange(source.Content.Select(c => new ContentMetadata(c)));

            this.ExpirationMetadata = new ExpirationMetadata(source.ExpirationMetadata);

            this.AuditEnabled = source.AuditEnabled;

            this.CallerPermissions = new CallerPermissions
            {
                CanRead = source.CallerPermissions.CanRead,
                CanWrite = source.CallerPermissions.CanWrite,
                CanGrantAccess = source.CallerPermissions.CanGrantAccess,
                CanRevokeAccess = source.CallerPermissions.CanRevokeAccess,
            };
        }

        public ObjectMetadata(ObjectMetadataOutput source)
        {
            this.ObjectName = source.ObjectName;

            this.Content = new List<ContentMetadata>(source.Content.Count);
            this.Content.AddRange(source.Content.Select(c => new ContentMetadata(c)));

            this.ExpirationMetadata = new ExpirationMetadata(source.ExpirationSettings);

            this.AuditEnabled = source.AuditEnabled;

            // Additive field: the API only populates it on the single-secret read path,
            // so fall back to an all-false capability set for responses without it.
            this.CallerPermissions = source.CallerPermissions ?? new CallerPermissions();
        }

        private static List<ContentMetadata> CreateContent()
        {
            var mainContent = new ContentMetadata()
            {
                ContentName = string.Empty,

                ContentType = string.Empty,
                FileName = string.Empty,

                Chunks = new List<ChunkMetadata>()
            };

            return new List<ContentMetadata> { mainContent };
        }

        [StringLength(100, ErrorMessage = "Value too long (100 character limit).")]
        [RegularExpression(@"^[0-9a-zA-Z-]+$", ErrorMessage = "Only letters, numbers and hyphens are allowed.")]
        public string ObjectName { get; set; }

        public List<ContentMetadata> Content { get; set; }

        public ExpirationMetadata ExpirationMetadata { get; set; }

        // Default true so freshly-constructed metadata (the new-secret form path)
        // opts new secrets into auditing unless the user unchecks the checkbox.
        public bool AuditEnabled { get; set; } = true;

        // The caller's effective permissions on this secret (direct unioned with
        // group-derived). Drives the UI capability checks. Defaults to no capabilities
        // until the read endpoint populates it.
        public CallerPermissions CallerPermissions { get; set; } = new();

        public MetadataCreationInput ToCreationDto() => new MetadataCreationInput()
        {
            ExpirationSettings = this.ExpirationMetadata.ToDto(),
            AuditEnabled = this.AuditEnabled,
        };

        public MetadataUpdateInput ToUpdateDto() => new MetadataUpdateInput()
        {
            ExpirationSettings = this.ExpirationMetadata.ToDto()
        };
    }
}

/// <summary>
/// ObjectMetadata
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class ObjectMetadata
    {
        public ObjectMetadata() { }

        public ObjectMetadata(ObjectMetadata source)
        {
            this.ObjectName = source.ObjectName;

            this.Content = new List<ContentMetadata>(source.Content.Count);
            this.Content.AddRange(source.Content.Select(c => new ContentMetadata(c)));

            this.ExpirationMetadata = new ExpirationMetadata(source.ExpirationMetadata);
        }

        public ObjectMetadata(ObjectMetadataOutput source)
        {
            this.ObjectName = source.ObjectName;

            this.Content = new List<ContentMetadata>(source.Content.Count);
            this.Content.AddRange(source.Content.Select(c => new ContentMetadata(c)));

            this.ExpirationMetadata = new ExpirationMetadata(source.ExpirationSettings);
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

        public MetadataCreationInput ToCreationDto() => new ()
        {
            ExpirationSettings = this.ExpirationMetadata.ToDto()
        };

        public MetadataUpdateInput ToUpdateDto() => new()
        {
            ExpirationSettings = this.ExpirationMetadata.ToDto()
        };
    }
}

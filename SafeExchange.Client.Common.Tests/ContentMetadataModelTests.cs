/// <summary>
/// ContentMetadataModelTests — verifies the IsImage marker (images-as-attachments
/// spike) round-trips through the output DTO and the creation DTO.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common.Model;
    using System.Collections.Generic;

    [TestFixture]
    public class ContentMetadataModelTests
    {
        [Test]
        public void IsImage_FromOutput_AndToCreationDto()
        {
            var fromOutput = new ContentMetadata(new ContentMetadataOutput
            {
                ContentName = "BLOB-1",
                IsMain = false,
                IsImage = true,
                ContentType = "image/png",
                FileName = "passport.png",
                IsReady = true,
                Chunks = new List<ChunkOutput>()
            });
            Assert.That(fromOutput.IsImage, Is.True);

            var creationDto = new ContentMetadata
            {
                IsImage = true,
                ContentType = "image/png",
                FileName = "passport.png"
            }.ToCreationDto();
            Assert.That(creationDto.IsImage, Is.True);

            var fileCreationDto = new ContentMetadata
            {
                IsImage = false,
                ContentType = "application/pdf",
                FileName = "doc.pdf"
            }.ToCreationDto();
            Assert.That(fileCreationDto.IsImage, Is.False);
        }
    }
}

/// <summary>
/// ByteArrayInputFileModel
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System.IO;
    using System.Threading;

    /// <summary>
    /// In-memory <see cref="InputFileModel"/> backed by a byte array. Used to upload
    /// inline images (extracted from base64 in the note) through the same attachment
    /// path as file uploads — see ApiClient.ExtractInlineImagesAsync.
    /// </summary>
    public class ByteArrayInputFileModel : InputFileModel
    {
        private readonly byte[] data;

        public ByteArrayInputFileModel(string name, string contentType, byte[] data)
        {
            this.Name = name;
            this.ContentType = contentType;
            this.data = data;
        }

        public override string ContentType { get; }

        public override string Name { get; }

        public override long Size => this.data.Length;

        public override Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(this.data, writable: false);
    }
}

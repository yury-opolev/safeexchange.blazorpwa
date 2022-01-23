/// <summary>
/// PartialStreamContent
/// </summary>

namespace SafeExchange.Client.Common
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class PartialStreamContent : StreamContent
    {
        private const int defaultBufferSize = 10 * 4096;

        private readonly Stream content;

        private readonly int bufferSize;

        private readonly int uploadSize;

        public int UploadedSize { get; private set; }

        public bool ContentEnded { get; private set; }

        public PartialStreamContent(Stream content, int uploadSize) : base(content)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.bufferSize = defaultBufferSize;
            this.uploadSize = (uploadSize > 0) ? uploadSize : throw new ArgumentException($"Incorrect '{nameof(uploadSize)}' value provided");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[bufferSize];
            var toUploadSize = this.uploadSize;
            while (toUploadSize > 0)
            {
                var sizeToRead = Math.Min(toUploadSize, bufferSize);
                var readSize = await this.content.ReadAsync(buffer, 0, sizeToRead);
                if (readSize <= 0)
                {
                    this.ContentEnded = true;
                    break;
                }

                await stream.WriteAsync(buffer, 0, readSize);

                this.UploadedSize += readSize;
                toUploadSize -= readSize;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = this.uploadSize;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.ContentEnded)
                {
                    this.content.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}

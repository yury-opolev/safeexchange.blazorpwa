/// <summary>
/// SecretContentStream
/// </summary>

namespace SafeExchange.Client.Common
{
    using SafeExchange.Client.Common.Model;
    using System;

    public class SecretContentStream : Stream
    {
        private readonly ApiClient apiClient;

        private readonly string secretId;

        private readonly string contentId;

        private readonly List<ChunkMetadata> chunks;

        private long length;
        private long position;

        private int currentChunkIndex;
        private long currentChunkPosition;

        private ChunkMetadata currentChunk;

        private Stream currentSourceStream;

        public SecretContentStream(ApiClient apiClient, string secretId, ContentMetadata secretContent)
        {
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.secretId = secretId ?? throw new ArgumentNullException(nameof(secretId));
            if (secretContent is null)
            {
                throw new ArgumentNullException(nameof(secretContent));
            }

            this.contentId = secretContent.ContentName;
            this.chunks = new(secretContent.Chunks);

            this.length = 0;
            foreach (var chunk in this.chunks)
            {
                this.length += chunk.Length;
            }

            this.currentChunkIndex = -1;
            this.currentChunkPosition = 0L;
        }

        public override bool CanRead
        {
            get
            {
                return this.currentChunkIndex < (this.chunks.Count - 1) ||
                    this.currentChunkPosition < this.currentChunk.Length;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return this.length;
            }
        }

        public override long Position 
        {
            get
            {
                return this.position;
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.currentChunk == null || this.currentChunkPosition == this.currentChunk.Length)
            {
                this.currentChunkIndex += 1;
                this.currentChunk = this.chunks[this.currentChunkIndex];
                this.currentChunkPosition = 0;
                this.currentSourceStream = this.GetCurrentChunkStreamAsync().GetAwaiter().GetResult();
            }

            var toRead = Math.Min(count, this.currentChunk.Length - this.currentChunkPosition);
            var readBytes = this.currentSourceStream.Read(buffer, offset, (int)toRead);
            this.position += readBytes;
            this.currentChunkPosition += readBytes;

            return readBytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private async Task<Stream> GetCurrentChunkStreamAsync()
        {
            if (this.currentChunk == null)
            {
                throw new InvalidOperationException("Current chunk is null.");
            }

            if (this.currentSourceStream != null)
            {
                this.currentSourceStream.Dispose();
                this.currentSourceStream = null;
            }

            var streamResponse = await this.apiClient.GetSecretDataStreamAsync(this.secretId, this.contentId, this.currentChunk.ChunkName);
            if (!"ok".Equals(streamResponse.Status))
            {
                throw new InvalidOperationException($"Could not get stream for chunk '{this.currentChunk.ChunkName}'.");
            }

            if (streamResponse.Result == null)
            {
                throw new InvalidOperationException($"Stream for chunk '{this.currentChunk.ChunkName}' is null.");
            }

            return streamResponse.Result;
        }
    }
}

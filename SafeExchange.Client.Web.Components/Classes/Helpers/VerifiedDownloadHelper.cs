/// <summary>
/// VerifiedDownloadHelper
/// </summary>

namespace SafeExchange.Client.Web.Components.Helpers
{
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Microsoft.JSInterop;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Model;

    public sealed class VerifiedDownloadHelper
    {
        private const int ReadBufferSize = 64 * 1024;

        private readonly IJSRuntime jsRuntime;
        private readonly ApiClient apiClient;

        public VerifiedDownloadHelper(IJSRuntime jsRuntime, ApiClient apiClient)
        {
            this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public async Task<VerifiedDownloadResult> DownloadAsync(
            string secretId,
            ContentMetadata content,
            IProgress<VerifiedDownloadProgress>? progress = null)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var handle = await this.jsRuntime.InvokeAsync<string>(
                "saexDownload.startVerifiedSave", content.FileName, content.ContentType);

            using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long totalRead = 0;
            long totalSize = 0;
            foreach (var c in content.Chunks)
            {
                totalSize += c.Length;
            }

            try
            {
                for (var chunkIndex = 0; chunkIndex < content.Chunks.Count; chunkIndex++)
                {
                    var chunk = content.Chunks[chunkIndex];
                    using var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var stream = await this.apiClient.GetSecretDataStreamAsync(secretId, content.ContentName, chunk.ChunkName);
                    if (!"ok".Equals(stream.Status) || stream.Result is null)
                    {
                        await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
                        return VerifiedDownloadResult.Failed(chunkIndex, content.Chunks.Count, "Chunk fetch failed.");
                    }

                    var buffer = new byte[ReadBufferSize];
                    int read;
                    while ((read = await stream.Result.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                    {
                        chunkHasher.AppendData(buffer, 0, read);
                        fileHasher.AppendData(buffer, 0, read);

                        // Blazor WASM marshals byte[] (but NOT ArraySegment<byte>) to a JS
                        // Uint8Array. An ArraySegment comes through as a JSON array of numbers,
                        // which the Blob constructor then stringifies as "65,116,…" instead of
                        // the raw bytes. Allocate a right-sized slice so the JS side receives
                        // a Uint8Array of exactly `read` bytes.
                        var slice = read == buffer.Length ? buffer : buffer[..read];
                        await this.jsRuntime.InvokeVoidAsync("saexDownload.writeBlock", handle, slice);
                        totalRead += read;
                        progress?.Report(new VerifiedDownloadProgress(totalRead, totalSize, chunkIndex, content.Chunks.Count));
                    }

                    var chunkHex = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(chunk.Hash) && !chunkHex.Equals(chunk.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
                        return VerifiedDownloadResult.Failed(chunkIndex, content.Chunks.Count, $"Chunk {chunkIndex + 1} hash mismatch.");
                    }
                }

                var fileHex = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();
                if (!string.IsNullOrEmpty(content.Hash) && !fileHex.Equals(content.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
                    return VerifiedDownloadResult.Failed(-1, content.Chunks.Count, "Whole-content hash mismatch.");
                }

                await this.jsRuntime.InvokeVoidAsync("saexDownload.finalize", handle);
                return VerifiedDownloadResult.Success(fileHex);
            }
            catch
            {
                await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
                throw;
            }
        }
    }

    public readonly record struct VerifiedDownloadProgress(long BytesWritten, long TotalBytes, int CurrentChunk, int TotalChunks);

    public sealed class VerifiedDownloadResult
    {
        private VerifiedDownloadResult(bool ok, string? hash, int? failedAtChunk, int chunkCount, string? error)
        {
            this.IsSuccess = ok;
            this.ComputedHash = hash;
            this.FailedAtChunk = failedAtChunk;
            this.ChunkCount = chunkCount;
            this.Error = error;
        }

        public bool IsSuccess { get; }

        public string? ComputedHash { get; }

        public int? FailedAtChunk { get; }

        public int ChunkCount { get; }

        public string? Error { get; }

        public static VerifiedDownloadResult Success(string hash) => new(true, hash, null, 0, null);

        public static VerifiedDownloadResult Failed(int atChunk, int totalChunks, string error)
            => new(false, null, atChunk, totalChunks, error);
    }
}

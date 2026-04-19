/// <summary>
/// LocalFileVerifier
/// </summary>

namespace SafeExchange.Client.Web.Components.Helpers
{
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components.Forms;

    /// <summary>
    /// Streams a user-picked local file through SHA-256 incrementally and returns the
    /// lowercase hex digest. No network involvement — purely client-local.
    /// Used by the attachment row's "Verify local file…" action.
    /// </summary>
    public sealed class LocalFileVerifier
    {
        private const int ReadBufferSize = 64 * 1024;
        private const long DefaultMaxBytes = 4L * 1024 * 1024 * 1024;

        public async Task<string> ComputeHashAsync(IBrowserFile file, long maxBytes = DefaultMaxBytes)
        {
            if (file is null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using var stream = file.OpenReadStream(maxBytes);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[ReadBufferSize];
            int read;
            while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                hasher.AppendData(buffer, 0, read);
            }

            return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        }
    }
}

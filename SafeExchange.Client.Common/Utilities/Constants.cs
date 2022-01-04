/// <summary>
/// Constants
/// </summary>

namespace SafeExchange.Client.Common
{
    using System;

    public static class Constants
    {
        public const int MaxMainDataLength = 10 * 1024 * 1024; // 10 Mb

        public const long MaxAttachmentDataLength = 1L * 1024 * 1024 * 1024 * 1024; // 1 Tb

        public const int MaxChunkDataLength = 95 * 1024 * 1024; // 95 Mb
    }
}

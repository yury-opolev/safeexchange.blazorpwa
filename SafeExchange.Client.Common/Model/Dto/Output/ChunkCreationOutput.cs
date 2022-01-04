/// <summary>
/// ChunkCreationOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ChunkCreationOutput
    {
        public string ChunkName { get; set; }

        public string Hash { get; set; }

        public long Length { get; set; }

        public string? AccessTicket { get; set; }
    }
}

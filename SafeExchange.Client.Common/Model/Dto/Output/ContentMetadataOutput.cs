/// <summary>
/// ContentOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ContentMetadataOutput
    {
        public string ContentName { get; set; }

        public bool IsMain { get; set; }

        public string ContentType { get; set; }

        public string FileName { get; set; }

        public bool IsReady { get; set; }

        public List<ChunkOutput> Chunks { get; set; }
    }
}

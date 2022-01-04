/// <summary>
/// ChunkMetadata
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ChunkMetadata
    {
        public ChunkMetadata()
        { }

        public ChunkMetadata(ChunkMetadata source)
        {
            this.ChunkName = source.ChunkName;
            this.Hash = source.Hash;
            this.Length = source.Length;
        }

        public ChunkMetadata(ChunkCreationOutput source)
        {
            this.ChunkName = source.ChunkName;
            this.Hash = source.Hash;
            this.Length = source.Length;
        }

        public ChunkMetadata(ChunkOutput source)
        {
            this.ChunkName = source.ChunkName;
            this.Hash = source.Hash;
            this.Length = source.Length;
        }

        public string ChunkName { get; set; }

        public string Hash { get; set; }

        public long Length { get; set; }
    }
}

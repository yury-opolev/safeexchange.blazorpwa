/// <summary>
/// ContentMetadata
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Collections.Generic;

    public class ContentMetadata
    {
        public ContentMetadata()
        { }

        public ContentMetadata(ContentMetadata source)
        {
            this.ContentName = source.ContentName;
            this.IsMain = source.IsMain;
            this.ContentType = source.ContentType;
            this.FileName = source.FileName;
            this.IsReady = source.IsReady;

            this.Chunks = new List<ChunkMetadata>(source.Chunks.Count);
            this.Chunks.AddRange(source.Chunks.Select(c => new ChunkMetadata(c)));
        }

        public ContentMetadata(ContentMetadataOutput source)
        {
            this.ContentName = source.ContentName;
            this.IsMain = source.IsMain;
            this.ContentType = source.ContentType;
            this.FileName = source.FileName;
            this.IsReady = source.IsReady;

            this.Chunks = new List<ChunkMetadata>(source.Chunks.Count);
            this.Chunks.AddRange(source.Chunks.Select(c => new ChunkMetadata(c)));
        }

        public string ContentName { get; set; }

        public bool IsMain { get; set; }

        public string ContentType { get; set; }

        public string FileName { get; set; }

        public bool IsReady { get; set; }

        public List<ChunkMetadata> Chunks { get; set; }

        public void AppendChunk()
        {
            var chunk = new ChunkMetadata()
            {
                ChunkName = string.Empty,
                Hash = string.Empty,
                Length = 0
            };

            this.Chunks.Add(chunk);
        }

        public List<ChunkMetadata> DeleteChunks()
        {
            var removedChunks = this.Chunks;
            this.Chunks.Clear();
            return removedChunks;
        }

        public long GetLength()
        {
            var result = 0L;
            foreach (var chunk in this.Chunks)
            {
                result += chunk.Length;
            }

            return result;
        }

        public string GetLengthDescription()
        {
            var length = this.GetLength();
            return BytesToString(length);
        }

        public ContentMetadataCreationInput ToCreationDto() => new ()
        {
            ContentType = this.ContentType,
            FileName = this.FileName
        };

        public ContentMetadataUpdateInput ToUpdateDto() => new ()
        {
            ContentType = this.ContentType,
            FileName = this.FileName
        };

        public static string BytesToString(long byteCount)
        {
            string[] suf = { "b", "Kb", "Mb", "Gb", "Tb", "Pb", "Eb" }; // longs run out around Eb
            if (byteCount == 0)
            {
                return "0" + suf[0];
            }

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
    }
}

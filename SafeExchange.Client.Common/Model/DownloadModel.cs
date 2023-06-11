/// <summary>
/// DownloadStatus
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class DownloadModel
    {
        public DownloadModel(ContentMetadata attachment)
        {
            this.Status = DownloadStatus.NotStarted;
            this.ProgressPercents = 0.0f;
            Attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
        }

        public ContentMetadata Attachment { get; set; }

        public DownloadStatus Status { get; set; }

        public float ProgressPercents { get; set; }
    }
}

/// <summary>
/// AttachmentModel
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class AttachmentModel
    {
        public AttachmentModel(InputFileModel sourceFile)
        {
            this.SourceFile = sourceFile;
            this.Status = UploadStatus.NotStarted;
            this.Error = String.Empty;
            this.ProgressPercents = 0.0f;
        }

        public UploadStatus Status { get; set; }

        public string Error { get; set; }

        public float ProgressPercents { get; set; }

        public InputFileModel SourceFile { get; set; }
    }
}

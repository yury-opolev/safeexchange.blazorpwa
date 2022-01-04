﻿/// <summary>
/// AttachmentModel
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using Microsoft.AspNetCore.Components.Forms;
    using System;

    public class AttachmentModel
    {
        public AttachmentModel(IBrowserFile sourceFile)
        {
            this.SourceFile = sourceFile;
            this.Status = UploadStatus.NotStarted;
            this.Error = String.Empty;
            this.ProgressPercents = 0.0f;
        }

        public UploadStatus Status { get; set; }

        public string Error { get; set; }

        public float ProgressPercents { get; set; }

        public IBrowserFile SourceFile { get; set; }
    }
}

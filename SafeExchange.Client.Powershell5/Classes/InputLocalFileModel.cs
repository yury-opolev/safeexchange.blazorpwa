/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.IO;
    using System.Threading;
    using System.Web;

    public class InputLocalFileModel : InputFileModel
    {
        private FileInfo fileInfo;

        public InputLocalFileModel(string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new ArgumentException($"File '{fileName}' not exists.");
            }

            this.fileInfo = new FileInfo(fileName);
        }

        public override string ContentType => MimeMapping.GetMimeMapping(this.fileInfo.FullName);

        public override string Name => Path.GetFileName(this.fileInfo.FullName);

        public override long Size => this.fileInfo.Length;

        public override Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => this.fileInfo.OpenRead();
    }
}

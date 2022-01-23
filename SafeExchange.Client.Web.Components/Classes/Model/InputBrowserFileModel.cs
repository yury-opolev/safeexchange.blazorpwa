/// <summary>
/// InputBrowserFileModel
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using Microsoft.AspNetCore.Components.Forms;
    using SafeExchange.Client.Common.Model;
    using System;
    using System.IO;
    using System.Threading;

    public class InputBrowserFileModel : InputFileModel
    {
        private IBrowserFile browserFile;

        public InputBrowserFileModel(IBrowserFile browserFile)
        {
            this.browserFile = browserFile ?? throw new ArgumentNullException(nameof(browserFile));
        }

        public override string ContentType => this.browserFile.ContentType;

        public override string Name => this.browserFile.Name;

        public override long Size => this.browserFile.Size;

        public override Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => browserFile.OpenReadStream(maxAllowedSize, cancellationToken);
    }
}

/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.JSInterop;
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public class DownloadUploadHelper : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public DownloadUploadHelper(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
               "import", "./_content/SafeExchange.Client.Web.Components/downloadUploadHelper.js").AsTask());
        }

        public async Task InvokeAttachFileAsync(InputFile inputFileRef)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("triggerAttachFile", inputFileRef.Element);
        }

        public async Task DownloadFileFromStreamAsync(Stream source, string fileName, string contentType)
        {
            var module = await moduleTask.Value;
            using var streamRef = new DotNetStreamReference(source);
            await module.InvokeVoidAsync("downloadFileFromStream", fileName, contentType, streamRef);
        }

        public async Task<bool> SupportsFileSystemAccessAsync()
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<bool>("supportsFileSystemAccess");
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}

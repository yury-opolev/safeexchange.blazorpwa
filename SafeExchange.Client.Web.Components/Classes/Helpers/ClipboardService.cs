/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.JSInterop;
    using System;
    using System.Threading.Tasks;

    public class ClipboardService
    {
        private readonly IJSRuntime jsRuntime;

        public ClipboardService(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public ValueTask<string> ReadTextAsync()
        {
            return this.jsRuntime.InvokeAsync<string>("navigator.clipboard.readText");
        }

        public ValueTask WriteTextAsync(string text)
        {
            return this.jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
    }
}

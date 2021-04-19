/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.JSInterop;
    using System;
    using System.Threading.Tasks;

    public class TooltipsInitializer : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public TooltipsInitializer(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
               "import", "./_content/SafeExchange.Client.Web.Components/tooltipsInitializer.js").AsTask());
        }

        public async Task InitializeTooltipsAsync(string initialText, string clickText)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("addTooltips", initialText, clickText);
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

/// <summary>
/// ItemPickDialogHelper
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;
    using System;
    using System.Threading.Tasks;

    public class ItemPickDialogHelper : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public ItemPickDialogHelper(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
               "import", "./_content/SafeExchange.Client.Web.Components/itemPickDialogHelper.js").AsTask());
        }

        public async Task ShowModalAsync(ElementReference dialogRef)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("showModal", dialogRef);
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

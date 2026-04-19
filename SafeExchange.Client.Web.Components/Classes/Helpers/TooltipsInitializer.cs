/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
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

        public async Task InitializeQuillClipboardTooltipsAsync(ElementReference quillElement, string initialText, string clickText)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("addQuillClipboardTooltips", quillElement, initialText, clickText);
        }

        // Wires a Bootstrap tooltip onto a specific element. Idempotent —
        // safe to call from OnAfterRenderAsync on every render.
        public async Task InitializeSingleTooltipAsync(ElementReference element, string initialText, string clickText)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("addTooltipFor", element, initialText, clickText);
        }

        // Hover-only variant for elements whose tooltip simply reveals
        // the full text of a truncated label. No click-swap behaviour.
        public async Task InitializePlainTooltipAsync(ElementReference element)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("addPlainTooltipFor", element);
        }

        // Bulk-init every `[data-bs-toggle="tooltip"]` on the page as a
        // plain hover-only Bootstrap tooltip. Idempotent — safe to call
        // from OnAfterRenderAsync on every render. Use for pages that
        // render a variable number of tooltip-carrying elements (lists,
        // tables) where per-element ElementReference wiring is awkward.
        public async Task InitializeAllPlainTooltipsAsync()
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("initAllPlainTooltips");
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

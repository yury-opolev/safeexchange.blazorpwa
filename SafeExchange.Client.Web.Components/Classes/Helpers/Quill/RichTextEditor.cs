/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class RichTextEditor : IAsyncDisposable
    {
        public event EventHandler OnFocus;

        public event EventHandler OnBlur;

        public event EventHandler OnTextChange;

        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        private DotNetObjectReference<RichTextEditor> objRef;

        public RichTextEditor(IJSRuntime jsRuntime)
        {
            this.moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
               "import", "./_content/SafeExchange.Client.Web.Components/richTextEditor.js").AsTask());
        }

        public async Task InitializeEditorAsync(ElementReference elementRef, string placeholderText, bool readOnly, ElementReference nextElementRef)
        {
            this.objRef = DotNetObjectReference.Create(this);

            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("initializeEditor", this.objRef, elementRef, placeholderText, readOnly, nextElementRef);
        }

        public async Task SetEnabledAsync(ElementReference elementRef, bool enabled)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("setEnabled", elementRef, enabled);
        }

        public async Task<string> GetContentsAsync(ElementReference elementRef)
        {
            var module = await moduleTask.Value;
            var contents = await module.InvokeAsync<JsonElement>("getContents", elementRef);
            return contents.GetRawText();
        }

        public async Task SetContentsAsync(ElementReference elementRef, string contentToSet)
        {
            var module = await moduleTask.Value;
            var contentDocument = JsonDocument.Parse(contentToSet);
            await module.InvokeVoidAsync("setContents", elementRef, contentDocument.RootElement);
        }

        public async Task<string> GetHtmlAsync(ElementReference elementRef)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("getHtml", elementRef);
        }

        public async Task SetHtmlAsync(ElementReference elementRef, string contentToSet)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("setHtml", elementRef, contentToSet);
        }

        public async Task<string> GetTextAsync(ElementReference elementRef)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("getText", elementRef);
        }

        public async Task SetTextAsync(ElementReference elementRef, string text)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("setText", elementRef, text);
        }

        [JSInvokable]
        public void OnFocusJS()
        {
            this.OnFocus?.Invoke(this, EventArgs.Empty);
        }

        [JSInvokable]
        public void OnBlurJS()
        {
            this.OnBlur?.Invoke(this, EventArgs.Empty);
        }

        [JSInvokable]
        public void OnTextChangeJS()
        {
            this.OnTextChange?.Invoke(this, EventArgs.Empty);
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }

            objRef?.Dispose();
        }
    }
}

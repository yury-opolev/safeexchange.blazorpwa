/// <summary>
/// ColorThemeHelper
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.JSInterop;
    using SafeExchange.Client.Web.Components.Classes.Model;
    using System;
    using System.Threading.Tasks;

    public class ColorThemeHelper : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public ColorThemeHelper(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
               "import", "./_content/SafeExchange.Client.Web.Components/colorThemeHelper.js").AsTask());
        }

        public async Task<ColorTheme> GetPreferredThemeAsync()
        {
            var module = await moduleTask.Value;
            var preferredTheme = await module.InvokeAsync<string>("getPreferredTheme");

            if (string.IsNullOrEmpty(preferredTheme))
            {
                return ColorTheme.Light;
            }

            switch (preferredTheme)
            {
                case "light":
                    return ColorTheme.Light;

                case "dark":
                    return ColorTheme.Dark;

                case "auto":
                    return ColorTheme.Auto;

                default:
                    return ColorTheme.Light;
            }
        }

        public async Task SetThemeAsync(ColorTheme theme)
        {
            var themeStr = theme switch
            {
                ColorTheme.Light => "light",
                ColorTheme.Dark => "dark",
                ColorTheme.Auto => "auto",
                _ => "light"
            };

            var module = await moduleTask.Value;
            await module.InvokeAsync<string>("setTheme", themeStr);
            await module.InvokeAsync<string>("setStoredTheme", themeStr);
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

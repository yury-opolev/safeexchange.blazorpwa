namespace SafeExchange.AdminPanel.Services
{
    using Microsoft.JSInterop;
    using System;
    using System.Threading.Tasks;

    public sealed class AdminPreferences
    {
        private const string ThemeStorageKey = "admin-theme";

        private readonly IJSRuntime jsRuntime;
        private bool initialised;

        public AdminPreferences(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            this.SessionId = Guid.NewGuid().ToString("D");
            this.Theme = "auto";
        }

        public string SessionId { get; }

        public string Theme { get; private set; }

        public event EventHandler? ThemeChanged;

        public async Task InitialiseAsync()
        {
            if (this.initialised)
            {
                return;
            }
            this.initialised = true;

            try
            {
                var stored = await this.jsRuntime.InvokeAsync<string?>("localStorage.getItem", ThemeStorageKey);
                if (!string.IsNullOrEmpty(stored) && IsValidTheme(stored))
                {
                    this.Theme = stored;
                }
            }
            catch
            {
            }

            await this.ApplyAsync();
        }

        public async Task SetThemeAsync(string theme)
        {
            if (!IsValidTheme(theme) || string.Equals(this.Theme, theme, StringComparison.Ordinal))
            {
                return;
            }

            this.Theme = theme;
            try
            {
                await this.jsRuntime.InvokeVoidAsync("localStorage.setItem", ThemeStorageKey, theme);
            }
            catch
            {
            }

            await this.ApplyAsync();
            this.ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task ApplyAsync()
        {
            var effective = this.Theme;
            if (string.Equals(effective, "auto", StringComparison.Ordinal))
            {
                try
                {
                    var prefersDark = await this.jsRuntime.InvokeAsync<bool>(
                        "window.matchMedia",
                        "(prefers-color-scheme: dark)");
                    effective = prefersDark ? "dark" : "light";
                }
                catch
                {
                    effective = "dark";
                }
            }

            try
            {
                await this.jsRuntime.InvokeVoidAsync(
                    "eval",
                    $"document.documentElement.setAttribute('data-bs-theme', '{effective}')");
            }
            catch
            {
            }
        }

        private static bool IsValidTheme(string theme)
            => string.Equals(theme, "auto", StringComparison.Ordinal)
            || string.Equals(theme, "dark", StringComparison.Ordinal)
            || string.Equals(theme, "light", StringComparison.Ordinal);
    }
}

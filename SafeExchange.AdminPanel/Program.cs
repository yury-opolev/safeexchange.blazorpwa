namespace SafeExchange.AdminPanel
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using SafeExchange.AdminPanel.Services;
    using SafeExchange.Client.Common;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            // MSAL — same Entra app registration as the main site (declared in
            // wwwroot/appsettings.json). Separate origin in eventual prod so
            // cookies/storage don't collide, but the same identity.
            builder.Services.AddMsalAuthentication(options =>
            {
                builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
                var scopes = builder.Configuration.GetSection("AccessTokenScopes").Get<string[]>() ?? Array.Empty<string>();
                foreach (var scope in scopes)
                {
                    options.ProviderOptions.DefaultAccessTokenScopes.Add(scope);
                }

                // Force redirect login mode. The default is popup, and on iOS
                // Safari a popup is a separate browser tab whose token hand-off
                // back to the opener routinely fails — login then stalls on the
                // orphaned /authentication/login-callback tab. Redirect uses a
                // same-tab, full-page navigation that completes reliably on all
                // platforms. See docs and dotnet/aspnetcore#23621.
                options.ProviderOptions.LoginMode = "redirect";
            });

            // BackendApi HttpClient with the auth message handler so calls carry
            // a token automatically.
            builder.Services
                .AddHttpClient(ApiClient.DefaultHttpClientName, client =>
                {
                    var backend = builder.Configuration.GetSection("BackendApi")["BaseAddress"];
                    if (!string.IsNullOrEmpty(backend))
                    {
                        client.BaseAddress = new Uri(backend);
                    }
                })
                .AddHttpMessageHandler(sp =>
                {
                    var handler = sp.GetRequiredService<AuthorizationMessageHandler>()
                        .ConfigureHandler(
                            authorizedUrls: new[] { builder.Configuration.GetSection("BackendApi")["BaseAddress"]! },
                            scopes: builder.Configuration.GetSection("AccessTokenScopes").Get<string[]>() ?? Array.Empty<string>());
                    return handler;
                });

            // Default scoped HttpClient (origin-relative) so components can fetch
            // /version.json or other static assets without ceremony.
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddScoped<ApiClient>();
            builder.Services.AddScoped<AdminPreferences>();
            builder.Services.AddAuthorizationCore();

            await builder.Build().RunAsync();
        }
    }
}

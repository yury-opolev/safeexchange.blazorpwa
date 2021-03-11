/// <summary>
/// SafeExchange web components
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using System;
    using System.Net.Http;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class ServicesHelper
    {
        public static void ConfigureServices(WebAssemblyHostBuilder builder)
        {
            builder.Services.AddSingleton<StateContainer>();

            builder.Services.AddScoped<ApiAuthorizationMessageHandler>();
            var apiConfiguration = builder.Configuration.GetSection("BackendApi");
            builder.Services.AddHttpClient("BackendApi",
                client => client.BaseAddress = new Uri(apiConfiguration["BaseAddress"]))
                .AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

            builder.Services.AddScoped<ApiClient>();

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddScoped<ClipboardService>();

            builder.Services.AddMsalAuthentication(options =>
            {
                builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
                builder.Configuration.Bind("AccessTokenScopes", options.ProviderOptions.DefaultAccessTokenScopes);
                // builder.Configuration.Bind("AdditionalScopesToConsent", options.ProviderOptions.AdditionalScopesToConsent);
            });
        }
    }
}

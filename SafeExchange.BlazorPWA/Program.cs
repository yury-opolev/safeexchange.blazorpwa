
namespace SafeExchange.BlazorPWA
{
    using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddSingleton<StateContainer>();

            builder.Services.AddScoped<ApiAccessTokenProvider<ApiAuthorizationProviderOptions>>();
            builder.Services.AddScoped<ApiAuthorizationMessageHandler>();

            var apiConfiguration = builder.Configuration.GetSection("BackendApi");
            builder.Services.AddHttpClient("BackendApi",
                client => client.BaseAddress = new Uri(apiConfiguration["BaseAddress"]))
                .AddHttpMessageHandler<ApiAuthorizationMessageHandler>();

            builder.Services.AddScoped<ApiClient>();

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddOidcAuthentication(options =>
            {
                builder.Configuration.Bind("Local", options.ProviderOptions);
            });

            await builder.Build().RunAsync();
        }
    }
}

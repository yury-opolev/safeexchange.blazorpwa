/// <summary>
/// SafeExchange web components
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Polly;
    using SafeExchange.Client.Common;

    public class ServicesHelper
    {
        private static readonly IList<HttpStatusCode> TransientErrorStatusCodes = new List<HttpStatusCode>()
        {
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout,
        };

        public static IAsyncPolicy<HttpResponseMessage> DefaultRetryPolicy
            => Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(msg => TransientErrorStatusCodes.Contains(msg.StatusCode))
                .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(retryAttempt));

        public static void ConfigureServices(WebAssemblyHostBuilder builder)
        {
            builder.Services.AddSingleton<StateContainer>();

            builder.Services.AddScoped<ApiAuthorizationMessageHandler>();
            var apiConfiguration = builder.Configuration.GetSection("BackendApi");
            builder.Services.AddHttpClient("BackendApi",
                client =>
                {
                    client.BaseAddress = new Uri(apiConfiguration["BaseAddress"]);
                    client.Timeout = TimeSpan.FromMinutes(3);
                })
                .AddHttpMessageHandler<ApiAuthorizationMessageHandler>()
                .AddPolicyHandler(DefaultRetryPolicy)
                .SetHandlerLifetime(TimeSpan.FromMinutes(10));

            builder.Services.AddScoped<ApiClient>();

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddScoped<ClipboardService>();
            builder.Services.AddScoped<TooltipsInitializer>();
            builder.Services.AddScoped<PushNotifications>();
            builder.Services.AddScoped<NotificationsSubscriber>();
            builder.Services.AddScoped<RichTextEditor>();
            builder.Services.AddScoped<DownloadUploadHelper>();

            builder.Services.AddMsalAuthentication(options =>
            {
                builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
                builder.Configuration.Bind("AccessTokenScopes", options.ProviderOptions.DefaultAccessTokenScopes);
                builder.Configuration.Bind("AdditionalScopesToConsent", options.ProviderOptions.AdditionalScopesToConsent);
            });
        }
    }
}


namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
    using Microsoft.Extensions.Configuration;

    public class ApiAuthorizationMessageHandler : AuthorizationMessageHandler
    {
        public ApiAuthorizationMessageHandler(IAccessTokenProvider provider, NavigationManager navigationManager, IConfiguration configuration)
            : base(provider, navigationManager)
        {
            var apiUrl = configuration.GetSection("BackendApi").GetValue<string>("BaseAddress");
            var accessTokenScopes = configuration.GetValue<string[]>("AccessTokenScopes");
            ConfigureHandler(
                authorizedUrls: new[] { apiUrl },
                scopes: accessTokenScopes);
        }
    }
}

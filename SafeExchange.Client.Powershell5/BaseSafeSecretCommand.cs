/// <summary>
/// BaseSafeSecretCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;

    public class BaseSafeSecretCommand : Cmdlet
    {
        public static readonly string DefaultTenantId = "1472703e-99d8-45ee-aeac-7ec3ae9ab104";
        
        public static readonly string DefaultClientId = "b75602e2-2c3b-446b-9c92-77021db634eb";
        
        public static readonly string DefaultAuthority = "https://login.microsoftonline.com/1472703e-99d8-45ee-aeac-7ec3ae9ab104";

        public static readonly string DefaultBackendBaseAddress = "https://safeexchange-backend.azurewebsites.net/api/";

        public static readonly IList<string> DefaultScopes = new List<string>() { "https://spaceoysteroutlook.onmicrosoft.com/user_impersonation" };

        [Parameter()]
        public string BackendBaseAddress { get; set; }

        [Parameter()]
        public IEnumerable<string> Scopes { get; set; }

        [Parameter()]
        public ClientTokenProvider TokenProvider { get; set; }

        protected ApiClient apiClient;

        protected override void BeginProcessing()
        {
            if (this.TokenProvider == null)
            {
                this.TokenProvider = new ClientTokenProvider(DefaultClientId, DefaultAuthority, DefaultTenantId);
            }

            if (string.IsNullOrEmpty(this.BackendBaseAddress))
            {
                this.BackendBaseAddress = DefaultBackendBaseAddress;
            }

            if (this.Scopes == null || !this.Scopes.Any())
            {
                this.Scopes = DefaultScopes;
            }

            var authHttpClient = new AuthenticatedHttpClient(DefaultBackendBaseAddress, DefaultScopes, this.TokenProvider);
            this.apiClient = new ApiClient(authHttpClient.HttpClient);
        }
    }
}

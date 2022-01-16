/// <summary>
/// BaseSafeSecretCommand
/// </summary>

namespace SafeExchange.Client.Powershell
{
    using SafeExchange.Client.Common;
    using System.Management.Automation;

    public class BaseSafeSecretCommand : Cmdlet
    {
        public static readonly string DefaultTenantId = "{TENANT ID}";
        
        public static readonly string DefaultClientId = "{CLIENT ID}";
        
        public static readonly string DefaultAuthority = "{AUTHORITY}";

        public static readonly string DefaultBackendBaseAddress = "{BACKEND URI}";

        public static readonly IList<string> DefaultScopes = new List<string>() { "{SCOPE}" };

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

            if (this.Scopes == null || this.Scopes.Count() == 0)
            {
                this.Scopes = DefaultScopes;
            }

            var authHttpClient = new AuthenticatedHttpClient(DefaultBackendBaseAddress, DefaultScopes, this.TokenProvider);
            this.apiClient = new ApiClient(authHttpClient.HttpClient);
        }
    }
}

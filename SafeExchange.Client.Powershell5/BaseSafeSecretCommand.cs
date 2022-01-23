/// <summary>
/// BaseSafeSecretCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Management.Automation;
    using System.Reflection;

    public class BaseSafeSecretCommand : Cmdlet
    {
        public static readonly string FrontendBaseAddress = "{FRONTEND BASE ADDRESS}";

        public static readonly string DefaultTenantId = "{TENANT ID}";
        
        public static readonly string DefaultClientId = "{CLIENT ID}";
        
        public static readonly string DefaultAuthority = "{AUTHORITY}";

        public static readonly string DefaultRedirectUri = "{REDIRECT URI}";

        public static readonly string DefaultBackendBaseAddress = "{BACKEND BASE ADDRESS}";

        public static readonly IList<string> DefaultScopes = new List<string>() { "{SCOPE}" };

        [Parameter()]
        public string BackendBaseAddress { get; set; }

        [Parameter()]
        public ClientTokenProvider TokenProvider { get; set; }

        protected ApiClient apiClient;

        private Dictionary<string, Assembly> redirectedAssemblies;

        public BaseSafeSecretCommand() 
            : base()
        {
            this.AddAssembliesRedirect();
        }

        protected override void BeginProcessing()
        {
            if (this.TokenProvider == null)
            {
                this.TokenProvider = new ClientTokenProvider(DefaultClientId, DefaultAuthority, DefaultTenantId, DefaultRedirectUri, DefaultScopes);
            }

            if (string.IsNullOrEmpty(this.BackendBaseAddress))
            {
                this.BackendBaseAddress = DefaultBackendBaseAddress;
            }

            var authHttpClient = new AuthenticatedHttpClient(DefaultBackendBaseAddress, this.TokenProvider);
            this.apiClient = new ApiClient(new HttpClientFactory(authHttpClient.HttpClient));
        }

        private void AddAssembliesRedirect()
        {
            var compilerServiceAssemblyLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "System.Runtime.CompilerServices.Unsafe.dll");
            var compilerServicesAssembly = Assembly.LoadFrom(compilerServiceAssemblyLocation);

            this.redirectedAssemblies = new Dictionary<string, Assembly>();
            this.redirectedAssemblies.Add(
                "System.Runtime.CompilerServices.Unsafe, Version=4.0.4.1, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                compilerServicesAssembly);

            ResolveEventHandler onAssemblyResolve = (sender, e) =>
            {
                foreach (var item in this.redirectedAssemblies)
                {
                    if (e.Name.Equals(item.Key))
                    {
                        return item.Value;
                    }
                }

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Equals(e.Name))
                    {
                        return assembly;
                    }
                }

                return null;
            };

            AppDomain.CurrentDomain.AssemblyResolve += onAssemblyResolve;
        }

        public string CreateSafeSecretLink(string secretName)
        {
            return $"{FrontendBaseAddress.TrimEnd('/')}/viewdata/{secretName}";
        }
    }
}

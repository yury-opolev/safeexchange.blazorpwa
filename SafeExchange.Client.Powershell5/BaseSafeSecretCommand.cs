/// <summary>
/// BaseSafeSecretCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Reflection;

    public class BaseSafeSecretCommand : Cmdlet
    {
        public static readonly string DefaultTenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";
        
        public static readonly string DefaultClientId = "b75602e2-2c3b-446b-9c92-77021db634eb";
        
        public static readonly string DefaultAuthority = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad";

        public static readonly string DefaultRedirectUri = "msalb75602e2-2c3b-446b-9c92-77021db634eb://auth";

        public static readonly string DefaultBackendBaseAddress = "https://safeexchange-backend.azurewebsites.net/api/";

        public static readonly IList<string> DefaultScopes = new List<string>() { "https://spaceoysteroutlook.onmicrosoft.com/user_impersonation" };

        [Parameter()]
        public string BackendBaseAddress { get; set; }

        [Parameter()]
        public IEnumerable<string> Scopes { get; set; }

        [Parameter()]
        public ClientTokenProvider TokenProvider { get; set; }

        [Parameter()]
        public bool UseIntegratedWindowsAuth { get; set; }

        protected ApiClient apiClient;

        private Assembly compilerServicesAssembly;

        public BaseSafeSecretCommand() 
            : base()
        {
            var compilerServiceAssemblyLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "System.Runtime.CompilerServices.Unsafe.dll");
            this.compilerServicesAssembly = Assembly.LoadFrom(compilerServiceAssemblyLocation);
            ResolveEventHandler onAssemblyResolve = (sender, e) =>
            {
                if (e.Name.Equals("System.Runtime.CompilerServices.Unsafe, Version=4.0.4.1, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"))
                {
                    return this.compilerServicesAssembly;
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

        protected override void BeginProcessing()
        {
            if (this.TokenProvider == null)
            {
                this.TokenProvider = new ClientTokenProvider(DefaultClientId, DefaultAuthority, DefaultTenantId, DefaultRedirectUri)
                {
                    UseIntegratedWindowsAuth = this.UseIntegratedWindowsAuth
                };
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

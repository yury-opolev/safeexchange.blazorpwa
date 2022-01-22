/// <summary>
/// ClientTokenProvider
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using Microsoft.Identity.Client;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class ClientTokenProvider
    {
        public bool UseIntegratedWindowsAuth { get; set; }

        private IPublicClientApplication clientApplication;

        private ConcurrentDictionary<string, ClientToken> tokens;

        private IEnumerable<string> scopes;

        public ClientTokenProvider(string clientId, string authority, string tenantId, string redirectUri, IEnumerable<string> scopes)
        {
            this.scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));

            this.tokens = new ConcurrentDictionary<string, ClientToken>();

            this.clientApplication = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(authority).WithTenantId(tenantId).WithRedirectUri(redirectUri).Build();
        }

        public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            if (this.TryGetCachedToken(this.scopes, out var accessToken))
            {
                return accessToken;
            }

            var authResult = await this.AcquireTokenAsync(this.scopes, cancellationToken);
            this.CacheToken(authResult);
            return authResult.AccessToken;
        }

        private void CacheToken(AuthenticationResult authResult)
        {
            var key = this.GetKeyFromScopes(authResult.Scopes);
            var secureToken = new NetworkCredential(string.Empty, authResult.AccessToken).SecurePassword;
            var clientToken = new ClientToken(authResult.Scopes, secureToken, authResult.ExpiresOn);

            this.tokens.AddOrUpdate(key, clientToken, (k, v) => clientToken);
        }

        private bool TryGetCachedToken(IEnumerable<string> scopes, out string token)
        {
            token = string.Empty;

            var key = this.GetKeyFromScopes(scopes);
            if (this.tokens.TryGetValue(key, out var clientToken))
            {
                var bufferTime = TimeSpan.FromMinutes(1);
                var notAfter = new DateTimeOffset(DateTime.UtcNow + bufferTime);
                if (clientToken.ExpiresOn <= notAfter)
                {
                    token = new NetworkCredential(string.Empty, clientToken.Token).Password;
                    return true;
                }
            }

            return false;
        }

        private async Task<AuthenticationResult> AcquireTokenAsync(IEnumerable<string> scopes, CancellationToken cancellationToken)
        {
            try
            {
                var accounts = await this.clientApplication.GetAccountsAsync();
                if (accounts.Any())
                {
                    return await this.clientApplication.AcquireTokenSilent(scopes, accounts.First()).ExecuteAsync(cancellationToken);
                }
                else
                {
                    if (this.UseIntegratedWindowsAuth)
                    {
                        return await this.clientApplication.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync(cancellationToken);
                    }

                    return await this.clientApplication.AcquireTokenInteractive(scopes).ExecuteAsync(cancellationToken);
                }
            }
            catch (MsalUiRequiredException)
            {
                return await this.clientApplication.AcquireTokenInteractive(scopes).ExecuteAsync(cancellationToken);
            }
        }

        private string GetKeyFromScopes(IEnumerable<string> scopes)
        {
            return string.Join("|", scopes.OrderBy(s => s).Select(s => s.ToLowerInvariant()));
        }
    }
}

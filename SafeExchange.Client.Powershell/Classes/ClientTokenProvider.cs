/// <summary>
/// ClientTokenProvider
/// </summary>

namespace SafeExchange.Client.Powershell
{
    using Microsoft.Identity.Client;
    using System;
    using System.Collections.Concurrent;
    using System.Net;

    public class ClientTokenProvider
    {
        private IPublicClientApplication clientApplication;

        private ConcurrentDictionary<string, ClientToken> tokens;

        public ClientTokenProvider(string clientId, string authority, string tenantId)
        {
            this.tokens = new ConcurrentDictionary<string, ClientToken>();

            this.clientApplication = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(authority).WithTenantId(tenantId).Build();
        }

        public async Task<string> GetTokenAsync(IEnumerable<string> scopes, CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            if (this.TryGetCachedToken(scopes, out var accessToken))
            {
                return accessToken;
            }

            var authResult = await this.AcquireTokenAsync(scopes, cancellationToken);
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
                    return await this.clientApplication.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync(cancellationToken);
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

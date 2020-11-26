
namespace SafeExchange.BlazorPWA
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    public class ApiAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IAccessTokenProvider _provider;
        private readonly NavigationManager _navigation;

        private AccessTokenRequestOptions _tokenOptions;

        private AuthenticationHeaderValue _cachedHeader;

        private AccessToken _lastToken;

        public ApiAuthorizationMessageHandler(ApiAccessTokenProvider<ApiAuthorizationProviderOptions> provider, NavigationManager navigation)
            : base()
        {
            _provider = provider;
            _navigation = navigation;
        }

        public ApiAuthorizationMessageHandler(ApiAccessTokenProvider<ApiAuthorizationProviderOptions> provider, NavigationManager navigation, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _provider = provider;
            _navigation = navigation;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.Now;
            if (_lastToken == null || now >= _lastToken.Expires.AddMinutes(-5))
            {
                var tokenResult = _tokenOptions != null ?
                    await _provider.RequestAccessToken(_tokenOptions) :
                    await _provider.RequestAccessToken();

                if (tokenResult.TryGetToken(out var token))
                {
                    _lastToken = token;
                    _cachedHeader = new AuthenticationHeaderValue("Bearer", _lastToken.Value);
                }
                else
                {
                    throw new AccessTokenNotAvailableException(_navigation, tokenResult, _tokenOptions?.Scopes);
                }
            }

            request.Headers.Authorization = _cachedHeader;
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

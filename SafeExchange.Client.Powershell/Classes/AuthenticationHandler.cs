/// <summary>
/// AuthenticationHandler
/// </summary>

namespace SafeExchange.Client.Powershell.Classes
{
    using System;
    using System.Net.Http.Headers;

    public class AuthenticationHandler : DelegatingHandler
    {
        private ClientTokenProvider tokenProvider;

        private IEnumerable<string> scopes;

        public AuthenticationHandler(ClientTokenProvider tokenProvider, IEnumerable<string> scopes)
            : this(new HttpClientHandler(), tokenProvider, scopes)
        { }

        public AuthenticationHandler(HttpMessageHandler innerHandler, ClientTokenProvider tokenProvider, IEnumerable<string> scopes)
            : base(innerHandler)
        {
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            this.scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = await this.tokenProvider.GetTokenAsync(this.scopes, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

/// <summary>
/// AuthenticationHandler
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    public class AuthenticationHandler : DelegatingHandler
    {
        private ClientTokenProvider tokenProvider;

        public AuthenticationHandler(ClientTokenProvider tokenProvider)
            : this(new HttpClientHandler(), tokenProvider)
        { }

        public AuthenticationHandler(HttpMessageHandler innerHandler, ClientTokenProvider tokenProvider)
            : base(innerHandler)
        {
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = await this.tokenProvider.GetTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

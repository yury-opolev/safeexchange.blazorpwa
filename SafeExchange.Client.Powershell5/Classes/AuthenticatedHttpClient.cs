/// <summary>
/// AuthenticatedHttpClient
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    public class AuthenticatedHttpClient : HttpClient
    {
        public HttpClient HttpClient { get; private set; }

        public AuthenticatedHttpClient(string baseAddress, IEnumerable<string> scopes, ClientTokenProvider tokenProvider)
        {
            var authenticationHandler = new AuthenticationHandler(tokenProvider, scopes);
            this.HttpClient = new HttpClient(authenticationHandler)
            {
                BaseAddress = new Uri(baseAddress)
            };
        }
    }
}

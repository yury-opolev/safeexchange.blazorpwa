/// <summary>
/// AuthenticatedHttpClient
/// </summary>

namespace SafeExchange.Client.Powershell
{
    using System;
    using System.Collections.Generic;

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

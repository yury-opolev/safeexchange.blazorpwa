/// <summary>
/// AuthenticatedHttpClient
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using System;
    using System.Net.Http;

    public class AuthenticatedHttpClient : HttpClient
    {
        public HttpClient HttpClient { get; private set; }

        public AuthenticatedHttpClient(string baseAddress, ClientTokenProvider tokenProvider)
        {
            var authenticationHandler = new AuthenticationHandler(tokenProvider);
            this.HttpClient = new HttpClient(authenticationHandler)
            {
                BaseAddress = new Uri(baseAddress)
            };
        }
    }
}

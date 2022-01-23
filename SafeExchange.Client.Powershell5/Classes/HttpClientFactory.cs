/// <summary>
/// HttpClientFactory
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using System;
    using System.Net.Http;

    public class HttpClientFactory : IHttpClientFactory
    {
        private HttpClient httpClient;

        public HttpClientFactory(HttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public HttpClient CreateClient(string name) => this.httpClient;
    }
}

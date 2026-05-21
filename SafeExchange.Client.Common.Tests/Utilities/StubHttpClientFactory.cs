/// <summary>
/// StubHttpClientFactory — IHttpClientFactory that returns a single HttpClient
/// configured against the supplied HttpMessageHandler. BaseAddress is set to a
/// stable test URL so the relative paths emitted by ApiClient resolve.
/// </summary>

namespace SafeExchange.Client.Common.Tests.Utilities
{
    using System;
    using System.Net.Http;

    public sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public static readonly Uri BaseAddress = new Uri("https://test.example/api/");

        private readonly HttpMessageHandler handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            this.handler = handler;
        }

        public HttpClient CreateClient(string name) =>
            new HttpClient(this.handler, disposeHandler: false)
            {
                BaseAddress = BaseAddress
            };
    }
}

/// <summary>
/// CapturingHttpMessageHandler — fake HttpMessageHandler for ApiClient tests.
/// Records the first request sent through it and returns a caller-configured
/// HttpResponseMessage. Tests can then assert on Method, RequestUri and Content
/// (read once and cached so the assertions don't fight Content disposal).
/// </summary>

namespace SafeExchange.Client.Common.Tests.Utilities
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> responder;

        public CapturingHttpMessageHandler(HttpResponseMessage response)
        {
            this.responder = _ => Task.FromResult(response);
        }

        public CapturingHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody = null)
        {
            this.responder = _ =>
            {
                var resp = new HttpResponseMessage(statusCode);
                if (jsonBody != null)
                {
                    resp.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
                }
                return Task.FromResult(resp);
            };
        }

        public HttpRequestMessage? CapturedRequest { get; private set; }

        public string? CapturedRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.CapturedRequest = request;
            if (request.Content != null)
            {
                this.CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            return await this.responder(request).ConfigureAwait(false);
        }
    }
}
